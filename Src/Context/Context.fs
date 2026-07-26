module Context.Context

open DataAccessLayer.DbTransaction
open Logger.Audit

type DataContext = {
    dbTransaction: DbTransaction
}

type LoggingContext = {
    envelope: AuditEnvelope
}
