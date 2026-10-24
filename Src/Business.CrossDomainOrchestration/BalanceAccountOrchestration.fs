module Business.CrossDomainOrchestration.BalanceAccountOrchestration

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.General.PersonComponent
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// One Balance to record: the Balance Account, the balance date and the amount.
type BalanceInput = BalanceAccountId * LocalDate * Money.Money

type RecordedBalance = {
    balance: BalanceAccountBalance.BalanceAccountBalance
    replacedExisting: bool
}

let private confirmBalanceAccountNameFree
    (context: Context.Context)
    (name: BalanceAccountName)
    (self: BalanceAccountId option)
    : Result<unit, IAppError> =
    result {
        let! found = BalanceAccount.fetchByName context name
        match found with
        | Some balanceAccount when Some(balanceAccount |> BalanceAccount.balanceAccountId) <> self ->
            return! error (PositionsBalanceAccountNameAlreadyExists(name |> BalanceAccountName.value))
        | _ -> return ()
    }

/// Confirms the owners given for a Balance Account: at least one, no Person twice, each an existing Person. Returns them
/// as the Balance Account's set of owners.
let ownerSetOf
    (context: Context.Context)
    (balanceAccountName: BalanceAccountName)
    (owners: PersonId list)
    : Result<Set<PersonId>, IAppError> =
    let shownName = balanceAccountName |> BalanceAccountName.value
    result {
        do! if owners |> List.isEmpty then error (PositionsBalanceAccountHasNoOwners shownName) else Ok()
        let! persons = owners |> List.map (Person.fetchById context) |> convertListOfResultsToResultsList
        do!
            match
                persons
                |> List.map (Person.personName >> PersonName.value)
                |> List.countBy id
                |> List.tryFind (fun (_, count) -> count > 1)
            with
            | Some(repeated, _) -> error (PositionsBalanceAccountOwnerRepeated(shownName, repeated))
            | None -> Ok()
        return owners |> Set.ofList
    }

let constructNewAndPersist
    (context: Context.Context)
    (name: BalanceAccountName)
    (kind: BalanceAccountKind)
    (institution: Institution)
    (owners: PersonId list)
    : Result<BalanceAccount.BalanceAccount, IAppError> =
    let instant = context |> Context.getInitiationInstant
    result {
        do! confirmBalanceAccountNameFree context name None
        let! ownerSet = ownerSetOf context name owners
        let balanceAccount =
            BalanceAccount.create (BalanceAccountId.create ()) name kind institution ownerSet instant instant
        do! balanceAccount |> BalanceAccount.persist context
        return balanceAccount
    }

/// ownersUpdate carries the complete new set.
let updateBalanceAccount
    (context: Context.Context)
    (fieldUpdates: BalanceAccount.BalanceAccountFieldUpdates)
    : Result<BalanceAccount.BalanceAccount, IAppError> =
    result {
        let self = fieldUpdates.balanceAccountIdToUpdate
        let! balanceAccount = self |> BalanceAccount.fetchById context
        do!
            match fieldUpdates.balanceAccountNameUpdate with
            | SetTo newName -> confirmBalanceAccountNameFree context newName (Some self)
            | NoChange -> Ok()
        let resultingName =
            fieldUpdates.balanceAccountNameUpdate |> valueOrCurrent (balanceAccount |> BalanceAccount.balanceAccountName)
        do!
            match fieldUpdates.ownersUpdate with
            | SetTo owners -> owners |> Set.toList |> ownerSetOf context resultingName |> Result.map ignore
            | NoChange -> Ok()
        return! BalanceAccount.update context fieldUpdates
    }

/// Every Balance Account, ordered by name.
let listBalanceAccounts (context: Context.Context) : Result<BalanceAccount.BalanceAccount list, IAppError> =
    BalanceAccount.fetchAll context
    |> Result.map (List.sortBy (BalanceAccount.balanceAccountName >> BalanceAccountName.value))

