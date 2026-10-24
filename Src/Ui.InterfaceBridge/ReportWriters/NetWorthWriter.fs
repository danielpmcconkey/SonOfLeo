module Ui.InterfaceBridge.ReportWriters.NetWorthWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.File
open App.Utility.Calendar
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.NetWorth
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.BoundaryConverters.ReportConverters
open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents
open Ui.InterfaceBridge.ReportVisualizationAssets.BaseCssDeclarations
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportHeader
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportBody
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportFooter

let specificCss = [
    {
        ordinal = 10
        declarator = ".block"
        definition = "margin-top: 1.75rem;" }
    {
        ordinal = 20
        declarator = ".block:first-child"
        definition = "margin-top: 0;" }
    {
        ordinal = 30
        declarator = ".block h2"
        definition = """
    font-size: 1rem;
    font-weight: 600;
    margin: 0 0 0.4rem;
    padding-bottom: 0.3rem;
    border-bottom: 1.5px solid var(--ink);""" }
    {
        ordinal = 40
        declarator = "table"
        definition = "width: 100%; border-collapse: collapse; font-size: 0.9rem;" }
    {
        ordinal = 50
        declarator = "th, td"
        definition = "text-align: left; padding: 0.25rem 0.5rem; border-bottom: 1px solid var(--rule);" }
    {
        ordinal = 60
        declarator = "th"
        definition = """
    color: var(--ink-light);
    font-weight: 500;
    letter-spacing: 0.02em;
    text-transform: uppercase;
    font-size: 0.72rem;""" }
    {
        ordinal = 70
        declarator = "td.num, th.num"
        definition = "text-align: right; font-variant-numeric: tabular-nums;" }
    {
        ordinal = 80
        declarator = "tr.total td"
        definition = "font-weight: 600; border-top: 1.5px solid var(--ink);" }
    ]

let private encode (s: string) = System.Net.WebUtility.HtmlEncode s

let private dateString (d: LocalDate) = d |> localDateToString "yyyy-MM-dd"

let private element ordinal elementType identifierType contents : DomElement =
    { ordinal = ordinal; elementType = elementType; identifierType = identifierType; contents = contents }

let private headCell ordinal (text: string) isNumber =
    element ordinal (TableHeadCell(encode text)) (if isNumber then Class "num" else NoIdentifier) []

let private textCell ordinal (text: string) = element ordinal (TableDataCell(encode text)) NoIdentifier []

let private moneyCell ordinal (m: Money.Money) =
    element ordinal (TableDataCell(m |> Money.toAccountingString)) (Class "num") []

let private row ordinal className cells =
    element ordinal TableRow (match className with Some c -> Class c | None -> NoIdentifier) (cells |> List.mapi (fun i c -> c (10 + i)))

let private table ordinal (headings: (string * bool) list) (rows: DomElement list) =
    let headRow = element 1 TableRow NoIdentifier (headings |> List.mapi (fun i (h, isNumber) -> headCell (10 + i) h isNumber))
    element ordinal Table NoIdentifier (headRow :: rows)

let private block ordinal (title: string) contents =
    element ordinal Div (Class "block") (element 1 (H2(encode title)) NoIdentifier [] :: contents)

/// How a component of net worth is named in a report.
let componentLabel (netWorthComponent: NetWorthComponent) =
    match netWorthComponent with
    | CountedLedgerAssets -> "Counted ledger assets"
    | Investments -> "Investments"
    | PropertyValues -> "Property values"
    | Cash -> "Cash"
    | Liabilities -> "Liabilities"
    | Debts -> "Debts"
    | OwnedPropertyMortgages -> "Mortgages of owned properties"

/// A ledger account's code, name and balance.
type private NamedLedgerRow = string * string * Money.Money

let private ledgerTable ordinal (rows: NamedLedgerRow list) =
    table ordinal [ "Code", false; "Name", false; "Balance", true ]
        (rows
         |> List.mapi (fun i (code, name, balance) ->
             row (10 + i) None [ (fun o -> textCell o code); (fun o -> textCell o name); (fun o -> moneyCell o balance) ]))

let private joined (names: string list) = names |> String.concat ", "

/// Each account with its name and its owners' names.
let private investmentsBlock ordinal (accounts: (NetWorthInvestmentAccount * (string * string list)) list) =
    let rows =
        accounts
        |> List.mapi (fun i (a, (accountName, ownerNames)) ->
            row (10 + i) None
                [ (fun o -> textCell o accountName)
                  (fun o -> textCell o (joined ownerNames))
                  (fun o -> textCell o (a.accountGroup |> AccountGroup.value))
                  (fun o -> textCell o (a.taxTreatment |> TaxTreatment.toString))
                  (fun o -> textCell o $"{dateString a.snapshotDate} ({a.provenance |> Provenance.toString})")
                  (fun o -> moneyCell o a.marketValue)
                  (fun o ->
                      match a.contributionBasis with
                      | Some basis -> moneyCell o basis
                      | None -> textCell o "") ])
    block ordinal "Investment accounts, at market value"
        [ table 10
            [ "Account", false; "Owners", false; "Group", false; "Tax treatment", false; "Snapshot", false
              "Market value", true; "Contribution basis", true ]
            rows ]

