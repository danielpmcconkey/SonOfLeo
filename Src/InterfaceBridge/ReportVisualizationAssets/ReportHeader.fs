module InterfaceBridge.ReportVisualizationAssets.ReportHeader

open NodaTime
open Utilities.Calendar

let createReportHeader title subtitle =
    {
        ordinal = 10; elementType = Header; identifierType = (Class "report-head"); contents =
            [
                {ordinal = 10; elementType = (H1 title); identifierType = NoIdentifier; contents = []}
                subtitle
            ]
    }
    
let createAsOfSubtitle (asOf: LocalDate) : DomElement =
    let fDate = asOf |> localDateToString "yyyy-MM-dd"
    {
        ordinal = 20; elementType = Div; identifierType = (Class "range"); contents = 
            [
                {ordinal = 10; elementType = (NoTag "As of "); identifierType = NoIdentifier; contents = []}
                {ordinal = 10; elementType = (Bold fDate); identifierType = NoIdentifier; contents = []}
            ]
    }

let createAsOfHeader title asOf =
    let subtitle =createAsOfSubtitle asOf
    createReportHeader title subtitle

