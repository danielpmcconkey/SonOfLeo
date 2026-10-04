module Ui.InterfaceBridge.ReportWriters.NetWorthWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.File
open App.Utility.Calendar
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.NetWorth
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
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

let private ledgerTable ordinal (rows: LedgerAccountBalance list) =
    table ordinal [ "Code", false; "Name", false; "Balance", true ]
        (rows |> List.mapi (fun i r -> row (10 + i) None [ (fun o -> textCell o r.code); (fun o -> textCell o r.name); (fun o -> moneyCell o r.balance) ]))

let private joined (names: string list) = names |> String.concat ", "

let private investmentsBlock ordinal (accounts: NetWorthInvestmentAccount list) =
    let rows =
        accounts
        |> List.mapi (fun i a ->
            row (10 + i) None
                [ (fun o -> textCell o a.investmentAccountName)
                  (fun o -> textCell o (joined a.ownerNames))
                  (fun o -> textCell o a.accountGroup)
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

let private propertiesBlock ordinal (properties: NetWorthProperty list) =
    let propertyTable i (p: NetWorthProperty) =
        let source =
            match p.valueSource with
            | ValuationDated d -> $"valuation of {dateString d}"
            | PurchaseBasisValue -> "purchase basis"
        let mortgageRows =
            p.mortgageAccounts
            |> List.mapi (fun j m ->
                row (20 + j) None
                    [ (fun o -> textCell o $"Mortgage {m.code} · {m.name}"); (fun o -> textCell o ""); (fun o -> moneyCell o m.balance) ])
        table (10 + i)
            [ $"{p.propertyName} ({p.propertyUse |> PropertyUse.toString}), owned by {joined p.ownerNames}", false
              "Source", false; "Amount", true ]
            ([ row 10 None [ (fun o -> textCell o "Value"); (fun o -> textCell o source); (fun o -> moneyCell o p.value) ] ]
             @ mortgageRows
             @ [ row 90 (Some "total") [ (fun o -> textCell o "Equity"); (fun o -> textCell o ""); (fun o -> moneyCell o p.equity) ] ])
    block ordinal "Properties owned on the date" (properties |> List.mapi propertyTable)

let private totalsBlock ordinal (netWorth: NetWorth) =
    let line i label amount = row (10 + i) None [ (fun o -> textCell o label); (fun o -> moneyCell o amount) ]
    block ordinal "Totals"
        [ table 10 [ "", false; "Amount", true ]
            [ line 0 "Counted ledger assets" netWorth.totalLedgerAssets
              line 1 "Investments" netWorth.totalInvestments
              line 2 "Property values" netWorth.totalPropertyValues
              line 3 "Liabilities" netWorth.totalLiabilities
              row 20 (Some "total") [ (fun o -> textCell o "Net worth"); (fun o -> moneyCell o netWorth.netWorth) ]
              row 21 (Some "total") [ (fun o -> textCell o "Investable wealth"); (fun o -> moneyCell o netWorth.investableWealth) ] ] ]

let private subtotalsBlock ordinal title (totals: (string * Money.Money) list) =
    block ordinal title
        [ table 10 [ "", false; "Market value", true ]
            (totals |> List.mapi (fun i (group, total) -> row (10 + i) None [ (fun o -> textCell o group); (fun o -> moneyCell o total) ])) ]

let write
    (pathInfo: OutputPathInput)
    (generatedAt: Instant)
    (netWorth: NetWorth)
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
    let reportBody =
        createReportBody
            [ block 1 "Counted ledger assets" [ ledgerTable 10 netWorth.assetAccounts ]
              investmentsBlock 2 netWorth.investmentAccounts
              propertiesBlock 3 netWorth.properties
              block 4 "Liabilities" [ ledgerTable 10 netWorth.liabilityAccounts ]
              totalsBlock 5 netWorth
              subtotalsBlock 6 "Investments by tax treatment"
                  (netWorth.investmentsByTaxTreatment |> List.map (fun (t, m) -> t |> TaxTreatment.toString, m))
              subtotalsBlock 7 "Investments by account group" netWorth.investmentsByAccountGroup ]
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
