module App.Operation.Audit

open System
open NodaTime
open App.Utility

type AuditableAction = // todo: turn AuditableAction into an interface and remove upper level domain knowledge 
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
    | CashFlowDeletePayment
    | CashFlowDeletePaymentAgreementLink
    | CashFlowTransitionPaymentsToPosted
    | CashFlowUpdateAgreement
    | CashFlowUpdateInvoice
    | CashFlowUpdatePaymentAgreementLink
    | ClassificationNewRule
    | ClassificationUpdateRule
    | FiscalPeriodCreate
    | FiscalPeriodClose
    | FiscalPeriodReopen
    | IngestClassifyAccounts
    | IngestDeduplicateStageEntries
    | IngestRawEntries
    | IngestNewSource
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
    let create
        (instant:Instant)
        (action: AuditableAction)
        : AuditEnvelope =
        { uniqueId = Guid.NewGuid(); action = action; instant = instant }
