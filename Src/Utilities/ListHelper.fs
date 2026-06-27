module Utilities.ListHelper

let listOfResultsToResultsList<'T>
        (listOfResults: Result<'T,string> list)
        : Result<'T list, string> =
    listOfResults
    |> List.foldBack (fun createResult acc ->
            match createResult, acc with
            | Ok validCr, Ok validAcc -> Ok (validCr :: validAcc)
            | Error e, _ -> Error e
            | _, Error e -> Error e
            ) <| Ok []
    