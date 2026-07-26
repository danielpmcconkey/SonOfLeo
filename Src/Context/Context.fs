module Context.Context

open DataAccessLayer.DbTransaction
open Logger.Audit
open Utilities.AppError

type DataContext = { dbTransaction: DbTransaction }

type LoggingContext = { envelope: AuditEnvelope }

// todo: add a user context so Jodi can use this system too someday

type Context = { dataContext: DataContext; loggingContext: LoggingContext }

let create transactionNeed auditAction =
    let dbTransaction =
        match transactionNeed with
        | NoTransaction -> createNoTransaction()
        | NewTransaction -> createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)) // we throw here to avoid complicated error unwinding at the head of every method
        | ExistingTransaction x -> x
    let envelope = auditAction |> AuditEnvelope.create
    { dataContext = { dbTransaction = dbTransaction }; loggingContext = { envelope = envelope } }

let getDatabaseTransaction c = c.dataContext.dbTransaction

let getInitiationInstant c =
    c.loggingContext.envelope |> AuditEnvelope.instant
