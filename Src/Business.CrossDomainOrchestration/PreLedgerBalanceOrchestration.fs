module Business.CrossDomainOrchestration.PreLedgerBalanceOrchestration

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.PositionsLedgerLinks

/// One pre-ledger balance to record: the ledger account, the balance date and the balance.
type PreLedgerBalanceInput = AccountId * LocalDate * Money.Money

/// A pre-ledger balance as stored, with its account's code and name.
type PreLedgerBalanceView = {
    preLedgerBalance: PreLedgerBalance.PreLedgerBalance
    accountCode: string
    accountName: string
}

type RecordedPreLedgerBalance = {
    balance: PreLedgerBalanceView
    replacedExisting: bool
}

let private viewOf (account: Account.Account) (preLedgerBalance: PreLedgerBalance.PreLedgerBalance) =
    { preLedgerBalance = preLedgerBalance
      accountCode = codeOf account
      accountName = account |> Account.accountName |> AccountName.value }

let private confirmNoRepeatedBalance (context: Context.Context) (inputs: PreLedgerBalanceInput list) : Result<unit, IAppError> =
    inputs
    |> List.countBy (fun (accountId, date, _) -> accountId, date)
    |> List.tryFind (fun (_, count) -> count > 1)
    |> function
        | Some((accountId, date), _) ->
            accountId
            |> fetchLedgerAccount context
            |> Result.bind (fun account -> error (PositionsPreLedgerBalanceRepeatedInRequest(codeOf account, date)))
        | None -> Ok()

let private confirmAssetOrLiability (account: Account.Account) : Result<unit, IAppError> =
    match account |> Account.accountType with
    | AccountType.Asset
    | AccountType.Liability -> Ok()
    | other -> error (PositionsPreLedgerAccountTypeNotAllowed(codeOf account, other |> AccountType.toString))

// Net worth counts what a linked account stands for from Positions and never reads its balance, so a balance recorded
// for one would count for nothing. A Property's mortgage account is not linked in that sense.
let private confirmNotLinked (context: Context.Context) (account: Account.Account) : Result<unit, IAppError> =
    let accountId = account |> Account.accountId
    result {
        let! investmentAccount = accountId |> InvestmentAccount.fetchByLedgerAccountId context
        do!
            match investmentAccount with
            | Some linked ->
                let name = linked |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
                error (PositionsPreLedgerAccountLinkedToInvestmentAccount(codeOf account, name))
            | None -> Ok()
        let! property = accountId |> Property.fetchByAssetAccountId context
        do!
            match property with
            | Some linked ->
                let name = linked |> Property.propertyName |> PropertyName.value
                error (PositionsPreLedgerAccountIsPropertyAsset(codeOf account, name))
            | None -> Ok()
    }

/// The start of the earliest fiscal period, when there is one.
let earliestFiscalPeriodStart (context: Context.Context) : Result<LocalDate option, IAppError> =
    FiscalPeriod.fetchAll context false
    |> Result.map (fun periods ->
        match periods with
        | [] -> None
        | _ -> periods |> List.map FiscalPeriod.startDate |> List.min |> Some)

let private recordOne
    (context: Context.Context)
    (ledgerStart: LocalDate option)
    (accountId: AccountId, balanceDate: LocalDate, balance: Money.Money)
    : Result<RecordedPreLedgerBalance, IAppError> =
    let instant = context |> Context.getInitiationInstant
    result {
        let! account = accountId |> fetchLedgerAccount context
        do! confirmAssetOrLiability account
        do! confirmNotLinked context account
        do! PreLedgerBalance.confirmBeforeLedger (codeOf account) balanceDate ledgerStart
        let! existing = PreLedgerBalance.fetchByAccountAndDate context accountId balanceDate
        let! stored =
            match existing with
            | Some found -> PreLedgerBalance.replaceBalance context found balance
            | None ->
                let created =
                    PreLedgerBalance.create (PreLedgerBalanceId.create ()) accountId balanceDate balance instant instant
                created |> PreLedgerBalance.persist context |> Result.map (fun () -> created)
        return { balance = viewOf account stored; replacedExisting = existing |> Option.isSome }
    }

/// Records every balance or, on the first failure, returns its error; the caller's transaction makes that all or none.
let recordPreLedgerBalances
    (context: Context.Context)
    (inputs: PreLedgerBalanceInput list)
    : Result<RecordedPreLedgerBalance list, IAppError> =
    result {
        do! if inputs |> List.isEmpty then error PositionsPreLedgerBalanceListIsEmpty else Ok()
        do! confirmNoRepeatedBalance context inputs
        let! ledgerStart = earliestFiscalPeriodStart context
        return! inputs |> List.map (recordOne context ledgerStart) |> convertListOfResultsToResultsList
    }

/// Deletes the account's balance dated so, and returns it as it stood before deletion.
let deletePreLedgerBalance
    (context: Context.Context)
    (accountId: AccountId)
    (balanceDate: LocalDate)
    : Result<PreLedgerBalanceView, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        let! existing = PreLedgerBalance.fetchByAccountAndDate context accountId balanceDate
        let! found =
            match existing with
            | Some b -> Ok b
            | None -> error (PositionsPreLedgerBalanceDoesntExist(codeOf account, balanceDate))
        do! found |> PreLedgerBalance.preLedgerBalanceId |> PreLedgerBalance.delete context
        return viewOf account found
    }

/// The balances dated in the range, both ends included, of one account or of every account, ordered by account code
/// and then balance date.
let listPreLedgerBalances
    (context: Context.Context)
    (accountId: AccountId option)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<PreLedgerBalanceView list, IAppError> =
    result {
        do! if endDate < beginDate then error (PositionsPreLedgerListEndBeforeBegin(beginDate, endDate)) else Ok()
        let! _ = accountId |> convertOptionToDesiredTypeWithFallibleConverter (fetchLedgerAccount context)
        let! balances = PreLedgerBalance.fetchBetween context accountId beginDate endDate
        let! accounts = Account.fetchAll context false
        let accountById = accounts |> List.map (fun a -> Account.accountId a, a) |> Map.ofList
        return
            balances
            |> List.map (fun b -> viewOf (accountById |> Map.find (PreLedgerBalance.ledgerAccountId b)) b)
            |> List.sortBy (fun v -> v.accountCode, v.preLedgerBalance |> PreLedgerBalance.balanceDate)
    }
