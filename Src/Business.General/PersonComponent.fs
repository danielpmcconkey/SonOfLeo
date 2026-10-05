module Business.General.PersonComponent

open System
open App.Utility.IAppError
open Business.General.BizGeneralError

type PersonId = private PersonId of Guid
module PersonId =
    let create () : PersonId = PersonId(Guid.NewGuid())
    let fromGuid g = PersonId g
    let value (PersonId g) : Guid = g

type PersonName = private PersonName of string
module PersonName =
    let maxLength = 100
    let value (PersonName pn) = pn
    let create (raw: string) : Result<PersonName, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(PersonNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(PersonNameTooLong(raw, maxLength))
        else
            Ok(PersonName trimmed)
