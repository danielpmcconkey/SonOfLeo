module InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters

open Model.Ledger.Journaling.JournalEntryComponent
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers

let convertJeDescriptionStringOptionToJeDescriptionOption
    (stringOption: string option)
    : Result<JournalEntryDescription option, string> =
    let fallibleConverter = (fun string -> string |> JournalEntryDescription.create)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let convertJeSourceStringOptionToJeSourceOption
    (stringOption: string option)
    : Result<JournalEntrySource option, string> =
    let fallibleConverter = (fun string -> string |> JournalEntrySource.create)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter



