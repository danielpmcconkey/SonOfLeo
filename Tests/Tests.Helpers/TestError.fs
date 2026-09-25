module Tests.Helpers.TestError

open App.Utility.IAppError

type TestError =
    | TestingError of string
    
    interface IAppError with
        member this.DomainName = nameof TestError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | TestingError message -> message
            
let toMessage (e: TestError) = (e :> IAppError).ToMessage()
let toAppError (e: TestError) : IAppError = e :> IAppError
let error (e: TestError) : Result<'T, IAppError> = Error (e :> IAppError)


