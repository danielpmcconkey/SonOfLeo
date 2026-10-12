module Business.CrossDomainOrchestration.HoldingsAsOf

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.General.PersonComponent
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent

/// An account's holdings as of a date: its latest snapshot on or before the date, with the account's own fields.
type HoldingsAsOfAccount = {
    investmentAccountId: InvestmentAccountId
    institution: Institution
    accountGroup: AccountGroup
    taxTreatment: TaxTreatment
    ownerIds: Set<PersonId>
    ledgerAccountId: AccountId option
    snapshotDate: LocalDate
    provenance: Provenance
    contributionBasis: ContributionBasis option
    lines: HoldingsAsOfLine list
}

let private ownerSeparator = ","

/// The names are read only to label a line whose figures disagree; nothing returned carries them.
type private RawRow = {
    lineId: Guid option
    investmentAccountId: Guid
    accountName: string
    institution: string
    accountGroup: string
    taxTreatment: string
    ownerIds: string option
    ledgerAccountId: Guid option
    snapshotDate: LocalDate
    provenance: string
    contributionBasis: decimal option
    securityId: Guid option
    securityName: string option
    basisMethod: string option
    quantity: decimal option
    price: decimal option
    marketValue: decimal option
    reportedCostBasis: decimal option
}

let private mapRawForDbRead (row: RowReader) : RawRow =
    { lineId = row |> RowReader.getUuidOption "line_id"
      investmentAccountId = row |> RowReader.getUuid "investment_account_id"
      accountName = row |> RowReader.getString "account_name"
      institution = row |> RowReader.getString "institution"
      accountGroup = row |> RowReader.getString "account_group"
      taxTreatment = row |> RowReader.getString "tax_treatment"
      ownerIds = row |> RowReader.getStringOption "owner_ids"
      ledgerAccountId = row |> RowReader.getUuidOption "ledger_account_id"
      snapshotDate = row |> RowReader.getDate "snapshot_date"
      provenance = row |> RowReader.getString "provenance"
      contributionBasis = row |> RowReader.getNumericOption "contribution_basis"
      securityId = row |> RowReader.getUuidOption "security_id"
      securityName = row |> RowReader.getStringOption "security_name"
      basisMethod = row |> RowReader.getStringOption "basis_method"
      quantity = row |> RowReader.getNumericOption "quantity"
      price = row |> RowReader.getNumericOption "price"
      marketValue = row |> RowReader.getNumericOption "market_value"
      reportedCostBasis = row |> RowReader.getNumericOption "reported_cost_basis" }

/// A line as read, waiting for its lots, which are read separately.
type private LineWithoutLots = (LocalDate * Quantity.Quantity * Money.Money option) list -> HoldingsAsOfLine

/// An account as read, waiting for its lines.
type private AccountWithoutLines = HoldingsAsOfLine list -> HoldingsAsOfAccount

// A snapshot with no lines comes back as one row whose line columns are all null.
let private reconstituteLine (raw: RawRow) : Result<(Guid * LineWithoutLots) option, IAppError> =
    match raw.lineId, raw.securityId, raw.securityName, raw.quantity, raw.price, raw.marketValue with
    | Some lineId, Some securityId, Some securityName, Some quantityRaw, Some priceRaw, Some marketValueRaw ->
        result {
            let! quantity = quantityRaw |> Quantity.fromDecimal
            let! price = priceRaw |> Price.fromDecimal
            let! marketValue = marketValueRaw |> Money.fromDecimal
            let! costBasis = raw.reportedCostBasis |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
            let! basisMethod = raw.basisMethod |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
            do!
                AccountSnapshotLine.confirmFigures
                    raw.accountName raw.snapshotDate securityName quantity price marketValue costBasis
            let line lots : HoldingsAsOfLine =
                { securityId = securityId |> SecurityId.fromGuid
                  basisMethod = basisMethod
                  quantity = quantity
                  price = price
                  marketValue = marketValue
                  reportedCostBasis = costBasis
                  lots = lots }
            return Some(lineId, line)
        }
    | _ -> Ok None

