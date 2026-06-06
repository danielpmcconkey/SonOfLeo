// For more information see https://aka.ms/fsharp-console-apps
open Model.Ledger.Account
let result = Account.createFromPrimitivesAndSaveToDb
                 "1000" "Assets" "Asset" None None None None
printfn "%A" result
// |> Result.bind( fun parent ->
//     Account.createFromPrimitivesAndSaveToDb "1100" "Rad skis" Asset Some true Some (Account.id parent) "snow skis" None None
//     |> Result.map(fun () -> ())
// )


