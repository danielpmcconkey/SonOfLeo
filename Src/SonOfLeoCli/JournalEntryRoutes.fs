module SonOfLeoCli.JournalEntryRoutes

open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Model.UI
open Utilities.ResultCE
open InterfaceContractTypes

let private postNew payload _ =
    Error "Not yet implemented"
let private fetchById payload _ = Error "Not yet implemented"
let private fetchByPeriod payload _ = Error "Not yet implemented"
let private fetchByAccount payload _ = Error "Not yet implemented"
let private fetchByExternalReference payload _ = Error "Not yet implemented"
let private voidJe payload _ = Error "Not yet implemented"
let private updateExternalReference payload _ = Error "Not yet implemented"
let private addExternalReference payload _ = Error "Not yet implemented"
let private addComment payload _ = Error "Not yet implemented"
let private amendComment payload _ = Error "Not yet implemented"
    
let journalEntryDomainCommandRoutes = [
    { domain = "JournalEntry"; verb = "PostNew"; description = "Create a complete Journal Entry with all related objects (lines, references, comments)."
      inputType = typeof<JournalEntryInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  postNew } 
    { domain = "JournalEntry"; verb = "FetchById"; description = "Retrieve a complete Journal Entry based on its unique ID in the database."
      inputType = typeof<JournalEntryFetchByIdInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  fetchById } 
    { domain = "JournalEntry"; verb = "FetchByPeriod"; description = "Retrieve all Journal Entries (and related objects) for a given Fiscal Period."
      inputType = typeof<JournalEntryFetchByPeriodInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByPeriod } 
    { domain = "JournalEntry"; verb = "FetchByAccount"; description = "Retrieve all Journal Entries (and related objects) for a given Account."
      inputType = typeof<JournalEntryFetchByAccountInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByAccount } 
    { domain = "JournalEntry"; verb = "FetchByExternalReference"; description = "Retrieve all Journal Entries (and related objects) matching a specific External Account Reference (FI and reference)"
      inputType = typeof<JournalEntryFetchByExternalReferenceInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByExternalReference } 
    { domain = "JournalEntry"; verb = "Void"; description = "Void a Journal Entry by setting its “voided at” Instant to the system run time (requires a reason comment)"
      inputType = typeof<JournalEntryVoidInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  voidJe } 
    { domain = "JournalEntry"; verb = "UpdateExternalReference"; description = "Update an existing Journal Entry Extrenal Reference"
      inputType = typeof<JournalEntryUpdateExternalReferenceInput>.Name; outputType = typeof<JournalEntryExternalReferenceReturn>.Name; handler =  updateExternalReference } 
    { domain = "JournalEntry"; verb = "AddExternalReference"; description = "Add a new External Reference to an existing Journal Entry"
      inputType = typeof<JournalEntryAddExternalReferenceInput>.Name; outputType = typeof<JournalEntryExternalReferenceReturn>.Name; handler =  addExternalReference } 
    { domain = "JournalEntry"; verb = "AddComment"; description = "Add a new Comment to an existing Journal Entry"
      inputType = typeof<JournalEntryAddCommentInput>.Name; outputType = typeof<JournalEntryCommentReturn>.Name; handler =  addComment } 
    { domain = "JournalEntry"; verb = "AmendComment"; description = "Update an existing Journal Entry Comment"
      inputType = typeof<JournalEntryAmendCommentInput>.Name; outputType = typeof<JournalEntryCommentReturn>.Name; handler =  amendComment } 

]