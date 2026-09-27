module Business.FinancialServices.DataIngestion.DataIngestionAuditableAction

open App.Operation.IAuditableAction

type DataIngestionAuditableAction = 
    | IngestDeduplicateStageEntries
    | IngestRawEntries
    | IngestNewSource
    | IngestUpdateStageEntry
    | IngestPostStageEntries
    | IngestShadowPostStageEntries
    | IngestShadowReconcile
    
    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this

