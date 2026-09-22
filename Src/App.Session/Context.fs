module App.Session.Context

open App.DataAccessLayer.DbTransaction
open App.Operation.Audit

type DataContext = { dbTransaction: DbTransaction }

type LoggingContext = { envelope: AuditEnvelope }

type Context = { dataContext: DataContext; loggingContext: LoggingContext }

let create transactionNeed auditAction  =
    let dbTransaction =
        match transactionNeed with
        | NoTransaction -> createNoTransaction()
        | NewTransaction ->
            createDbTransaction()
            |> Result.defaultWith(fun e -> failwith(e.ToMessage())) // we throw here to avoid complicated error unwinding at the head of every method
        | ExistingTransaction x -> x
    let envelope = auditAction |> AuditEnvelope.create 
    { dataContext = { dbTransaction = dbTransaction }; loggingContext = { envelope = envelope } }

let getDatabaseTransaction c = c.dataContext.dbTransaction

let getInitiationInstant c =
    c.loggingContext.envelope |> AuditEnvelope.instant

/// updateInitiationInstant is used for long orchestrated events where you need tasks to show the order of operations
/// through their logging
let updateInitiationInstant oldContext =
    let newEnvelope = oldContext.loggingContext.envelope |> AuditEnvelope.action |> AuditEnvelope.create
    { dataContext = oldContext.dataContext; loggingContext = { envelope = newEnvelope } }
