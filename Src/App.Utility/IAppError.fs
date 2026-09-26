module App.Utility.IAppError

open Microsoft.FSharp.Reflection

let getUnionCaseName (x: obj) =
      FSharpValue.GetUnionFields(x, x.GetType()) |> fst |> _.Name
      
type IAppError =
    abstract member ToMessage: unit -> string
    abstract member DomainName: string
    abstract member CaseName: string

/// AsError matches an IAppError that is the domain error type 'E, yielding it typed, so a caller can match the exact
/// case (and bind its payload) instead of comparing DomainName and CaseName strings.
let (|AsError|_|) (e: IAppError) : 'E option =
    match box e with
    | :? 'E as typed -> Some typed
    | _ -> None
