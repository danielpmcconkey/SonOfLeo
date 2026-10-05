module Ui.InterfaceBridge.Routes.PersonRoutes

open App.Utility.Result
open App.Utility.Json
open App.Utility.FieldUpdate
open App.Operation.CoreAuditableAction
open App.DataAccessLayer.DbTransaction
open App.Session
open Business.General.PersonComponent
open Business.General.Person
open Business.General.BizGeneralAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.InterfaceContracts.SharedContracts
open Ui.InterfaceBridge.InterfaceContracts.PersonContracts
open Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters
open Ui.InterfaceBridge.CommandRoute

let private create payload _ =
    runCommandRouteAndAutoCompleteTransaction PersonCreate (fun context ->
        result {
            let! input = Json.fromJson<PersonCreateInput> payload
            let! personName = input.personName |> PersonName.create
            let! person = PersonOrchestration.constructNewAndPersist context personName input.birthdate
            return! person |> ``convert [Person] to [PersonReturn]`` |> Json.toJson<PersonReturn>
        })

let private update payload _ =
    runCommandRouteAndAutoCompleteTransaction PersonUpdate (fun context ->
        result {
            let! input = Json.fromJson<PersonUpdateInput> payload
            let! personId = input.personName |> ``convert [PersonNameString] to [PersonId]`` context
            let! nameUpdate = input.personNameUpdate |> convertFieldUpdateToNewTypeFallible PersonName.create
            let! person =
                PersonOrchestration.updatePerson
                    context
                    { personIdToUpdate = personId; personNameUpdate = nameUpdate; birthdateUpdate = input.birthdateUpdate }
            return! person |> ``convert [Person] to [PersonReturn]`` |> Json.toJson<PersonReturn>
        })

let private list _ _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! persons = PersonOrchestration.listPersons context
        return! persons |> List.map ``convert [Person] to [PersonReturn]`` |> Json.toJson<PersonReturn list>
    }

let personDomainCommandRoutes =
    [ { domain = "Person"
        verb = "Create"
        description = "Create a Person from a name and a birthdate. Names are unique, compared exactly after trimming."
        inputContract = typeof<PersonCreateInput>.Name
        outputContract = typeof<PersonReturn>.Name
        handler = create }
      { domain = "Person"
        verb = "Update"
        description = "Update a Person's name and birthdate, the Person addressed by its current name."
        inputContract = typeof<PersonUpdateInput>.Name
        outputContract = typeof<PersonReturn>.Name
        handler = update }
      { domain = "Person"
        verb = "List"
        description = "List every Person with its name and birthdate, ordered by name. Read-only."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<PersonReturn list>.Name
        handler = list } ]
