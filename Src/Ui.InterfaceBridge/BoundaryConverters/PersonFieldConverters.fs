module Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters

open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General.BizGeneralError
open Business.General.PersonComponent
open Business.General.Person
open Ui.InterfaceBridge.InterfaceContracts.PersonContracts

let ``convert [Person] to [PersonReturn]`` (person: Person) : PersonReturn =
    { personName = person |> personName |> PersonName.value
      birthdate = person |> birthdate
      createdAt = person |> createdAt
      modifiedAt = person |> modifiedAt }

let ``convert [PersonNameString] to [PersonId]`` (context: Context.Context) (nameString: string) : Result<PersonId, IAppError> =
    result {
        let! name = nameString |> PersonName.create
        let! found = name |> fetchByName context
        match found with
        | Some person -> return person |> personId
        | None -> return! Error(PersonNameDoesntMatchId(name |> PersonName.value))
    }

let ``convert [PersonNameString list] to [PersonId list]``
    (context: Context.Context)
    (names: string list)
    : Result<PersonId list, IAppError> =
    names |> List.map (``convert [PersonNameString] to [PersonId]`` context) |> convertListOfResultsToResultsList
