module App.Utility.IAppError

open Microsoft.FSharp.Reflection

let getUnionCaseName (x: obj) =
      FSharpValue.GetUnionFields(x, x.GetType()) |> fst |> _.Name
      
type IAppError =
    abstract member ToMessage: unit -> string
    abstract member DomainName: string
    abstract member CaseName: string


