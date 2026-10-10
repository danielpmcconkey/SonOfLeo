module Business.CrossDomainOrchestration.AccountSnapshotOrchestration

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// One lot of a line to record: its acquired date, quantity and reported cost basis.
type SnapshotLot = LocalDate * Quantity.Quantity * Money.Money option

/// One line of a snapshot to record: the Security, quantity, price, market value, reported cost basis and lots, the
/// lots in the order the institution reported them.
type SnapshotLine = SecurityId * Quantity.Quantity * Price.Price * Money.Money * Money.Money option * SnapshotLot list

/// One snapshot to record: the account, the date, provenance, contribution basis and lines.
type Snapshot = InvestmentAccountId * LocalDate * Provenance * ContributionBasis option * SnapshotLine list

/// A line as stored, with its lots in the order supplied.
type SnapshotLineView = {
    line: AccountSnapshotLine.AccountSnapshotLine
    securityName: string
    lots: AccountSnapshotLot.AccountSnapshotLot list
}

/// A snapshot as stored, its lines ordered by Security name.
type SnapshotView = {
    header: AccountSnapshotHeader.AccountSnapshotHeader
    investmentAccountName: string
    lines: SnapshotLineView list
}

type RecordedSnapshot = {
    snapshot: SnapshotView
    replacedExisting: bool
}

let private accountNameOf account = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value

let private confirmNoRepeatedSnapshot (context: Context.Context) (inputs: Snapshot list) : Result<unit, IAppError> =
    inputs
    |> List.countBy (fun (accountId, date, _, _, _) -> accountId, date)
    |> List.tryFind (fun (_, count) -> count > 1)
    |> function
        | Some((accountId, date), _) ->
            accountId
            |> InvestmentAccount.fetchById context
            |> Result.bind (fun account -> error (PositionsSnapshotRepeatedInRequest(accountNameOf account, date)))
        | None -> Ok()

let private confirmSnapshotDate
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (snapshotDate: LocalDate)
    : Result<unit, IAppError> =
    let accountName = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
    let currentDate = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
    if not (account |> InvestmentAccount.activityPeriod |> ActivityPeriod.isActive snapshotDate) then
        error (PositionsSnapshotDateOutsideActivePeriod(accountName, snapshotDate))
    elif snapshotDate > currentDate then
        error (PositionsSnapshotDateLaterThanCurrentDate(accountName, snapshotDate))
    else
        Ok()

let private confirmContributionBasisAllowed
    (account: InvestmentAccount.InvestmentAccount)
    (contributionBasis: ContributionBasis option)
    : Result<unit, IAppError> =
    match contributionBasis, account |> InvestmentAccount.taxTreatment with
    | None, _
    | Some _, TaxTreatment.Roth -> Ok()
    | Some _, other ->
        error (
            PositionsContributionBasisNotAllowed(
                account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value,
                other |> TaxTreatment.toString
            )
        )

let private confirmEachSecurityOnce
    (context: Context.Context)
    (accountName: string)
    (snapshotDate: LocalDate)
    (lines: SnapshotLine list)
    : Result<unit, IAppError> =
    lines
    |> List.countBy (fun (securityId, _, _, _, _, _) -> securityId)
    |> List.tryFind (fun (_, count) -> count > 1)
    |> function
        | Some(securityId, _) ->
            securityId
            |> Security.fetchById context
            |> Result.bind (fun security ->
                error (
                    PositionsSnapshotSecurityRepeated(
                        accountName,
                        snapshotDate,
                        security |> Security.securityName |> SecurityName.value
                    )
                ))
        | None -> Ok()

// Only a Taxable account's lines carry lots: outside one, when a unit was acquired means nothing.
let private confirmLotsAllowed
    (account: InvestmentAccount.InvestmentAccount)
    (securityName: string)
    (lots: SnapshotLot list)
    : Result<unit, IAppError> =
    match lots, account |> InvestmentAccount.taxTreatment with
    | [], _
    | _, TaxTreatment.Taxable -> Ok()
    | _ -> error (PositionsLotsNotAllowed(accountNameOf account, securityName))

