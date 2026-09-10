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
    | CashFlowClassifyPaymentAgreements
    | CashFlowCreateAgreement
    | CashFlowCreateInstance
    | CashFlowCreateInvoice
    | CashFlowCreatePayment
    | CashFlowCreatePaymentAgreementLink
    | CashFlowCreateUpcomingInstances
    | CashFlowDeletePaymentAgreementLink
    | CashFlowUpdateAgreement
    | CashFlowUpdateInvoice
    | CashFlowUpdatePaymentAgreementLink
    | FiscalPeriodCreate
    | FiscalPeriodClose
    | FiscalPeriodReopen
    | IngestClassifyAccounts
    | IngestDeduplicateStageEntries
    | IngestRawEntries
    | IngestNewClassificationRule
    | IngestNewSource
    | IngestUpdateClassificationRule
    | IngestUpdateStageEntry
    | IngestPostStageEntries
    | IngestShadowPostStageEntries
    | JournalEntryPostNew
    | JournalEntryVoid
    | JournalEntryUpdateExternalReference
    | JournalEntryAddExternalReference
    | JournalEntryAddComment
    | JournalEntryUpdateComment

type AuditEnvelope =
    private
        { uniqueId: Guid
          action: AuditableAction
          instant: Instant
        }

module AuditEnvelope =
    let uniqueId (e: AuditEnvelope) = e.uniqueId
    let action (e: AuditEnvelope) = e.action
    let instant (e: AuditEnvelope) = e.instant

    // todo: create an actual audit log that appends a log file on AuditEnvelope create
    let create (action: AuditableAction) : AuditEnvelope =
        { uniqueId = Guid.NewGuid(); action = action; instant = Clock.now() }
