module Tests.Helpers.TestContext

open App.Operation.AuditEnvelope
open App.Session

/// updateInitiationInstant simulates a separate later operation on the same transaction: the context keeps its
/// transaction and audit action but takes a fresh initiation instant.
let updateInitiationInstant (oldContext: Context.Context) : Context.Context =
    let newEnvelope = oldContext.loggingContext.envelope |> AuditEnvelope.action |> AuditEnvelope.create
    { dataContext = oldContext.dataContext; loggingContext = { envelope = newEnvelope } }
