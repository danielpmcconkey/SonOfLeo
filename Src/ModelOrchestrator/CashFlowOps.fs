module ModelOrchestrator.CashFlowOps

open System

(*
    CashFlowOps represents the activities that the operator will perform every time we run finances (the saturday
    routine)
*)

let ProjectionSweep =
    // Takes a horizon (days). Walks every active agreement's cadence, creates missing Instances, creates Invoices for
    // fixed-amount PAs. Returns what it created and what already existed.
    raise(NotImplementedException())

let Projection =
    // Takes a horizon. Reads ledger balances + open invoices. Returns per-account `{ currentBalance, knownInflows,
    // knownOutflows, projectedLow }` + `billsToChase` (instances with no invoice).
    raise(NotImplementedException())

let TransitionPaymentsToPosted =
    // No input. For every Payment pointing at a staged entry that now has a JE, transitions pointer to Posted + updates
    // invoice posted state. Returns the list.
    raise(NotImplementedException())

let StagedEntryMatchCandidates =
    // Given an invoice or agreement, returns unlinked staged entries matching the PA's account pattern within the
    // instance's date window. Candidates only — Hobson decides. 
    raise(NotImplementedException())

let AgreementSummary =
    // Full tree view: master → PAs → recent instances → invoices → payments. 
    raise(NotImplementedException())