let private reconstituteAccount (raw: RawRow) : Result<AccountWithoutLines, IAppError> =
    result {
        let! institution = raw.institution |> Institution.create
        let! accountGroup = raw.accountGroup |> AccountGroup.create
        let! taxTreatment = raw.taxTreatment |> TaxTreatment.fromString
        let! provenance = raw.provenance |> Provenance.fromString
        let! contributionBasis =
            raw.contributionBasis
            |> convertOptionToDesiredTypeWithFallibleConverter (Money.fromDecimal >> Result.bind ContributionBasis.create)
        let ownerIds =
            raw.ownerIds
            |> Option.map (fun joined -> joined.Split(ownerSeparator) |> Array.map (Guid.Parse >> PersonId.fromGuid) |> Set.ofArray)
            |> Option.defaultValue Set.empty
        return fun lines ->
            { investmentAccountId = raw.investmentAccountId |> InvestmentAccountId.fromGuid
              institution = institution
              accountGroup = accountGroup
              taxTreatment = taxTreatment
              ownerIds = ownerIds
              ledgerAccountId = raw.ledgerAccountId |> Option.map AccountId.fromGuid
              snapshotDate = raw.snapshotDate
              provenance = provenance
              contributionBasis = contributionBasis
              lines = lines }
    }

let private reconstitute (raw: RawRow) : Result<InvestmentAccountId * AccountWithoutLines * (Guid * LineWithoutLots) option, IAppError> =
    result {
        let! account = reconstituteAccount raw
        let! line = reconstituteLine raw
        return raw.investmentAccountId |> InvestmentAccountId.fromGuid, account, line
    }

/// For every Investment Account active on the date with a snapshot on or before it, that account's latest such
/// snapshot, each line with its lots in the order supplied. Accounts ordered by name, lines by Security name; the
/// "C" collation compares the names as the ordinal string sort it replaced did, whatever the database's collation.
let fetchHoldingsAsOf (context: Context.Context) (asOf: LocalDate) : Result<HoldingsAsOfAccount list, IAppError> =
    let queryStatement =
        $"""
        with latest as (
            select distinct on (snap.investment_account_id)
                snap.unique_id, snap.investment_account_id, snap.snapshot_date, snap.provenance, snap.contribution_basis
            from positions.account_snapshot snap
            where snap.snapshot_date <= @as_of
            order by snap.investment_account_id, snap.snapshot_date desc)
        select
            ia.unique_id as investment_account_id, ia.account_name, ia.institution, ia.account_group, ia.tax_treatment,
            (select string_agg(iao.person_id::text, @owner_separator)
             from positions.investment_account_owner iao
             where iao.investment_account_id = ia.unique_id) as owner_ids,
            ia.ledger_account_id,
            l.snapshot_date, l.provenance, l.contribution_basis,
            snapl.unique_id as line_id, sec.unique_id as security_id, sec.security_name,
            hol.basis_method, snapl.quantity, snapl.price, snapl.market_value, snapl.reported_cost_basis
        from latest l
        join positions.investment_account ia on ia.unique_id = l.investment_account_id
        left join positions.account_snapshot_line snapl on snapl.account_snapshot_id = l.unique_id
        left join positions.holding hol on hol.unique_id = snapl.holding_id
        left join positions.security sec on sec.unique_id = hol.security_id
        where ia.active_begin <= @as_of and (ia.active_end is null or ia.active_end >= @as_of)
        order by ia.account_name collate "C", ia.unique_id, sec.security_name collate "C";"""
    let parameters =
        [ { name = "@as_of"; value = DbLocalDate asOf }
          { name = "@owner_separator"; value = CharString ownerSeparator } ]
    result {
        let! rows =
            executeReaderQuery
                (context |> Context.getDatabaseTransaction)
                queryStatement
                parameters
                mapRawForDbRead
                reconstitute
                AnyQuantityIsAcceptable
        let lineIds = rows |> List.choose (fun (_, _, line) -> line |> Option.map (fst >> AccountSnapshotLineId.fromGuid))
        let! lots =
            if lineIds |> List.isEmpty then Ok [] else AccountSnapshotLot.fetchByAccountSnapshotLineIdList context lineIds
        let lotsByLine =
            lots
            |> List.groupBy (AccountSnapshotLot.accountSnapshotLineId >> AccountSnapshotLineId.value)
            |> Map.ofList
            |> Map.map (fun _ lineLots ->
                lineLots
                |> List.map (fun lot ->
                    lot |> AccountSnapshotLot.acquiredDate,
                    lot |> AccountSnapshotLot.quantity,
                    lot |> AccountSnapshotLot.reportedCostBasis))
        // the query's order survives grouping, which keeps each account where its first row was
        return
            rows
            |> List.groupBy (fun (accountId, _, _) -> accountId)
            |> List.map (fun (_, accountRows) ->
                let _, account, _ = accountRows |> List.head
                accountRows
                |> List.choose (fun (_, _, line) -> line)
                |> List.map (fun (lineId, line) -> lotsByLine |> Map.tryFind lineId |> Option.defaultValue [] |> line)
                |> account)
    }
