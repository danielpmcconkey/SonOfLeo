module App.Session.Context

open App.DataAccessLayer.DbTransaction
open App.Operation.Audit
open App.Utility.AppError

type DataContext = { dbTransaction: DbTransaction }

type LoggingContext = { envelope: AuditEnvelope }

// todo: add a user context so Jodi can use this system too someday

type Context = { dataContext: DataContext; loggingContext: LoggingContext }

let create transactionNeed auditAction instant =
    let dbTransaction =
        match transactionNeed with
        | NoTransaction -> createNoTransaction()
        | NewTransaction -> createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)) // we throw here to avoid complicated error unwinding at the head of every method
        | ExistingTransaction x -> x
    let envelope = auditAction |> AuditEnvelope.create instant
    { dataContext = { dbTransaction = dbTransaction }; loggingContext = { envelope = envelope } }

let getDatabaseTransaction c = c.dataContext.dbTransaction

let getInitiationInstant c =
    c.loggingContext.envelope |> AuditEnvelope.instant

/// updateInitiationInstant is used for long orchestrated events where you need tasks to show the order of operations
/// through their logging
let updateInitiationInstant instant oldContext =
    let newEnvelope = oldContext.loggingContext.envelope |> AuditEnvelope.action |> AuditEnvelope.create instant
    { dataContext = oldContext.dataContext; loggingContext = { envelope = newEnvelope } }
