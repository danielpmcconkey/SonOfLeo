module Business.FinancialServices.DataIngestion.DataIngestionAuditableAction

open App.Operation.IAuditableAction

type DataIngestionAuditableAction = 
    | IngestDeduplicateStageEntries
    | IngestRawEntries
    | IngestNewSource
    | IngestUpdateStageEntry
    | IngestPostStageEntries
    | IngestShadowPostStageEntries
    
    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this

