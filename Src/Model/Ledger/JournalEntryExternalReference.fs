namespace Model.Ledger

open NodaTime
type JournalEntryExternalReference =
  private  {    createdAt: Instant
                modifiedAt: Instant }

module JournalEntryExternalReference =
    let createdAt jer = jer.createdAt
    let modifiedAt jer = jer.modifiedAt
    


