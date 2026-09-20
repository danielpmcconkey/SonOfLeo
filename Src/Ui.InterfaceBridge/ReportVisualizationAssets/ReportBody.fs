module Ui.InterfaceBridge.ReportVisualizationAssets.ReportBody

open Ui.InterfaceBridge.ReportVisualizationAssets.HtmlComponents
let createReportBody elements =
    {
        ordinal = 20
        elementType = Div
        identifierType = (Class "report-body")
        contents = elements
    }
    
