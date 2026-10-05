module Ui.InterfaceBridge.ReportWriters.InvestmentWealthHistoryWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.File
open App.Utility.Calendar
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.InvestmentWealthHistory
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
        declarator = "th.date, td.date"
        definition = "text-align: left;" }
    {
        ordinal = 50
        declarator = "th.total, td.total"
        definition = "font-weight: 600;" }
    ]

let private encode (s: string) = System.Net.WebUtility.HtmlEncode s

let private dateString (d: LocalDate) = d |> localDateToString "yyyy-MM-dd"

let private element ordinal elementType identifierType contents : DomElement =
    { ordinal = ordinal; elementType = elementType; identifierType = identifierType; contents = contents }

let groupLabel (group: WealthGroup) =
    match group with
    | GroupName name -> name
    | GroupOwners owners -> owners |> String.concat " & "
    | WealthGroup.Unassigned -> "Unassigned"

let write
    (pathInfo: OutputPathInput)
    (generatedAt: Instant)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (grouping: WealthGrouping)
    (points: WealthPoint list)
    : Result<InvestmentWealthHistoryReturn, IAppError> =
    let beginString = beginDate |> dateString
    let endString = endDate |> dateString
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Investment Wealth History {beginString} to {endString}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    let subtitle =
        element 20 Div (Class "range")
            [ element 10 (NoTag "From ") NoIdentifier []
              element 20 (Bold beginString) NoIdentifier []
              element 30 (NoTag " to ") NoIdentifier []
              element 40 (Bold endString) NoIdentifier []
              element 50 (NoTag $", by {grouping |> WealthGrouping.toString}") NoIdentifier [] ]
    let header = createReportHeader "Investment Wealth History" subtitle
    // one column per group value present at any point
    let groups = points |> List.collect (fun p -> p.totals |> List.map fst) |> List.distinct |> List.sort
    let headRow =
        element 1 TableRow NoIdentifier
            ([ element 1 (TableHeadCell "Month end") (Class "date") [] ]
             @ (groups |> List.mapi (fun i g -> element (10 + i) (TableHeadCell(encode (groupLabel g))) NoIdentifier []))
             @ [ element 100000 (TableHeadCell "Total") (Class "total") [] ])
    let moneyText (m: Money.Money) = m |> Money.toAccountingString
    let pointRow i (point: WealthPoint) =
        // every point carries every group, at zero where it has no holdings
        let amountIn group = point.totals |> List.find (fun (g, _) -> g = group) |> snd |> moneyText
        element (10 + i) TableRow NoIdentifier
            ([ element 1 (TableDataCell(point.monthEnd |> dateString)) (Class "date") [] ]
             @ (groups |> List.mapi (fun j g -> element (10 + j) (TableDataCell(amountIn g)) NoIdentifier []))
             @ [ element 100000 (TableDataCell(moneyText point.total)) (Class "total") [] ])
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
        return InvestmentWealthHistoryReturn.Report { fullyQualifiedPath = path }
    }
