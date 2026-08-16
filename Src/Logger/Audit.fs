module Logger.Audit


open System
open NodaTime
open Utilities

type AuditableAction =
    | FetchOnly
    | AccountCreate
    | AccountUpdateName
    | AccountUpdateExtReference
    | AccountDeactivate
    | FiscalPeriodCreate
    | FiscalPeriodClose
    | FiscalPeriodReopen
    | IngestRawEntries
    | IngestNewClassificationRule
    | IngestNewSource
    | IngestUpdateStageEntry
    | JournalEntryPostNew
    | JournalEntryVoid
    | JournalEntryUpdateExternalReference
    | JournalEntryAddExternalReference
    | JournalEntryAddComment
    | JournalEntryUpdateComment

type AuditEnvelope =
    private // intentionally private to prevent tampering
        { uniqueId: Guid
          action: AuditableAction
          instant: Instant
        // todo: add input params to AuditEnvelope as json
        }

module AuditEnvelope =
    let uniqueId (e: AuditEnvelope) = e.uniqueId
    let action (e: AuditEnvelope) = e.action
    let instant (e: AuditEnvelope) = e.instant

    // todo: create an actual audit log that appends a log file on AuditEnvelope create
    let create (action: AuditableAction) : AuditEnvelope =
        { uniqueId = Guid.NewGuid(); action = action; instant = Clock.now() }
