namespace Model.Ledger.Journaling

open NodaTime

type JournalEntryComment =
  private  {    createdAt: Instant
                modifiedAt: Instant }

module JournalEntryComment =
    let createdAt jer = jer.createdAt
    let modifiedAt jer = jer.modifiedAt
    


