module Ui.ReportCli.ReportCliError

open App.Utility.IAppError

type ReportCliError =
    | ReportingUnknownReportName of string
    
    interface IAppError with
        member this.DomainName = nameof ReportCliError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | ReportingUnknownReportName name -> $"Unknown report: {name}."
            
let toMessage (e: ReportCliError) = (e :> IAppError).ToMessage()
let toAppError (e: ReportCliError) : IAppError = e :> IAppError
let error (e: ReportCliError) : Result<'T, IAppError> = Error (e :> IAppError)
