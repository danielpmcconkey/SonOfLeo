module Ui.InterfaceBridge.ReportWriters.PeriodActivityWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Calendar
open App.Utility.File
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.PeriodActivity
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents
open Ui.InterfaceBridge.ReportVisualizationAssets.BaseCssDeclarations
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportBody
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportFooter
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportHeader

let specificCss = [
    {
        ordinal = 10
        declarator = ".acct"
        definition = "margin-top: 1.75rem;" }
    {
        ordinal = 20
        declarator = ".acct:first-child"
        definition = "margin-top: 0;" }
    {
        ordinal = 30
        declarator = ".acct-head"
        definition = """
    display: flex;
    gap: 1.5rem;
    align-items: baseline;
    flex-wrap: wrap;
    padding: 0.5rem 0 0.4rem;
    border-top: 1.5px solid var(--ink);
    border-bottom: 1px solid var(--rule-strong);""" }
    {
        ordinal = 40
        declarator = ".acct-head .head, .line .head"
        definition = "flex: 1 1 auto; min-width: 0;" }
    {
        ordinal = 50
        declarator = ".acct-head .head"
        definition = "font-weight: 600;" }
    {
        ordinal = 60
        declarator = ".tab"
        definition = """
    flex: 0 0 auto;
    font-size: 0.85rem;
    display: inline-flex;
    align-items: baseline;
    gap: 0.4rem;
    font-variant-numeric: tabular-nums;""" }
    {
        ordinal = 70
        declarator = ".tab .lbl"
        definition = """
    color: var(--ink-light);
    letter-spacing: 0.02em;
    text-transform: uppercase;
    font-size: 0.72rem;""" }
    {
        ordinal = 80
        declarator = ".tab .val"
        definition = "color: var(--ink); font-weight: 500;" }
    {
        ordinal = 90
        declarator = ".tab .val.neg"
        definition = "color: var(--neg);" }
    {
        ordinal = 100
        declarator = ".tab .val.zero"
        definition = "color: var(--zero);" }
    {
        ordinal = 110
        declarator = ".line"
        definition = """
    display: flex;
    gap: 1.5rem;
    align-items: baseline;
    flex-wrap: wrap;
    padding: 0.3rem 0 0.3rem 1.5rem;
    border-bottom: 1px dashed var(--rule);
    font-size: 0.9rem;""" }
    ]

let private encode (s: string) = System.Net.WebUtility.HtmlEncode s

let private dateString (d: LocalDate) = d |> localDateToString "yyyy-MM-dd"

let private span ordinal className text : DomElement =
    { ordinal = ordinal; elementType = Span (encode text); identifierType = Class className; contents = [] }

let private div ordinal className contents : DomElement =
    { ordinal = ordinal; elementType = Div; identifierType = Class className; contents = contents }

let private tab ordinal label valueClass value : DomElement =
    { ordinal = ordinal
      elementType = NestedSpan
      identifierType = Class "tab"
      contents =
        [ span 10 "lbl" label
          { ordinal = 20; elementType = Bold (encode value); identifierType = Class valueClass; contents = [] } ] }

let private moneyTab ordinal label (m: Money.Money) : DomElement =
    let valueClass =
        if m |> Money.isNegative then "val neg"
        elif m |> Money.isZero then "val zero"
        else "val"
    tab ordinal label valueClass (m |> Money.toAccountingString)

let private lineElement ordinal (line: PeriodActivityLine) : DomElement =
    let memoText = line.memo |> Option.map JournalEntryLineMemo.value |> Option.defaultValue ""
    div ordinal "line"
        [ span 10 "head" $"{line.entryDate |> dateString} · {line.description |> JournalEntryDescription.value}"
          moneyTab 20 (line.lineType |> JournalEntryLineType.toString) line.amount
          tab 30 "Memo" "val" memoText ]

let private accountElement ordinal (account: PeriodActivityAccount) : DomElement =
    let head =
        div 1 "acct-head"
            [ span 10 "head" $"{account.accountCode |> AccountCode.value} · {account.accountName |> AccountName.value}"
              moneyTab 20 "Net" account.netTotal ]
    div ordinal "acct" (head :: (account.lines |> List.mapi (fun i l -> lineElement (10 + i) l)))

let write
    (pathInfo: OutputPathInput)
    (generatedAt: Instant)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (accounts: PeriodActivityAccount list)
    : Result<PeriodActivityReturn, IAppError> =
    let beginString = beginDate |> dateString
    let endString = endDate |> dateString
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Period Activity {beginString} to {endString}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    // the header shows the range
    let subtitle =
        div 20 "range"
            [ { ordinal = 10; elementType = NoTag "From "; identifierType = NoIdentifier; contents = [] }
              { ordinal = 20; elementType = Bold beginString; identifierType = NoIdentifier; contents = [] }
              { ordinal = 30; elementType = NoTag " to "; identifierType = NoIdentifier; contents = [] }
              { ordinal = 40; elementType = Bold endString; identifierType = NoIdentifier; contents = [] } ]
    let header = createReportHeader "Period Activity" subtitle
    let reportBody = createReportBody (accounts |> List.mapi (fun i a -> accountElement (i + 1) a))
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
        return PeriodActivityReturn.Report { fullyQualifiedPath = path }
    }
