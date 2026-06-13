namespace Model.Audit

open System
open NodaTime
open Utilities.Clock


    
type AuditableAction =  
    | AccountCreate
    | AccountUpdateName
    | AccountUpdateExtReference
    | AccountDeactivation
    
type AuditEnvelope =
    private { // intentionally private to prevent tampering
        id: Guid
        action: AuditableAction
        instant: Instant
        // todo: add input params to AuditEnvelope as json
    }

module AuditEnvelope =
    let id (e:AuditEnvelope) = e.id
    let action (e:AuditEnvelope) = e.action
    let instant (e:AuditEnvelope) = e.instant
    
    // todo: create an actual audit log that appends a log file on AuditEnvelope create
    let create (action: AuditableAction) : AuditEnvelope =  {
            id = Guid.NewGuid()
            action = action
            instant = Clock.now()
        }
        