/// A mortgage Balance Account's name, the date of the Balance its balance came from, and that balance.
type private NamedMortgageBalanceAccount = string * LocalDate * Money.Money

/// Each property with its name, its owners' names, its named mortgage accounts and its named mortgage Balance Accounts.
let private propertiesBlock
    ordinal
    (properties:
        (NetWorthProperty * (string * string list) * NamedLedgerRow list * NamedMortgageBalanceAccount list) list)
    =
    let propertyTable i (p: NetWorthProperty, (propertyName: string, ownerNames: string list), mortgages, mortgageBalanceAccounts) =
        let source =
            match p.valueSource with
            | ValuationDated d -> $"valuation of {dateString d}"
            | PurchaseBasisValue -> "purchase basis"
        let mortgageRows =
            mortgages
            |> List.mapi (fun j (code, name, balance) ->
                row (20 + j) None
                    [ (fun o -> textCell o $"Mortgage {code} · {name}")
                      (fun o -> textCell o "")
                      (fun o -> moneyCell o balance) ])
        let mortgageBalanceAccountRows =
            mortgageBalanceAccounts
            |> List.mapi (fun j (name, balanceDate, balance) ->
                row (50 + j) None
                    [ (fun o -> textCell o $"Mortgage {name}")
                      (fun o -> textCell o $"balance of {dateString balanceDate}")
                      (fun o -> moneyCell o balance) ])
        table (10 + i)
            [ $"{propertyName} ({p.propertyUse |> PropertyUse.toString}), owned by {joined ownerNames}", false
              "Source", false; "Amount", true ]
            ([ row 10 None [ (fun o -> textCell o "Value"); (fun o -> textCell o source); (fun o -> moneyCell o p.value) ] ]
             @ mortgageRows
             @ mortgageBalanceAccountRows
             @ [ row 90 (Some "total") [ (fun o -> textCell o "Equity"); (fun o -> textCell o ""); (fun o -> moneyCell o p.equity) ] ])
    block ordinal "Properties owned on the date" (properties |> List.mapi propertyTable)

/// Each Balance Account listed, with its name and its owners' names.
let private balanceAccountsBlock ordinal (balanceAccounts: (NetWorthBalanceAccount * (string * string list)) list) =
    let rows =
        balanceAccounts
        |> List.mapi (fun i (b, (name, ownerNames)) ->
            row (10 + i) None
                [ (fun o -> textCell o name)
                  (fun o -> textCell o (b.kind |> BalanceAccountKind.toString))
                  (fun o -> textCell o (joined ownerNames))
                  (fun o -> textCell o (dateString b.balanceDate))
                  (fun o -> moneyCell o b.balance) ])
    block ordinal "Cash and debts in Balance Accounts"
        [ table 10 [ "Balance Account", false; "Kind", false; "Owners", false; "Balance date", false; "Balance", true ] rows ]

let private totalsBlock ordinal (netWorth: NetWorth) =
    let line i label amount = row (10 + i) None [ (fun o -> textCell o label); (fun o -> moneyCell o amount) ]
    block ordinal "Totals"
        [ table 10 [ "", false; "Amount", true ]
            [ line 0 "Counted ledger assets" netWorth.totalLedgerAssets
              line 1 "Investments" netWorth.totalInvestments
              line 2 "Property values" netWorth.totalPropertyValues
              line 3 "Cash" netWorth.totalCash
              line 4 "Liabilities" netWorth.totalLiabilities
              line 5 "Debts" netWorth.totalDebts
              line 6 "Mortgages of owned properties" netWorth.totalOwnedPropertyMortgages
              row 20 (Some "total") [ (fun o -> textCell o "Net worth"); (fun o -> moneyCell o netWorth.netWorth) ]
              row 21 (Some "total") [ (fun o -> textCell o "Investable wealth"); (fun o -> moneyCell o netWorth.investableWealth) ] ] ]

let private subtotalsBlock ordinal title (totals: (string * Money.Money) list) =
    block ordinal title
        [ table 10 [ "", false; "Market value", true ]
            (totals |> List.mapi (fun i (group, total) -> row (10 + i) None [ (fun o -> textCell o group); (fun o -> moneyCell o total) ])) ]

