module Ui.InterfaceBridge.ReportWriters.BalanceSheetIntegrityWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Calendar
open App.Utility.File
open App.Utility.Result
open Business.FinancialServices
open Business.CrossDomainOrchestration.BalanceSheetIntegrity
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.InterfaceContracts.BalanceSheetIntegrityContracts
open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents
open Ui.InterfaceBridge.ReportVisualizationAssets.BaseCssDeclarations
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportBody
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportFooter
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportHeader

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
        declarator = ".block-head"
        definition = """
    font-weight: 600;
    padding: 0.5rem 0 0.4rem;
    border-top: 1.5px solid var(--ink);
    border-bottom: 1px solid var(--rule-strong);""" }
    {
        ordinal = 40
        declarator = ".figure"
        definition = """
    display: flex;
    justify-content: space-between;
    gap: 1.5rem;
    padding: 0.3rem 0;
    border-bottom: 1px dashed var(--rule);
    font-variant-numeric: tabular-nums;""" }
    {
        ordinal = 50
        declarator = ".figure .lbl"
        definition = "color: var(--ink-light);" }
    {
        ordinal = 60
        declarator = ".figure .val"
        definition = "color: var(--ink); font-weight: 500;" }
    {
        ordinal = 70
        declarator = ".figure .val.neg"
        definition = "color: var(--neg);" }
    {
        ordinal = 80
        declarator = ".figure .val.zero"
        definition = "color: var(--zero);" }
    {
        ordinal = 90
        declarator = ".figure.check .val"
        definition = "font-weight: 700;" }
    ]

let private encode (s: string) = System.Net.WebUtility.HtmlEncode s

let private div ordinal className contents : DomElement =
    { ordinal = ordinal; elementType = Div; identifierType = Class className; contents = contents }

let private figure ordinal className label valueClass value : DomElement =
    div ordinal className
        [ { ordinal = 10; elementType = Span (encode label); identifierType = Class "lbl"; contents = [] }
          { ordinal = 20; elementType = Bold (encode value); identifierType = Class valueClass; contents = [] } ]

let private moneyFigure ordinal className label (m: Money.Money) : DomElement =
    let signClass =
        match m |> Money.amount with
        | a when a < 0M -> "val neg"
        | a when a = 0M -> "val zero"
        | _ -> "val"
    figure ordinal className label signClass (m |> Money.toAccountingString)

let private block ordinal title figures : DomElement =
    div ordinal "block" (div 1 "block-head" [ { ordinal = 10; elementType = Span (encode title); identifierType = NoIdentifier; contents = [] } ] :: figures)

let write
    (pathInfo: OutputPathInput)
    (integrity: BalanceSheetIntegrity)
    : Result<BalanceSheetIntegrityReturn, IAppError> =
    let asOf = integrity.asOf
    let asOfString = asOf |> localDateToString "yyyy-MM-dd"
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Balance-Sheet Integrity as of {asOfString}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    let header = createAsOfHeader "Balance-Sheet Integrity" asOf
    let totals =
        block 1 "Debits and credits"
            [ moneyFigure 10 "figure" "Total debits" integrity.totalDebits
              moneyFigure 20 "figure" "Total credits" integrity.totalCredits
              figure 30 "figure check" "Debits equal credits" "val" (if integrity.debitsEqualCredits then "Yes" else "No") ]
    let accountTypes =
        block 2 "Account types, in their normal-balance direction"
            [ moneyFigure 10 "figure" "Assets" integrity.assets
              moneyFigure 20 "figure" "Liabilities" integrity.liabilities
              moneyFigure 30 "figure" "Equity" integrity.equity
              moneyFigure 40 "figure" "Revenue" integrity.revenue
              moneyFigure 50 "figure" "Expenses" integrity.expenses
              moneyFigure 60 "figure" "Net income (revenue minus expenses)" integrity.netIncome
              moneyFigure 70 "figure check" "Residual: assets minus (liabilities + equity + net income)" integrity.residual ]
    let reportBody = createReportBody [ totals; accountTypes ]
    let footer = createReportFooter()
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
        return BalanceSheetIntegrityReturn.Report { fullyQualifiedPath = path }
    }
