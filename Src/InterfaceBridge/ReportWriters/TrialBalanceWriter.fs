module InterfaceBridge.ReportWriters.TrialBalanceWriter

open System.Globalization
open InterfaceBridge.InterfaceContracts.ReportsContracts
open InterfaceBridge.ReportVisualizationAssets
open InterfaceBridge.ReportVisualizationAssets.Css
open ModelOrchestrator.TrialBalanceReport
open NodaTime
open Utilities.AppError
open Utilities.FileIO

let write
    (pathInfo: OutputPathInput)
    (asOf: LocalDate)
    (sortedRows: TrialBalanceReturnRow list)
    : Result<TrialBalanceReturn, AppError> =
    let header = {
        charSet = "utf-8"
        title = $"Son of Leo: Trial Balance Report as of {asOf}"
        baseCss = baseCssDeclarations
        specificCss = []
        script = ""
    }
    let body = {elements =
        [
            { ordinal = 10; elementType = Paragraph; identifierType = NoIdentifier; contents =
                [
                    {ordinal = 10; elementType = (Span "Hello, world. "); identifierType = NoIdentifier; contents = []}
                    {ordinal = 20; elementType = (Span "Now fuck off."); identifierType = NoIdentifier; contents = []}
                ] }
        ]}
    let htmlWrapper:HtmlWrapper = {
        language = "en"
        header = header
        body = body
    }
    let stringContents = htmlWrapper |> HtmlWrapper.toString
    let dateInterpolation =
        if pathInfo.interpolateAsOf
        then
            let fDate = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            $"-{fDate}"
        else ""
    let fileName = $"{pathInfo.fileName}{dateInterpolation}"
    match stringContents |> writeTextFile pathInfo.baseDir fileName "html" with
    | Error e -> Error e
    | Ok path -> Ok (TrialBalanceReturn.Report{ fullyQualifiedPath = path})