// The Holding must already exist; a Security the account doesn't hold is something for the operator to look at.
let private buildLine
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (snapshotDate: LocalDate)
    (accountSnapshotId: AccountSnapshotId)
    (input: SnapshotLine)
    : Result<AccountSnapshotLine.AccountSnapshotLine * AccountSnapshotLot.AccountSnapshotLot list, IAppError> =
    let securityId, quantity, price, marketValue, reportedCostBasis, lotInputs = input
    let accountName = accountNameOf account
    result {
        let! security = securityId |> Security.fetchById context
        let securityName = security |> Security.securityName |> SecurityName.value
        let! holding =
            Holding.fetchByInvestmentAccountAndSecurity context (account |> InvestmentAccount.investmentAccountId) securityId
        let! holding =
            match holding with
            | Some h -> Ok h
            | None -> error (PositionsSnapshotSecurityNotHeld(accountName, securityName))
        do!
            AccountSnapshotLine.confirmFigures
                accountName snapshotDate securityName quantity price marketValue reportedCostBasis
        do! confirmLotsAllowed account securityName lotInputs
        do! AccountSnapshotLot.confirmLots accountName snapshotDate securityName quantity lotInputs
        let accountSnapshotLineId = AccountSnapshotLineId.create ()
        let line =
            AccountSnapshotLine.create
                accountSnapshotLineId
                accountSnapshotId
                (holding |> Holding.holdingId)
                quantity
                price
                marketValue
                reportedCostBasis
        let lots =
            lotInputs
            |> List.mapi (fun ordinal (acquiredDate, lotQuantity, lotCostBasis) ->
                AccountSnapshotLot.create
                    (AccountSnapshotLotId.create ()) accountSnapshotLineId ordinal acquiredDate lotQuantity lotCostBasis)
        return line, lots
    }

let private viewSnapshot
    (context: Context.Context)
    (accountName: string)
    (header: AccountSnapshotHeader.AccountSnapshotHeader)
    : Result<SnapshotView, IAppError> =
    result {
        let accountSnapshotId = header |> AccountSnapshotHeader.accountSnapshotId
        let! lines = accountSnapshotId |> AccountSnapshotLine.fetchByAccountSnapshot context
        let! lots = accountSnapshotId |> AccountSnapshotLot.fetchByAccountSnapshot context
        let lotsOf line =
            lots |> List.filter (fun l -> AccountSnapshotLot.accountSnapshotLineId l = AccountSnapshotLine.accountSnapshotLineId line)
        let! lineViews =
            lines
            |> List.map (fun line ->
                line
                |> AccountSnapshotLine.holdingId
                |> Holding.fetchById context
                |> Result.bind (Holding.securityId >> Security.fetchById context)
                |> Result.map (fun security ->
                    { line = line
                      securityName = security |> Security.securityName |> SecurityName.value
                      lots = lotsOf line }))
            |> convertListOfResultsToResultsList
        return
            { header = header
              investmentAccountName = accountName
              lines = lineViews |> List.sortBy (fun l -> l.securityName) }
    }

