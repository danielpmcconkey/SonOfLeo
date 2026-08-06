module InterfaceBridge.ReportVisualizationAssets.ReportBody

let createReportBody elements =
    {
        ordinal = 20
        elementType = Div
        identifierType = (Class "report-body")
        contents = elements
    }
    
