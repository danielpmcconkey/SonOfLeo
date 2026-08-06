module InterfaceBridge.ReportWriters.TrialBalanceWriter

open InterfaceBridge.InterfaceContracts.ReportsContracts
open InterfaceBridge.ReportVisualizationAssets
open InterfaceBridge.ReportVisualizationAssets.Css
open InterfaceBridge.ReportVisualizationAssets.ReportBody
open InterfaceBridge.ReportVisualizationAssets.ReportFooter
open InterfaceBridge.ReportVisualizationAssets.ReportHeader
open Model
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.TrialBalanceReport
open NodaTime
open Utilities.AppError
open Utilities.Calendar
open Utilities.FileIO
open Utilities.ResultHelper


let specificCss = [
    //* ----- account blocks ----- */
    {
        ordinal = 10
        declarator = ".acct"
        definition = "margin: 0;" }
    {
        ordinal = 20
        declarator = ".acct-label"
        definition = """
    display: flex;
    gap: 2.5rem;
    align-items: baseline;
    flex-wrap: wrap;
    padding: 0.3rem 0;""" }
    {
        ordinal = 30
        declarator = ".acct-label .head"
        definition = """
    flex: 1 1 auto;
    min-width: 0;""" }
    {
        ordinal = 40
        declarator = ".acct-label .tab"
        definition = """
    flex: 0 0 auto;
    font-variant-numeric: tabular-nums;
    font-size: 0.85rem;
    display: inline-flex;
    align-items: baseline;
    gap: 0.5rem;""" }
    {
        ordinal = 50
        declarator = ".acct-label .tab .lbl"
        definition = """
    color: var(--ink-light);
    font-weight: 400;
    letter-spacing: 0.02em;
    text-transform: uppercase;
    font-size: 0.72rem;""" }
    {
        ordinal = 60
        declarator = ".acct-label .tab .val"
        definition = "color: var(--ink); font-weight: 500;" }
    {
        ordinal = 70
        declarator = ".acct-label .tab .val.neg"
        definition = "color: var(--neg);" }
    {
        ordinal = 80
        declarator = ".acct-label .tab .val.zero"
        definition = """color: var(--zero);""" }
    //* level-0: top band */
    {
        ordinal = 90
        declarator = ".acct.level-0 "
        definition = "margin-top: 2.5rem;" }
    {
        ordinal = 100
        declarator = ".acct.level-0:first-child"
        definition = "margin-top: 0;" }
    {
        ordinal = 110
        declarator = ".acct.level-0 > .acct-label"
        definition = """
    padding: 0.75rem 0 0.65rem;
    border-top: 1.5px solid var(--ink);
    border-bottom: 1px solid var(--rule-strong);
    margin-bottom: 0.5rem;""" }
    {
        ordinal = 120
        declarator = ".acct.level-0 > .acct-label .head"
        definition = """
    font-size: 0.95rem;
    font-weight: 600;
    letter-spacing: 0.12em;
    text-transform: uppercase;""" }
    {
        ordinal = 130
        declarator = ".acct.level-0 > .acct-label .tab .val"
        definition = "font-size: 1rem; font-weight: 500;" }
    //* level-1: mid parent */
    {
        ordinal = 140
        declarator = ".acct.level-1"
        definition = "padding-left: 1.5rem; margin-top: 0.85rem;" }
    {
        ordinal = 150
        declarator = ".acct.level-1 > .acct-label "
        definition = """
    padding: 0.4rem 0 0.3rem;
    border-bottom: 1px solid var(--rule);
    margin-bottom: 0.25rem;""" }
    {
        ordinal = 160
        declarator = ".acct.level-1 > .acct-label .head"
        definition = """
    font-size: 0.95rem;
    font-weight: 600;""" }
    //* level-2: sub-parent */
    {
        ordinal = 170
        declarator = ".acct.level-2"
        definition = "padding-left: 3rem; margin-top: 0.4rem;" }
    {
        ordinal = 180
        declarator = ".acct.level-2 > .acct-label"
        definition = """
    padding: 0.3rem 0 0.25rem;
    border-bottom: 1px dashed var(--rule);""" }
    {
        ordinal = 190
        declarator = ".acct.level-2 > .acct-label .head"
        definition = "font-weight: 500; font-size: 0.9rem;" }
    //* level-3+: leaves */
    {
        ordinal = 200
        declarator = ".acct.level-3"
        definition = "padding-left: 4.5rem; margin-top: 0.25rem;" }
    {
        ordinal = 210
        declarator = ".acct.level-4"
        definition = "padding-left: 6rem;" }
    {
        ordinal = 220
        declarator = ".acct.level-5"
        definition = "padding-left: 7.5rem;" }
    {
        ordinal = 230
        declarator = ".acct.leaf > .acct-label"
        definition = "padding: 0.35rem 0;" }
    {
        ordinal = 240
        declarator = ".acct.leaf > .acct-label .head"
        definition = "font-weight: 500; font-size: 0.88rem;" }
    {
        ordinal = 250
        declarator = ".acct.leaf.dormant > .acct-label .head"
        definition = "color: var(--ink-light); font-weight: 400;" }
    {
        ordinal = 260
        declarator = ".acct.leaf.dormant > .acct-label .tab .val"
        definition = "color: var(--zero);" }
    ]

