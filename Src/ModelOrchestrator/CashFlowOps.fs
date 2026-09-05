module ModelOrchestrator.CashFlowOps

open System
open DataAccessLayer.ExecuteReader
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.Ledger.JournalEntryExternalReference
open ModelOrchestrator
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.ResultHelper

(*
    CashFlowOps represents the activities that the operator will perform every time we run finances (the saturday
    routine)
*)
    
let rec private fillInstanceDatesToCutOff
    (nextDate: LocalDate)
    (cutOffDate: LocalDate)
    (cadenceType: Cadence.CadenceType)
    (accumulator: LocalDate list)
    : LocalDate list =
    if nextDate > cutOffDate then accumulator // break out of the recursion
    else 
    let nextNextDate = Cadence.determineNextDateFromPrior nextDate cadenceType
    fillInstanceDatesToCutOff nextNextDate cutOffDate cadenceType (nextDate::accumulator)

let private spawnInstancesFromAgreement
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    (agreement: AgreementOrchestration.Agreement)
    : Result<AgreementOrchestration.Agreement, AppError> =
    result {
        let cutOffDate = Calendar.today().PlusDays(daysOut |> ProjectionHorizonInDays.value)
        let master = agreement |> AgreementOrchestration.masterAgreement
        let agreementId = master |> MasterAgreement.agreementID
        let cadence = master |> MasterAgreement.cadence
        let cadenceType = cadence |> Cadence.cadenceType
        let nextInstance = cadence |> Cadence.nextInstance
        let nextInstanceDate = nextInstance.nextInstance
        let neededDates =
            fillInstanceDatesToCutOff nextInstanceDate cutOffDate cadenceType []
            |> List.sortByDescending id
        if neededDates |> List.isEmpty then return agreement else
        do! neededDates
            |> List.map(fun neededDate ->
                InstanceOrchestration.createInstanceCompositeAndSaveToDb
                        context agreementId neededDate false [])
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let latestAdded = neededDates |> List.head
        let newNextInstance = Cadence.determineNextDateFromPrior latestAdded cadenceType
        let! newCadence = Cadence.create cadenceType { nextInstance = newNextInstance }
        do! master |> MasterAgreement.updateCadence context newCadence |> Result.map ignore
        return! AgreementOrchestration.fetchByMasterAgreementId context agreementId
    }

let private spawnInstancesFromAgreements
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    (agreements: AgreementOrchestration.Agreement list)
    : Result<AgreementOrchestration.Agreement list, AppError> =
    agreements
    |> List.map (spawnInstancesFromAgreement context daysOut)
    |> convertListOfResultsToResultsList
    

let projectionSweep // step 2.2.3.0
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    : Result<AgreementOrchestration.Agreement list, AppError> =
    result {
    // Walk every active agreement's cadence, create missing Instances through the horizon. Advance each cadence's
    // nextInstance pointer. Return count of instances created per agreement. Invoices are NOT created here.
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        return! agreements |> spawnInstancesFromAgreements context daysOut
    }

let private createPaymentsToInvoiceFromLedgerAndStageEntries
    (context: Context.Context)
    (gracePeriod: int)
    (invoice: Invoice.Invoice) =
    // todo: this is just placeholder to get the thing to build. We need to create a classifier match rule on description
    let externalInvoiceId = invoice |> Invoice.externalInvoiceId
    let minDate = (invoice |> Invoice.invoiceDate).localDate
    let maxDate = (invoice |> Invoice.dueDate).localDate.PlusDays(gracePeriod)
    let filter:FetchFilters.JournalEntryFetchFilter = {
        journalEntryHeaderId = None
        source = None
        financialInstitution = None
        referenceText = None
        temporalFilter = None
        unVoidedOnly = true
    }
    let journalEntriesThatMatch = ModelOrchestrator.JournalEntries.JournalEntry.fetchFiltered context filter AnyQuantityIsAcceptable
    journalEntriesThatMatch

let private createPaymentsToInvoicesFromLedgerAndStageEntries
    (context: Context.Context)
    (gracePeriod: int)
    (invoicesInOpenInstances: Invoice.Invoice list) =
    invoicesInOpenInstances |> List.map (createPaymentsToInvoiceFromLedgerAndStageEntries context gracePeriod)
   
let private matchInvoicesToStageEntriesForOneAgreement
    (context: Context.Context)
    (agreement: AgreementOrchestration.Agreement)
    : Result<unit, AppError> =
    result {
        let masterAgreement = agreement |> AgreementOrchestration.masterAgreement
        let cadence = masterAgreement |> MasterAgreement.cadence
        let gracePeriod = // the number of days away from the invoice due date we check
            match cadence |> Cadence.cadenceType with
            | Cadence.Daily -> 0
            | Cadence.Weekly _ -> 2
            | Cadence.EveryOtherWeek _ -> 4
            | Cadence.Monthly _ -> 7
            | Cadence.Annually _ -> 7
        let paymentAgreements = agreement |> AgreementOrchestration.paymentAgreements
        let openInstances =
            agreement
            |> AgreementOrchestration.instances
            |> List.filter (fun instance -> instance |> Instance.isFulfilled = false)
        let openInstanceIds = openInstances |> List.map(fun instance -> instance |> Instance.instanceId)
        let invoicesInOpenInstances =
            agreement
            |> AgreementOrchestration.invoices
            |> List.filter(fun invoice -> openInstanceIds |> List.contains (invoice |> Invoice.instanceId))
        do! invoicesInOpenInstances
            |> createPaymentsToInvoicesFromLedgerAndStageEntries context gracePeriod
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return ()
    }
   
let matchInvoicesToStageEntriesForAllAgreements // 2.2.5.0
    (context: Context.Context)
    : Result<AgreementOrchestration.Agreement list, AppError> =
    result {
    // For each unpaid Invoice: (a) match by external invoice ID against stage entry and JE external references, (b)
    // match by PA account pattern + date window for obligations without external IDs. For unambiguous single-candidate
    // matches: auto-create Payment linking Invoice to stage entry, derive paymentState. Return structured result:
    // { autoMatched, ambiguous, unfulfilled, unlinkedStaged }. Ambiguous and unlinked lists feed step 3.0.
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        do! agreements
            |> List.map (matchInvoicesToStageEntriesForOneAgreement context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        // re-fetch just to make sure we have the complete new state
        return! AgreementOrchestration.fetchAllActiveAgreements context
    }

let Projection() =
    // Takes a horizon. Reads ledger balances + open invoices. Returns per-account `{ currentBalance, knownInflows,
    // knownOutflows, projectedLow }` + `billsToChase` (instances with no invoice).
    raise(NotImplementedException())

let TransitionPaymentsToPosted() =
    // No input. For every Payment pointing at a staged entry that now has a JE, transitions pointer to Posted + updates
    // invoice posted state. Returns the list.
    raise(NotImplementedException())

let StagedEntryMatchCandidates() =
    // Given an invoice or agreement, returns unlinked staged entries matching the PA's account pattern within the
    // instance's date window. Candidates only — Hobson decides. 
    raise(NotImplementedException())

let AgreementSummary() =
    // Full tree view: master → PAs → recent instances → invoices → payments. 
    raise(NotImplementedException())
