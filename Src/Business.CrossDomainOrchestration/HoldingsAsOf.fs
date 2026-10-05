module Business.CrossDomainOrchestration.HoldingsAsOf

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent

type HoldingsAsOfLine = {
    securityName: string
    ticker: string option
    dimensionValueNames: Map<Dimension, string>
    basisMethod: BasisMethod option
    quantity: Quantity.Quantity
    price: Price.Price
    marketValue: Money.Money
    reportedCostBasis: Money.Money option
}

type HoldingsAsOfAccount = {
    investmentAccountName: string
    institution: string
    accountGroup: string
    taxTreatment: TaxTreatment
    ownerNames: string list
    ledgerAccountCodeAndName: (string * string) option
    snapshotDate: LocalDate
    provenance: Provenance
    contributionBasis: Money.Money option
    lines: HoldingsAsOfLine list
}

let private ownerSeparator = "\u001f"

let private dimensionAlias dimension = $"dv_{dimension |> Dimension.securityColumn}"

type private RawRow = {
    accountName: string
    institution: string
    accountGroup: string
    taxTreatment: string
    ownerNames: string option
    ledgerCode: string option
    ledgerName: string option
    snapshotDate: LocalDate
    provenance: string
    contributionBasis: decimal option
    securityName: string option
    ticker: string option
    dimensionValueNames: string option list
    basisMethod: string option
    quantity: decimal option
    price: decimal option
    marketValue: decimal option
    reportedCostBasis: decimal option
}

let private mapRawForDbRead (row: RowReader) : RawRow =
    { accountName = row |> RowReader.getString "account_name"
      institution = row |> RowReader.getString "institution"
      accountGroup = row |> RowReader.getString "account_group"
      taxTreatment = row |> RowReader.getString "tax_treatment"
      ownerNames = row |> RowReader.getStringOption "owner_names"
      ledgerCode = row |> RowReader.getStringOption "ledger_code"
      ledgerName = row |> RowReader.getStringOption "ledger_name"
      snapshotDate = row |> RowReader.getDate "snapshot_date"
      provenance = row |> RowReader.getString "provenance"
      contributionBasis = row |> RowReader.getNumericOption "contribution_basis"
      securityName = row |> RowReader.getStringOption "security_name"
      ticker = row |> RowReader.getStringOption "ticker"
      dimensionValueNames = Dimension.all |> List.map (fun d -> row |> RowReader.getStringOption (dimensionAlias d))
      basisMethod = row |> RowReader.getStringOption "basis_method"
      quantity = row |> RowReader.getNumericOption "quantity"
      price = row |> RowReader.getNumericOption "price"
      marketValue = row |> RowReader.getNumericOption "market_value"
      reportedCostBasis = row |> RowReader.getNumericOption "reported_cost_basis" }

// A snapshot with no lines comes back as one row whose line columns are all null.
let private reconstituteLine (raw: RawRow) : Result<HoldingsAsOfLine option, IAppError> =
    match raw.securityName, raw.quantity, raw.price, raw.marketValue with
    | Some securityName, Some quantityRaw, Some priceRaw, Some marketValueRaw ->
        result {
            let! quantity = quantityRaw |> Quantity.fromDecimal
            let! price = priceRaw |> Price.fromDecimal
            let! marketValue = marketValueRaw |> Money.fromDecimal
            let! costBasis = raw.reportedCostBasis |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
            let! basisMethod = raw.basisMethod |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
            do!
                AccountSnapshotLine.confirmFigures
                    raw.accountName raw.snapshotDate securityName quantity price marketValue costBasis
            return
                Some
                    { securityName = securityName
                      ticker = raw.ticker
                      dimensionValueNames =
                        List.zip Dimension.all raw.dimensionValueNames
                        |> List.choose (fun (d, name) -> name |> Option.map (fun n -> d, n))
                        |> Map.ofList
                      basisMethod = basisMethod
                      quantity = quantity
                      price = price
                      marketValue = marketValue
                      reportedCostBasis = costBasis }
        }
    | _ -> Ok None

