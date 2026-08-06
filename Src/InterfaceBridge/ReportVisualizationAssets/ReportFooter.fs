module InterfaceBridge.ReportVisualizationAssets.ReportFooter

open Utilities

let createReportFooter  =
    let createTime = Clock.now() |> Clock.instantToString "yyyy-MM-dd HH:mm:ss"
    let content = $"Generated: {createTime}"
    {
        ordinal = 30
        elementType = (Footer content)
        identifierType = (Class "report-foot")
        contents = []
    }
    
