module Ui.InterfaceBridge.InterfaceContracts.PersonContracts

open NodaTime
open App.Utility.FieldUpdate

type PersonReturn = {
    personName: string
    birthdate: LocalDate
    createdAt: Instant
    modifiedAt: Instant
}

type PersonCreateInput = { personName: string; birthdate: LocalDate }

// the Person is addressed by its current name
type PersonUpdateInput = {
    personName: string
    personNameUpdate: FieldUpdate<string>
    birthdateUpdate: FieldUpdate<LocalDate>
}