type LabelType =
    | Credits
    | Debits
    | NetBal
    
let createAccountLabel labelType amount ordinal   =
    let fAmount = amount |> Money.toCurrencyString
    let className =
        match amount |> Money.amount with
        | 0M -> "val zero"
        | x when x > 0M -> "val pos"
        | _ -> "val neg"
    let label =
        match labelType with
        | Credits -> "Credits"
        | Debits -> "Debits"
        | NetBal -> "Net Balance"
    let labelSpan:DomElement =
        { ordinal = 10
          elementType = (Span label)
          identifierType = (Class "lbl")
          contents = [] }
    let labelBold =
        { ordinal = 20
          elementType = (Bold fAmount)
          identifierType = (Class className)
          contents =[] }
    {
        ordinal = ordinal
        elementType = NestedSpan
        identifierType = (Class "tab")
        contents = [ labelSpan; labelBold ]
    } 

let createAccountRowDomElement ordinal row =
    let code = row.accountCode |> AccountCode.value
    let accountName = row.accountName |> AccountName.value
    let accountLabelName = {
        ordinal = 10
        elementType = (Span $"{code} &middot; {accountName}")
        identifierType = (Class "head")
        contents = []
    }
    let accountLabelCredits = createAccountLabel Credits row.totalCredits 10
    let accountLabelDebits = createAccountLabel Debits row.totalDebits 20
    let accountLabelNet = createAccountLabel NetBal row.netBalance 30
    let divClass = $"acct level-{row.generation}"
    let divInside = {
        ordinal = 10
        elementType = Div
        identifierType = (Class "acct-label")
        contents = [accountLabelName; accountLabelCredits; accountLabelDebits; accountLabelNet] }
    {
        ordinal = ordinal
        elementType = Div
        identifierType = (Class divClass)
        contents = [divInside]
    } 
    

let write
    (pathInfo: OutputPathInput)
    (asOf: LocalDate)
    (sortedRows: TrialBalanceRowFlattened list)
    : Result<TrialBalanceReturn, AppError> =
    let head = {
        charSet = "utf-8"
        title = $"Son of Leo: Trial Balance Report as of {asOf}"
        baseCss = baseCssDeclarations
        specificCss = specificCss
        script = ""
    }
    let header = createAsOfHeader "Trial Balance Report" asOf
    
    let accountRows =
        [ 1 .. (sortedRows |> List.length) ]
        |> List.zip sortedRows
        |> List.map(fun (row, iterator) ->
            row |> createAccountRowDomElement iterator)
    let reportBody = createReportBody accountRows
    let footer = createReportFooter()
    let section = {
            ordinal = 10; elementType = Section; identifierType = (Class "report"); contents =
                [
                    header
                    reportBody
                    footer
                ]
        }
    let body = {elements =
        [
            section
        ]}
    let htmlWrapper:HtmlWrapper = {
        language = "en"
        head = head
        body = body
    }
    let dateInterpolation =
        if pathInfo.interpolateAsOf
        then
            let fDate = asOf |> localDateToString "yyyy-MM-dd"
            $"-{fDate}"
        else ""
    let fileName = $"{pathInfo.fileName}{dateInterpolation}"
    result {
        let! path =
            htmlWrapper
            |> HtmlWrapper.toString
            |> writeTextFile pathInfo.baseDir fileName "html" 
        return TrialBalanceReturn.Report{ fullyQualifiedPath = path}
    }
