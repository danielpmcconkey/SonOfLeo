module Ui.OperatorCli.OperatorCliError

open App.Utility.IAppError

type OperatorCliError =
    | CliUnknownCommand of string * string
    
    interface IAppError with
        member this.DomainName = nameof OperatorCliError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | CliUnknownCommand(domain, verb) -> $"Unknown command: {domain} {verb}"
            
let toMessage (e: OperatorCliError) = (e :> IAppError).ToMessage()
let toAppError (e: OperatorCliError) : IAppError = e :> IAppError
let error (e: OperatorCliError) : Result<'T, IAppError> = Error (e :> IAppError)