/// Deletes the Balance Account with its owners, and returns it as it stood before deletion.
let deleteBalanceAccount
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId)
    : Result<BalanceAccount.BalanceAccount, IAppError> =
    result {
        let! balanceAccount = balanceAccountId |> BalanceAccount.fetchById context
        let shownName = balanceAccount |> BalanceAccount.balanceAccountName |> BalanceAccountName.value
        let! hasBalances = balanceAccountId |> BalanceAccountBalance.existsForAccount context
        do! if hasBalances then error (PositionsBalanceAccountHasBalances shownName) else Ok()
        let! mortgaged = balanceAccountId |> Property.fetchByMortgageBalanceAccountId context
        do!
            match mortgaged with
            | Some property ->
                let propertyName = property |> Property.propertyName |> PropertyName.value
                error (PositionsBalanceAccountIsPropertyMortgage(shownName, propertyName))
            | None -> Ok()
        do! balanceAccountId |> BalanceAccount.delete context
        return balanceAccount
    }

let private shownNameOf (context: Context.Context) (balanceAccountId: BalanceAccountId) : Result<string, IAppError> =
    balanceAccountId
    |> BalanceAccount.fetchById context
    |> Result.map (BalanceAccount.balanceAccountName >> BalanceAccountName.value)

let private confirmNoRepeatedBalance (context: Context.Context) (inputs: BalanceInput list) : Result<unit, IAppError> =
    inputs
    |> List.countBy (fun (balanceAccountId, date, _) -> balanceAccountId, date)
    |> List.tryFind (fun (_, count) -> count > 1)
    |> function
        | Some((balanceAccountId, date), _) ->
            balanceAccountId
            |> shownNameOf context
            |> Result.bind (fun shownName -> error (PositionsBalanceRepeatedInRequest(shownName, date)))
        | None -> Ok()

let private recordOne
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId, balanceDate: LocalDate, amount: Money.Money)
    : Result<RecordedBalance, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let currentDate = instant |> Calendar.dateFromInstant
    result {
        let! shownName = balanceAccountId |> shownNameOf context
        do!
            if balanceDate > currentDate then error (PositionsBalanceDateLaterThanCurrentDate(shownName, balanceDate))
            else Ok()
        let! existing = BalanceAccountBalance.fetchByAccountAndDate context balanceAccountId balanceDate
        let! stored =
            match existing with
            | Some found ->
                BalanceAccountBalance.update
                    context
                    { balanceAccountBalanceIdToUpdate = found |> BalanceAccountBalance.balanceAccountBalanceId
                      amountUpdate = SetTo amount }
            | None ->
                let created =
                    BalanceAccountBalance.create
                        (BalanceAccountBalanceId.create ()) balanceAccountId balanceDate amount instant instant
                created |> BalanceAccountBalance.persist context |> Result.map (fun () -> created)
        return { balance = stored; replacedExisting = existing |> Option.isSome }
    }

/// Records every Balance or, on the first failure, returns its error; the caller's transaction makes that all or none.
let recordBalances (context: Context.Context) (inputs: BalanceInput list) : Result<RecordedBalance list, IAppError> =
    result {
        do! confirmNoRepeatedBalance context inputs
        return! inputs |> List.map (recordOne context) |> convertListOfResultsToResultsList
    }

/// Deletes the account's Balance dated so, and returns it as it stood before deletion.
let deleteBalance
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId)
    (balanceDate: LocalDate)
    : Result<BalanceAccountBalance.BalanceAccountBalance, IAppError> =
    result {
        let! shownName = balanceAccountId |> shownNameOf context
        let! existing = BalanceAccountBalance.fetchByAccountAndDate context balanceAccountId balanceDate
        match existing with
        | Some balance ->
            do! balance |> BalanceAccountBalance.balanceAccountBalanceId |> BalanceAccountBalance.delete context
            return balance
        | None -> return! error (PositionsBalanceDoesntExist(shownName, balanceDate))
    }

/// The account's Balances dated in the range, both ends included, in date order.
let listBalances
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<BalanceAccountBalance.BalanceAccountBalance list, IAppError> =
    result {
        do! if endDate < beginDate then error (PositionsBalanceListEndBeforeBegin(beginDate, endDate)) else Ok()
        let! _ = balanceAccountId |> BalanceAccount.fetchById context
        return! BalanceAccountBalance.fetchBetween context balanceAccountId beginDate endDate
    }
