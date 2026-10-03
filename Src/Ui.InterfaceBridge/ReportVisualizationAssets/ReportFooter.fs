module Ui.InterfaceBridge.ReportVisualizationAssets.ReportFooter

open App.Utility
open NodaTime
open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents

let createReportFooter (generatedAt: Instant) =
    let createTime = generatedAt |> Clock.instantToString "yyyy-MM-dd HH:mm:ss"
    let content = $"Generated: {createTime}"
    {
        ordinal = 30
        elementType = (Footer content)
        identifierType = (Class "report-foot")
        contents = []
    }
    
