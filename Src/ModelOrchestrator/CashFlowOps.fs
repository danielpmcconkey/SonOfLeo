module ModelOrchestrator.CashFlowOps

open System
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
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
                let instanceId = InstanceId.create()
                let now = context |> Context.getInitiationInstant
                let newInstance = Instance.create instanceId agreementId neededDate false now now
                newInstance |> Instance.insertNewToDb context
                )
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
    

let ProjectionSweep
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    : Result<AgreementOrchestration.Agreement list, AppError> =
    result {
    // Takes a horizon (days). Walks every active agreement's cadence, creates missing Instances, creates Invoices for
    // fixed-amount PAs. Returns what it created and what already existed.
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        return! agreements |> spawnInstancesFromAgreements context daysOut
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