let private recordOne (context: Context.Context) (input: Snapshot) : Result<RecordedSnapshot, IAppError> =
    let accountId, snapshotDate, provenance, contributionBasis, lineInputs = input
    let instant = context |> Context.getInitiationInstant
    result {
        let! account = accountId |> InvestmentAccount.fetchById context
        let accountName = accountNameOf account
        do! confirmSnapshotDate context account snapshotDate
        do! confirmContributionBasisAllowed account contributionBasis
        do! confirmEachSecurityOnce context accountName snapshotDate lineInputs
        let! existing = AccountSnapshotHeader.fetchByInvestmentAccountAndDate context accountId snapshotDate
        let accountSnapshotId =
            existing
            |> Option.map AccountSnapshotHeader.accountSnapshotId
            |> Option.defaultWith AccountSnapshotId.create
        let! linesAndLots =
            lineInputs
            |> List.map (buildLine context account snapshotDate accountSnapshotId)
            |> convertListOfResultsToResultsList
        let! header =
            match existing with
            | Some _ ->
                AccountSnapshotLine.deleteByAccountSnapshot context accountSnapshotId
                |> Result.bind (fun () ->
                    AccountSnapshotHeader.update
                        context
                        { accountSnapshotIdToUpdate = accountSnapshotId
                          provenanceUpdate = SetTo provenance
                          contributionBasisUpdate = SetTo contributionBasis })
            | None ->
                let header =
                    AccountSnapshotHeader.create
                        accountSnapshotId accountId snapshotDate provenance contributionBasis instant instant
                header |> AccountSnapshotHeader.persist context |> Result.map (fun () -> header)
        do!
            linesAndLots
            |> List.map (fun (line, lots) ->
                line
                |> AccountSnapshotLine.persist context
                |> Result.bind (fun () ->
                    lots |> List.map (AccountSnapshotLot.persist context) |> convertListOfResultsToResultsList)
                |> Result.map ignore)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let! view = viewSnapshot context accountName header
        return { snapshot = view; replacedExisting = existing |> Option.isSome }
    }

/// Records every snapshot or, on the first failure, returns its error; the caller's transaction makes that all or none.
let recordSnapshots (context: Context.Context) (inputs: Snapshot list) : Result<RecordedSnapshot list, IAppError> =
    result {
        do! if inputs |> List.isEmpty then error PositionsSnapshotListIsEmpty else Ok()
        do! confirmNoRepeatedSnapshot context inputs
        return! inputs |> List.map (recordOne context) |> convertListOfResultsToResultsList
    }

let private fetchHeader
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (snapshotDate: LocalDate)
    : Result<string * AccountSnapshotHeader.AccountSnapshotHeader, IAppError> =
    result {
        let! account = investmentAccountId |> InvestmentAccount.fetchById context
        let accountName = accountNameOf account
        let! header = AccountSnapshotHeader.fetchByInvestmentAccountAndDate context investmentAccountId snapshotDate
        match header with
        | Some h -> return accountName, h
        | None -> return! error (PositionsSnapshotDoesntExist(accountName, snapshotDate))
    }

/// Deletes the snapshot and its lines with their lots, and returns it as it stood before deletion.
let deleteSnapshot
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (snapshotDate: LocalDate)
    : Result<SnapshotView, IAppError> =
    result {
        let! accountName, header = fetchHeader context investmentAccountId snapshotDate
        let! view = viewSnapshot context accountName header
        let accountSnapshotId = header |> AccountSnapshotHeader.accountSnapshotId
        do! AccountSnapshotLine.deleteByAccountSnapshot context accountSnapshotId
        do! AccountSnapshotHeader.delete context accountSnapshotId
        return view
    }

let fetchSnapshot
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (snapshotDate: LocalDate)
    : Result<SnapshotView, IAppError> =
    result {
        let! accountName, header = fetchHeader context investmentAccountId snapshotDate
        return! viewSnapshot context accountName header
    }

/// Each snapshot date in the range, both ends included, with its provenance, in date order.
let listSnapshotDates
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<(LocalDate * Provenance) list, IAppError> =
    result {
        do! if endDate < beginDate then error (PositionsSnapshotDatesEndBeforeBegin(beginDate, endDate)) else Ok()
        let! _ = investmentAccountId |> InvestmentAccount.fetchById context
        let! headers = AccountSnapshotHeader.fetchByInvestmentAccountBetween context investmentAccountId beginDate endDate
        return headers |> List.map (fun h -> h |> AccountSnapshotHeader.snapshotDate, h |> AccountSnapshotHeader.provenance)
    }
