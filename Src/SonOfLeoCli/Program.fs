// For more information see https://aka.ms/fsharp-console-apps
open System
open Model.Ledger.Account
let result = Account.fetchById (Guid.Parse("f6b34177-0b6b-48cc-9a47-3fa2cb232093"))
printfn "%A" result
// |> Result.bind( fun parent ->
//     Account.createFromPrimitivesAndSaveToDb "1100" "Rad skis" Asset Some true Some (Account.id parent) "snow skis" None None
//     |> Result.map(fun () -> ())
// )


