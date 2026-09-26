module App.Operation.IAuditableAction

open Microsoft.FSharp.Reflection

let getUnionCaseName (x: obj) =
      FSharpValue.GetUnionFields(x, x.GetType()) |> fst |> _.Name
      
type IAuditableAction =
    abstract member CaseName: string

