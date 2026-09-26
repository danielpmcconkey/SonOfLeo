module App.Operation.AuditEnvelope

open System
open App.Operation.IAuditableAction
open NodaTime
open App.Utility

type AuditEnvelope =
    private
        { uniqueId: Guid
          action: IAuditableAction
          instant: Instant
        }

module AuditEnvelope =
    let uniqueId (e: AuditEnvelope) = e.uniqueId
    let action (e: AuditEnvelope) = e.action
    let instant (e: AuditEnvelope) = e.instant

    // todo: create an actual audit log that appends a log file on AuditEnvelope create
    let create
        (action: IAuditableAction)
        : AuditEnvelope =
        { uniqueId = Guid.NewGuid(); action = action; instant = Clock.now() }