let private reconstitute (raw: RawRow) : Result<RawRow * HoldingsAsOfLine option, IAppError> =
    reconstituteLine raw |> Result.map (fun line -> raw, line)

let private buildAccount (raw: RawRow) (lines: HoldingsAsOfLine list) : Result<HoldingsAsOfAccount, IAppError> =
    result {
        let! taxTreatment = raw.taxTreatment |> TaxTreatment.fromString
        let! provenance = raw.provenance |> Provenance.fromString
        let! contributionBasis = raw.contributionBasis |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        return
            { investmentAccountName = raw.accountName
              institution = raw.institution
              accountGroup = raw.accountGroup
              taxTreatment = taxTreatment
              ownerNames =
                raw.ownerNames
                |> Option.map (fun joined -> joined.Split(ownerSeparator) |> Array.toList |> List.sort)
                |> Option.defaultValue []
              ledgerAccountCodeAndName =
                match raw.ledgerCode, raw.ledgerName with
                | Some code, Some name -> Some(code, name)
                | _ -> None
              snapshotDate = raw.snapshotDate
              provenance = provenance
              contributionBasis = contributionBasis
              lines = lines |> List.sortBy (fun l -> l.securityName) }
    }

/// For every Investment Account active on the date with a snapshot on or before it, that account's latest such
/// snapshot. Accounts ordered by name, lines by Security name.
let fetchHoldingsAsOf (context: Context.Context) (asOf: LocalDate) : Result<HoldingsAsOfAccount list, IAppError> =
    let dimensionColumns =
        Dimension.all |> List.map (fun d -> $"{dimensionAlias d}.value_name as {dimensionAlias d}") |> String.concat ", "
    let dimensionJoins =
        Dimension.all
        |> List.map (fun d ->
            $"left join positions.dimension_value {dimensionAlias d} on {dimensionAlias d}.unique_id = sec.{Dimension.securityColumn d}")
        |> String.concat Environment.NewLine
    let queryStatement =
        $"""
        with latest as (
            select distinct on (snap.investment_account_id)
                snap.unique_id, snap.investment_account_id, snap.snapshot_date, snap.provenance, snap.contribution_basis
            from positions.account_snapshot snap
            join positions.investment_account sia on sia.unique_id = snap.investment_account_id
            where snap.snapshot_date <= @as_of
                and sia.active_begin <= @as_of
                and (sia.active_end is null or sia.active_end >= @as_of)
            order by snap.investment_account_id, snap.snapshot_date desc)
        select
            ia.account_name, ia.institution, ia.account_group, ia.tax_treatment,
            (select string_agg(per.person_name, @owner_separator)
             from positions.investment_account_owner iao
             join general.person per on per.unique_id = iao.person_id
             where iao.investment_account_id = ia.unique_id) as owner_names,
            la.code as ledger_code, la.account_name as ledger_name,
            l.snapshot_date, l.provenance, l.contribution_basis,
            sec.security_name, sec.ticker, {dimensionColumns},
            hol.basis_method, snapl.quantity, snapl.price, snapl.market_value, snapl.reported_cost_basis
        from latest l
        join positions.investment_account ia on ia.unique_id = l.investment_account_id
        left join ledger.account la on la.unique_id = ia.ledger_account_id
        left join positions.account_snapshot_line snapl on snapl.account_snapshot_id = l.unique_id
        left join positions.holding hol on hol.unique_id = snapl.holding_id
        left join positions.security sec on sec.unique_id = hol.security_id
        {dimensionJoins}"""
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
        let! accounts =
            rows
            |> List.groupBy (fun (raw, _) -> raw.accountName)
            |> List.map (fun (_, accountRows) ->
                buildAccount (accountRows |> List.head |> fst) (accountRows |> List.choose snd))
            |> convertListOfResultsToResultsList
        return accounts |> List.sortBy (fun a -> a.investmentAccountName)
    }
