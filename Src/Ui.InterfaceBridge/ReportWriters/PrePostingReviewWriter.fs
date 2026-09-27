module Ui.InterfaceBridge.ReportWriters.PrePostingReviewWriter

open NodaTime
open App.Utility.IAppError
open App.Utility.Calendar
open App.Utility.File
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration.PrePostingReview
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents
open Ui.InterfaceBridge.ReportVisualizationAssets.BaseCssDeclarations
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportBody
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportFooter
open Ui.InterfaceBridge.ReportVisualizationAssets.ReportHeader

let specificCss = [
    {
        ordinal = 10
        declarator = ".entry"
        definition = "margin-top: 1.75rem;" }
    {
        ordinal = 20
        declarator = ".entry:first-child"
        definition = "margin-top: 0;" }
    {
        ordinal = 30
        declarator = ".entry-head"
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
        declarator = ".entry-head .head"
        definition = "flex: 1 1 auto; min-width: 0; font-weight: 600;" }
    {
        ordinal = 50
        declarator = ".tab"
        definition = """
    flex: 0 0 auto;
    font-size: 0.85rem;
    display: inline-flex;
    align-items: baseline;
    gap: 0.4rem;""" }
    {
        ordinal = 60
        declarator = ".tab .lbl"
        definition = """
    color: var(--ink-light);
    letter-spacing: 0.02em;
    text-transform: uppercase;
    font-size: 0.72rem;""" }
    {
        ordinal = 70
        declarator = ".tab .val"
        definition = "color: var(--ink); font-weight: 500; font-variant-numeric: tabular-nums;" }
    {
        ordinal = 80
        declarator = ".line"
        definition = """
    display: flex;
    gap: 1.5rem;
    align-items: baseline;
    flex-wrap: wrap;
    padding: 0.3rem 0 0.3rem 1.5rem;
    border-bottom: 1px dashed var(--rule);""" }
    {
        ordinal = 90
        declarator = ".line .head"
        definition = "flex: 1 1 auto; min-width: 0; font-size: 0.9rem;" }
    {
        ordinal = 100
        declarator = ".obligation"
        definition = """
    padding: 0.2rem 0 0.3rem 3rem;
    font-size: 0.82rem;
    color: var(--ink-light);""" }
    ]

let private encode (s: string) = System.Net.WebUtility.HtmlEncode s

let private span ordinal className text : DomElement =
    { ordinal = ordinal; elementType = Span (encode text); identifierType = Class className; contents = [] }

let private tab ordinal label value : DomElement =
    { ordinal = ordinal
      elementType = NestedSpan
      identifierType = Class "tab"
      contents =
        [ span 10 "lbl" label
          { ordinal = 20; elementType = Bold (encode value); identifierType = Class "val"; contents = [] } ] }

let private div ordinal className contents : DomElement =
    { ordinal = ordinal; elementType = Div; identifierType = Class className; contents = contents }

let private dateString (d: LocalDate) = d |> localDateToString "yyyy-MM-dd"

let private obligationElements (line: PrePostingLine) : DomElement list =
    match line.agreement with
    | None -> []
    | Some a ->
        let agreementText =
            $"Agreement: {a.masterAgreementName |> AgreementName.value} / {a.paymentAgreementName |> PaymentAgreementName.value}"
        let paymentLines =
            line.payments
            |> List.mapi (fun i p ->
                let text =
                    $"Pays {p.paymentAmount |> Money.toCurrencyString} of invoice {p.invoiceAmount |> Money.toCurrencyString}"
                    + $" dated {p.invoiceDate |> dateString}, due {p.dueDate |> dateString}"
                    + $" ({p.paymentState |> PaymentState.toString}); instance {p.instanceDate |> dateString}"
                div (20 + i) "obligation" [ span 10 "" text ])
        div 10 "obligation" [ span 10 "" agreementText ] :: paymentLines

let private lineElement ordinal (line: PrePostingLine) : DomElement =
    let accountText = $"{line.accountCode |> AccountCode.value} · {line.accountName |> AccountName.value}"
    let memoText = line.memo |> Option.map JournalEntryLineMemo.value |> Option.defaultValue ""
    let ruleText = line.ruleName |> Option.map ClassificationRuleName.value |> Option.defaultValue "none"
    div ordinal "line-block"
        (div 1 "line"
            [ span 10 "head" accountText
              tab 20 (line.lineType |> JournalEntryLineType.toString) (line.amount |> Money.toCurrencyString)
              tab 30 "Rule" ruleText
              tab 40 "Memo" memoText ]
         :: (obligationElements line |> List.map (fun e -> { e with ordinal = e.ordinal + 1 })))

let private entryElement ordinal (entry: PrePostingEntry) : DomElement =
    let head =
        div 1 "entry-head"
            [ span 10 "head" $"{entry.entryDate |> dateString} · {entry.description |> JournalEntryDescription.value}"
              tab 20 "Source" (entry.sourceName |> JournalRefFinancialInstitution.value)
              tab 30 "FI ref" (entry.fiReference |> JournalExternalReferenceText.value)
              tab 40 "Status" (entry.status |> StagedEntryStatus.toString) ]
    div ordinal "entry" (head :: (entry.lines |> List.mapi (fun i l -> lineElement (10 + i) l)))

let write
    (pathInfo: OutputPathInput)
    (runDate: LocalDate)
    (entries: PrePostingEntry list)
    : Result<PrePostingReviewReturn, IAppError> =
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Pre-Posting Review run {runDate |> dateString}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    let lineCount = entries |> List.sumBy (fun e -> e.lines |> List.length)
    // REQ-RPT-7.7: the header shows the run date and the number of entries and lines
    let subtitle =
        div 20 "range"
            [ { ordinal = 10; elementType = NoTag "Run on "; identifierType = NoIdentifier; contents = [] }
              { ordinal = 20; elementType = Bold (runDate |> dateString); identifierType = NoIdentifier; contents = [] }
              { ordinal = 30; elementType = NoTag $" · {entries.Length} entries, {lineCount} lines"; identifierType = NoIdentifier; contents = [] } ]
    let header = createReportHeader "Pre-Posting Review" subtitle
    let reportBody = createReportBody (entries |> List.mapi (fun i e -> entryElement (i + 1) e))
    let footer = createReportFooter()
    let section = { ordinal = 10; elementType = Section; identifierType = (Class "report"); contents = [ header; reportBody; footer ] }
    let htmlWrapper: HtmlWrapper = {
        language = "en"
        head = head
        body = { elements = [ section ] }
    }
    let dateInterpolation = if pathInfo.interpolateAsOf then $"-{runDate |> dateString}" else ""
    let fileName = $"{pathInfo.fileName}{dateInterpolation}.html"
    result {
        let! path = createFullPath pathInfo.baseDir fileName
        do! htmlWrapper
            |> HtmlWrapper.toString
            |> writeTextFile path
        return PrePostingReviewReturn.Report { fullyQualifiedPath = path }
    }
