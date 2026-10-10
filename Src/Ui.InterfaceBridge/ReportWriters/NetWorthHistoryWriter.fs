module Ui.InterfaceBridge.ReportWriters.NetWorthHistoryWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.File
open App.Utility.Calendar
open Business.FinancialServices
open Business.CrossDomainOrchestration.NetWorthHistory
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents
open Ui.InterfaceBridge.ReportVisualizationAssets.BaseCssDeclarations
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportHeader
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportBody
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportFooter

let specificCss = [
    {
        ordinal = 10
        declarator = "table"
        definition = "width: 100%; border-collapse: collapse; font-size: 0.9rem;" }
    {
        ordinal = 20
        declarator = "th, td"
        definition = "padding: 0.25rem 0.5rem; border-bottom: 1px solid var(--rule); text-align: right; font-variant-numeric: tabular-nums;" }
    {
        ordinal = 30
        declarator = "th"
        definition = """
    color: var(--ink-light);
    font-weight: 500;
    letter-spacing: 0.02em;
    font-size: 0.72rem;""" }
    {
        ordinal = 40
        declarator = "th.text, td.text"
        definition = "text-align: left;" }
    {
        ordinal = 50
        declarator = "th.total, td.total"
        definition = "font-weight: 600;" }
    {
        ordinal = 60
        declarator = "tr.pre-ledger td"
        definition = "font-style: italic;" }
    ]

let private encode (s: string) = System.Net.WebUtility.HtmlEncode s

let private dateString (d: LocalDate) = d |> localDateToString "yyyy-MM-dd"

let private element ordinal elementType identifierType contents : DomElement =
    { ordinal = ordinal; elementType = elementType; identifierType = identifierType; contents = contents }

let write
    (pathInfo: OutputPathInput)
    (generatedAt: Instant)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (points: NetWorthPoint list)
    : Result<NetWorthHistoryReturn, IAppError> =
    let beginString = beginDate |> dateString
    let endString = endDate |> dateString
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Net Worth History {beginString} to {endString}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    let subtitle =
        element 20 Div (Class "range")
            [ element 10 (NoTag "From ") NoIdentifier []
              element 20 (Bold beginString) NoIdentifier []
              element 30 (NoTag " to ") NoIdentifier []
              element 40 (Bold endString) NoIdentifier [] ]
    let header = createReportHeader "Net Worth History" subtitle
    let headings =
        [ "Month end", "text"; "Source", "text"; "Counted ledger assets", ""; "Investments", ""; "Property values", ""
          "Liabilities", ""; "Mortgages of owned properties", ""; "Net worth", "total"; "Investable wealth", "total"
          "Absent", "text" ]
    let cellClass c = if c = "" then NoIdentifier else Class c
    let headRow =
        element 1 TableRow NoIdentifier
            (headings |> List.mapi (fun i (h, c) -> element (10 + i) (TableHeadCell(encode h)) (cellClass c) []))
    let moneyText (m: Money.Money) = m |> Money.toAccountingString
    let pointRow i (point: NetWorthPoint) =
        let absent = point.absentComponents |> List.map NetWorthWriter.componentLabel |> String.concat ", "
        let cells =
            [ point.monthEnd |> dateString, "text"
              (if point.isPreLedger then "pre-ledger" else "ledger"), "text"
              moneyText point.totalLedgerAssets, ""
              moneyText point.totalInvestments, ""
              moneyText point.totalPropertyValues, ""
              moneyText point.totalLiabilities, ""
              moneyText point.totalOwnedPropertyMortgages, ""
              moneyText point.netWorth, "total"
              moneyText point.investableWealth, "total"
              absent, "text" ]
        element (10 + i) TableRow (if point.isPreLedger then Class "pre-ledger" else NoIdentifier)
            (cells |> List.mapi (fun j (text, c) -> element (10 + j) (TableDataCell(encode text)) (cellClass c) []))
    let historyTable = element 1 Table NoIdentifier (headRow :: (points |> List.mapi pointRow))
    let reportBody = createReportBody [ historyTable ]
    let footer = createReportFooter generatedAt
    let section = { ordinal = 10; elementType = Section; identifierType = (Class "report"); contents = [ header; reportBody; footer ] }
    let htmlWrapper: HtmlWrapper = {
        language = "en"
        head = head
        body = { elements = [ section ] }
    }
    let dateInterpolation = if pathInfo.interpolateAsOf then $"-{beginString}_{endString}" else ""
    let fileName = $"{pathInfo.fileName}{dateInterpolation}.html"
    result {
        let! path = createFullPath pathInfo.baseDir fileName
        do! htmlWrapper
            |> HtmlWrapper.toString
            |> writeTextFile path
        return NetWorthHistoryReturn.Report { fullyQualifiedPath = path }
    }
