module Business.CrossDomainOrchestration.AccountSnapshotOrchestration

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
open Business.CrossDomainOrchestration.InvestmentOrchestration

type SnapshotLineInput = {
    securityName: SecurityName
    quantity: Quantity.Quantity
    price: Price.Price
    marketValue: Money.Money
    reportedCostBasis: Money.Money option
}

type SnapshotInput = {
    investmentAccountName: InvestmentAccountName
    snapshotDate: LocalDate
    provenance: Provenance
    contributionBasis: ContributionBasis option
    lines: SnapshotLineInput list
}

type SnapshotLineView = {
    line: AccountSnapshotLine.AccountSnapshotLine
    securityName: string
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

let private confirmNoRepeatedSnapshot (inputs: SnapshotInput list) : Result<unit, IAppError> =
    inputs
    |> List.countBy (fun i -> i.investmentAccountName |> InvestmentAccountName.value, i.snapshotDate)
    |> List.tryFind (fun (_, count) -> count > 1)
    |> function
        | Some((accountName, date), _) -> error (PositionsSnapshotRepeatedInRequest(accountName, date))
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
    (accountName: string)
    (snapshotDate: LocalDate)
    (lines: SnapshotLineInput list)
    : Result<unit, IAppError> =
    lines
    |> List.countBy (fun l -> l.securityName |> SecurityName.value)
    |> List.tryFind (fun (_, count) -> count > 1)
    |> function
        | Some(securityName, _) -> error (PositionsSnapshotSecurityRepeated(accountName, snapshotDate, securityName))
        | None -> Ok()

// The Holding must already exist; a Security the account doesn't hold is something for the operator to look at.
let private buildLine
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (snapshotDate: LocalDate)
    (accountSnapshotId: AccountSnapshotId)
    (input: SnapshotLineInput)
    : Result<AccountSnapshotLine.AccountSnapshotLine, IAppError> =
    let accountName = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
    let securityName = input.securityName |> SecurityName.value
    result {
        let! security = fetchSecurityByName context input.securityName
        let! holding =
            Holding.fetchByInvestmentAccountAndSecurity
                context
                (account |> InvestmentAccount.investmentAccountId)
                (security |> Security.securityId)
        let! holding =
            match holding with
            | Some h -> Ok h
            | None -> error (PositionsSnapshotSecurityNotHeld(accountName, securityName))
        do!
            AccountSnapshotLine.checkFigures
                accountName
                snapshotDate
                securityName
                input.quantity
                input.price
                input.marketValue
                input.reportedCostBasis
        return
            AccountSnapshotLine.create
                (AccountSnapshotLineId.create ())
                accountSnapshotId
                (holding |> Holding.holdingId)
                input.quantity
                input.price
                input.marketValue
                input.reportedCostBasis
    }

let private viewSnapshot
    (context: Context.Context)
    (accountName: string)
    (header: AccountSnapshotHeader.AccountSnapshotHeader)
    : Result<SnapshotView, IAppError> =
    result {
        let! lines = header |> AccountSnapshotHeader.accountSnapshotId |> AccountSnapshotLine.fetchByAccountSnapshot context
        let! lineViews =
            lines
            |> List.map (fun line ->
                line
                |> AccountSnapshotLine.holdingId
                |> Holding.fetchById context
                |> Result.bind (Holding.securityId >> Security.fetchById context)
                |> Result.map (fun security ->
                    { line = line; securityName = security |> Security.securityName |> SecurityName.value }))
            |> convertListOfResultsToResultsList
        return
            { header = header
              investmentAccountName = accountName
              lines = lineViews |> List.sortBy (fun l -> l.securityName) }
    }

let private recordOne (context: Context.Context) (input: SnapshotInput) : Result<RecordedSnapshot, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let accountName = input.investmentAccountName |> InvestmentAccountName.value
    result {
        let! account = fetchInvestmentAccountByName context input.investmentAccountName
        let accountId = account |> InvestmentAccount.investmentAccountId
        do! confirmSnapshotDate context account input.snapshotDate
        do! confirmContributionBasisAllowed account input.contributionBasis
        do! confirmEachSecurityOnce accountName input.snapshotDate input.lines
        let! existing = AccountSnapshotHeader.fetchByInvestmentAccountAndDate context accountId input.snapshotDate
        let header =
            match existing with
            | Some stored ->
                AccountSnapshotHeader.create
                    (stored |> AccountSnapshotHeader.accountSnapshotId)
                    accountId
                    input.snapshotDate
                    input.provenance
                    input.contributionBasis
                    (stored |> AccountSnapshotHeader.createdAt)
                    instant
            | None ->
                AccountSnapshotHeader.create
                    (AccountSnapshotId.create ())
                    accountId
                    input.snapshotDate
                    input.provenance
                    input.contributionBasis
                    instant
                    instant
        let accountSnapshotId = header |> AccountSnapshotHeader.accountSnapshotId
        let! lines =
            input.lines
            |> List.map (buildLine context account input.snapshotDate accountSnapshotId)
            |> convertListOfResultsToResultsList
        do!
            match existing with
            | Some _ ->
                AccountSnapshotLine.deleteByAccountSnapshot context accountSnapshotId
                |> Result.bind (fun () -> header |> AccountSnapshotHeader.replace context)
            | None -> header |> AccountSnapshotHeader.persist context
        do! lines |> List.map (AccountSnapshotLine.persist context) |> convertListOfResultsToResultsList |> Result.map ignore
        let! view = viewSnapshot context accountName header
        return { snapshot = view; replacedExisting = existing |> Option.isSome }
    }

/// Records every snapshot or, on the first failure, returns its error; the caller's transaction makes that all or none.
let recordSnapshots (context: Context.Context) (inputs: SnapshotInput list) : Result<RecordedSnapshot list, IAppError> =
    result {
        do! confirmNoRepeatedSnapshot inputs
        return! inputs |> List.map (recordOne context) |> convertListOfResultsToResultsList
    }

let private fetchHeader
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (snapshotDate: LocalDate)
    : Result<AccountSnapshotHeader.AccountSnapshotHeader, IAppError> =
    result {
        let! account = fetchInvestmentAccountByName context accountName
        let! header =
            AccountSnapshotHeader.fetchByInvestmentAccountAndDate
                context
                (account |> InvestmentAccount.investmentAccountId)
                snapshotDate
        match header with
        | Some h -> return h
        | None -> return! error (PositionsSnapshotDoesntExist(accountName |> InvestmentAccountName.value, snapshotDate))
    }

/// Deletes the snapshot and its lines, and returns it as it stood before deletion.
let deleteSnapshot
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (snapshotDate: LocalDate)
    : Result<SnapshotView, IAppError> =
    result {
        let! header = fetchHeader context accountName snapshotDate
        let! view = viewSnapshot context (accountName |> InvestmentAccountName.value) header
        let accountSnapshotId = header |> AccountSnapshotHeader.accountSnapshotId
        do! AccountSnapshotLine.deleteByAccountSnapshot context accountSnapshotId
        do! AccountSnapshotHeader.delete context accountSnapshotId
        return view
    }

let fetchSnapshot
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (snapshotDate: LocalDate)
    : Result<SnapshotView, IAppError> =
    result {
        let! header = fetchHeader context accountName snapshotDate
        return! viewSnapshot context (accountName |> InvestmentAccountName.value) header
    }

/// Each snapshot date in the range, both ends included, with its provenance, in date order.
let listSnapshotDates
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<(LocalDate * Provenance) list, IAppError> =
    result {
        let! account = fetchInvestmentAccountByName context accountName
        let! headers =
            AccountSnapshotHeader.fetchByInvestmentAccountBetween
                context
                (account |> InvestmentAccount.investmentAccountId)
                beginDate
                endDate
        return headers |> List.map (fun h -> h |> AccountSnapshotHeader.snapshotDate, h |> AccountSnapshotHeader.provenance)
    }