let private writeNamed
    (pathInfo: OutputPathInput)
    (generatedAt: Instant)
    (netWorth: NetWorth)
    (assetAccounts: NamedLedgerRow list)
    (liabilityAccounts: NamedLedgerRow list)
    (investmentAccounts: (NetWorthInvestmentAccount * (string * string list)) list)
    (properties:
        (NetWorthProperty * (string * string list) * NamedLedgerRow list * NamedMortgageBalanceAccount list) list)
    (balanceAccounts: (NetWorthBalanceAccount * (string * string list)) list)
    : Result<NetWorthReturn, IAppError> =
    let asOfString = netWorth.asOf |> dateString
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Net Worth as of {asOfString}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    let header = createAsOfHeader "Net Worth" netWorth.asOf
    let absentNote =
        match netWorth.absentComponents with
        | [] -> "No component is absent."
        | absent -> $"""Absent on this date, each totalling 0.00: {absent |> List.map componentLabel |> String.concat ", "}."""
    let reportBody =
        createReportBody
            [ block 0 "Sources"
                  [ element 20 Paragraph (Class "absent-note") [ element 1 (NoTag(encode absentNote)) NoIdentifier [] ] ]
              block 1 "Counted ledger assets" [ ledgerTable 10 assetAccounts ]
              investmentsBlock 2 investmentAccounts
              propertiesBlock 3 properties
              block 4 "Liabilities" [ ledgerTable 10 liabilityAccounts ]
              balanceAccountsBlock 5 balanceAccounts
              totalsBlock 6 netWorth
              subtotalsBlock 7 "Investments by tax treatment"
                  (netWorth.investmentsByTaxTreatment |> List.map (fun (t, m) -> t |> TaxTreatment.toString, m))
              subtotalsBlock 8 "Investments by account group"
                  (netWorth.investmentsByAccountGroup |> List.map (fun (g, m) -> g |> AccountGroup.value, m)) ]
    let footer = createReportFooter generatedAt
    let section = { ordinal = 10; elementType = Section; identifierType = (Class "report"); contents = [ header; reportBody; footer ] }
    let htmlWrapper: HtmlWrapper = {
        language = "en"
        head = head
        body = { elements = [ section ] }
    }
    let dateInterpolation = if pathInfo.interpolateAsOf then $"-{asOfString}" else ""
    let fileName = $"{pathInfo.fileName}{dateInterpolation}.html"
    result {
        let! path = createFullPath pathInfo.baseDir fileName
        do! htmlWrapper
            |> HtmlWrapper.toString
            |> writeTextFile path
        return NetWorthReturn.Report { fullyQualifiedPath = path }
    }

let private namedLedgerRows (context: Context.Context) (rows: LedgerAccountBalance list) : Result<NamedLedgerRow list, IAppError> =
    rows
    |> List.map (fun r ->
        ``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]`` context r
        |> Result.map (fun named -> named.code, named.name, r.balance))
    |> convertListOfResultsToResultsList

/// Names each ledger account, investment account, property and Balance Account, keeping the order net worth gives them,
/// then writes the report.
let write
    (context: Context.Context)
    (pathInfo: OutputPathInput)
    (generatedAt: Instant)
    (netWorth: NetWorth)
    : Result<NetWorthReturn, IAppError> =
    result {
        let! assetAccounts = netWorth.assetAccounts |> namedLedgerRows context
        let! liabilityAccounts = netWorth.liabilityAccounts |> namedLedgerRows context
        let! investmentAccounts =
            netWorth.investmentAccounts
            |> List.map (fun a -> netWorthInvestmentAccountNames context a |> Result.map (fun names -> a, names))
            |> convertListOfResultsToResultsList
        let! properties =
            netWorth.properties
            |> List.map (fun p ->
                result {
                    let! names = netWorthPropertyNames context p
                    let! mortgages = p.mortgageAccounts |> namedLedgerRows context
                    let! mortgageBalanceAccounts =
                        p.mortgageBalanceAccounts
                        |> List.map (fun b ->
                            b.balanceAccountId
                            |> Business.FinancialServices.Positions.BalanceAccount.idToName context
                            |> Result.map (fun name -> name |> BalanceAccountName.value, b.balanceDate, b.balance))
                        |> convertListOfResultsToResultsList
                    return p, names, mortgages, mortgageBalanceAccounts
                })
            |> convertListOfResultsToResultsList
        let! balanceAccounts =
            netWorth.balanceAccounts
            |> List.map (fun b -> netWorthBalanceAccountNames context b |> Result.map (fun names -> b, names))
            |> convertListOfResultsToResultsList
        return!
            writeNamed pathInfo generatedAt netWorth assetAccounts liabilityAccounts investmentAccounts properties balanceAccounts
    }
