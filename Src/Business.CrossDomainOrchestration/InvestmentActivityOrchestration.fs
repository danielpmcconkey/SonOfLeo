module Business.CrossDomainOrchestration.InvestmentActivityOrchestration

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// One activity to record: its date, kind, description, source, Security, quantity, price and amount.
type ActivityInput =
    LocalDate * ActivityKind * ActivityDescription * ActivitySource option * SecurityId option * Quantity.Quantity option *
    Price.Price option * Money.Money

/// One account's activity to record: the account, the range's begin and end dates, and every activity dated in the
/// range, in the order the institution reported them.
type ActivityRange = InvestmentAccountId * LocalDate * LocalDate * ActivityInput list

/// An activity as stored, with the name of the Security it names, if any.
type ActivityView = {
    activity: InvestmentActivity.InvestmentActivity
    investmentAccountName: string
    securityName: string option
}

/// What recording one account's range did: how many activities dated in it were removed, and how many recorded.
type RecordedRange = {
    investmentAccountName: string
    beginDate: LocalDate
    endDate: LocalDate
    removed: int
    recorded: int
}

let private accountNameOf account = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value

let private securityNamesById (context: Context.Context) : Result<Map<SecurityId, string>, IAppError> =
    Security.fetchAll context
    |> Result.map (List.map (fun s -> Security.securityId s, s |> Security.securityName |> SecurityName.value) >> Map.ofList)

// The Holding must already exist, as for snapshot lines; a Security the account doesn't hold is for the operator to
// look at.
let private buildActivity
    (account: InvestmentAccount.InvestmentAccount)
    (holdingsBySecurity: Map<SecurityId, Holding.Holding>)
    (securityNames: Map<SecurityId, string>)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (instant: Instant)
    (ordinal: int)
    (input: ActivityInput)
    : Result<InvestmentActivity.InvestmentActivity, IAppError> =
    let activityDate, kind, description, source, securityId, quantity, price, amount = input
    let accountName = accountNameOf account
    result {
        do! InvestmentActivity.confirmDatedInRange accountName beginDate endDate activityDate
        do!
            if account |> InvestmentAccount.activityPeriod |> ActivityPeriod.isActive activityDate then Ok()
            else error (PositionsActivityDateOutsideActivePeriod(accountName, activityDate))
        do! InvestmentActivity.confirmShape accountName activityDate kind (securityId |> Option.isSome) quantity price amount
        let! holdingId =
            match securityId with
            | None -> Ok None
            | Some id ->
                match holdingsBySecurity |> Map.tryFind id with
                | Some holding -> Ok(Some(holding |> Holding.holdingId))
                | None ->
                    let securityName = securityNames |> Map.tryFind id |> Option.defaultValue $"{id |> SecurityId.value}"
                    error (PositionsActivitySecurityNotHeld(accountName, securityName))
        return
            InvestmentActivity.create
                (InvestmentActivityId.create ())
                (account |> InvestmentAccount.investmentAccountId)
                activityDate
                ordinal
                kind
                description
                source
                holdingId
                quantity
                price
                amount
                instant
                instant
    }

// Ordinals number an account's activities of one date in the order supplied, so identical ones stay two and come back
// in that order.
let private withOrdinals (inputs: ActivityInput list) : (int * ActivityInput) list =
    inputs
    |> List.mapFold
        (fun (seen: Map<LocalDate, int>) (input: ActivityInput) ->
            let activityDate, _, _, _, _, _, _, _ = input
            let ordinal = seen |> Map.tryFind activityDate |> Option.defaultValue 0
            (ordinal, input), seen |> Map.add activityDate (ordinal + 1))
        Map.empty
    |> fst

let private recordRange
    (context: Context.Context)
    (securityNames: Map<SecurityId, string>)
    (account: InvestmentAccount.InvestmentAccount, (_, beginDate, endDate, inputs): ActivityRange)
    : Result<RecordedRange, IAppError> =
    let accountId = account |> InvestmentAccount.investmentAccountId
    let instant = context |> Context.getInitiationInstant
    result {
        let! holdings = accountId |> Holding.fetchByInvestmentAccount context
        let holdingsBySecurity = holdings |> List.map (fun h -> Holding.securityId h, h) |> Map.ofList
        let! activities =
            inputs
            |> withOrdinals
            |> List.map (fun (ordinal, input) ->
                buildActivity account holdingsBySecurity securityNames beginDate endDate instant ordinal input)
            |> convertListOfResultsToResultsList
        let! existing = InvestmentActivity.fetchByInvestmentAccountBetween context accountId beginDate endDate
        do! InvestmentActivity.deleteByInvestmentAccountBetween context accountId beginDate endDate
        do!
            activities
            |> List.map (InvestmentActivity.persist context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return
            { investmentAccountName = accountNameOf account
              beginDate = beginDate
              endDate = endDate
              removed = existing |> List.length
              recorded = activities |> List.length }
    }

/// Replaces each account's activity dated in its range with the activities given, or, on the first failure, returns
/// its error; the caller's transaction makes that all or none.
let recordActivity (context: Context.Context) (ranges: ActivityRange list) : Result<RecordedRange list, IAppError> =
    let currentDate = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
    result {
        do! if ranges |> List.isEmpty then error PositionsActivityRangeListIsEmpty else Ok()
        let! accounts =
            ranges
            |> List.map (fun (accountId, _, _, _) -> accountId |> InvestmentAccount.fetchById context)
            |> convertListOfResultsToResultsList
        let accountsAndRanges = List.zip accounts ranges
        do!
            accountsAndRanges
            |> List.map (fun (account, (_, beginDate, endDate, _)) ->
                InvestmentActivity.confirmRange (accountNameOf account) beginDate endDate currentDate)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do!
            accountsAndRanges
            |> List.map (fun (account, (accountId, beginDate, endDate, _)) ->
                accountId, accountNameOf account, beginDate, endDate)
            |> InvestmentActivity.confirmRangesDontOverlap
        let! securityNames = securityNamesById context
        return! accountsAndRanges |> List.map (recordRange context securityNames) |> convertListOfResultsToResultsList
    }

/// The account's activities dated in the range, both ends included, in date order and, within a date, in the order
/// supplied.
let listActivity
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<ActivityView list, IAppError> =
    result {
        do! if endDate < beginDate then error (PositionsActivityListEndBeforeBegin(beginDate, endDate)) else Ok()
        let! account = investmentAccountId |> InvestmentAccount.fetchById context
        let! activities = InvestmentActivity.fetchByInvestmentAccountBetween context investmentAccountId beginDate endDate
        let! holdings = investmentAccountId |> Holding.fetchByInvestmentAccount context
        let! securityNames = securityNamesById context
        let securityNameOfHolding =
            holdings
            |> List.map (fun h -> Holding.holdingId h, securityNames |> Map.tryFind (Holding.securityId h))
            |> Map.ofList
        return
            activities
            |> List.map (fun activity ->
                { activity = activity
                  investmentAccountName = accountNameOf account
                  securityName =
                    activity
                    |> InvestmentActivity.holdingId
                    |> Option.bind (fun id -> securityNameOfHolding |> Map.tryFind id |> Option.flatten) })
    }
