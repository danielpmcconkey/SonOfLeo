module Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters

open App.Utility.IAppError
open App.Utility.Result
open Business.General.Person
open Ui.InterfaceBridge.InterfaceContracts.PersonContracts

let ``convert [Person] to [PersonReturn]`` (person: Person) : PersonReturn =
    { personName = person |> personName |> PersonName.value
      birthdate = person |> birthdate
      createdAt = person |> createdAt
      modifiedAt = person |> modifiedAt }

let ``convert [string list] to [PersonName list]`` (names: string list) : Result<PersonName list, IAppError> =
    names |> List.map PersonName.create |> convertListOfResultsToResultsList
