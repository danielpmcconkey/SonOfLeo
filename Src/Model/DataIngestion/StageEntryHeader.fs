module Model.DataIngestion.StageEntryHeader

open Context.Context
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.DataIngestion.IngestionSource
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper

type StageEntryHeader =
    private {
        sourceFile: SourceFile
        stageEntryHeaderId : StageEntryHeaderId
        entryDate : LocalDate
        description: JournalEntryDescription
        ingestionSource: IngestionSource
        fiReference: JournalExternalReferenceText
        status: StagedEntryStatus 
    }

let sourceFile g = g.sourceFile
let stageEntryHeaderId g = g.stageEntryHeaderId
let entryDate g = g.entryDate
let description g = g.description
let ingestionSource g = g.ingestionSource
let fiReference g = g.fiReference
let status g = g.status
    
let create
    (sourceFile: SourceFile)
    (stageEntryHeaderId : StageEntryHeaderId)
    (entryDate : LocalDate)
    (description: JournalEntryDescription)
    (ingestionSource: IngestionSource)
    (fiReference: JournalExternalReferenceText)
    (status: StagedEntryStatus)
    : StageEntryHeader = {
        sourceFile = sourceFile
        stageEntryHeaderId = stageEntryHeaderId
        entryDate = entryDate
        description = description
        ingestionSource = ingestionSource
        fiReference = fiReference
        status = status }

let insertNewToDb (context: Context) (stageEntryHeader: StageEntryHeader) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.staged_entry(
	        unique_id, entry_date, description, source_id, fi_reference, source_file, status)
        values (
	        @unique_id, 
            @entry_date, 
            @description, 
            @source_id, 
            @fi_reference,
            @source_file,
            @status);"""
    let uuid = stageEntryHeader.stageEntryHeaderId |> StageEntryHeaderId.value
    let description = stageEntryHeader.description |> JournalEntryDescription.value
    let sourceUuid = stageEntryHeader.ingestionSource |> ingestionSourceId |> IngestionSourceId.value
    let fiReference = stageEntryHeader.fiReference |> JournalExternalReferenceText.value
    let sourceFile = stageEntryHeader.sourceFile |> SourceFile.value
    let status = stageEntryHeader.status |> StagedEntryStatus.toString
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId(uuid) }
          { name = "@entry_date"; value = DbLocalDate(stageEntryHeader.entryDate) }
          { name = "@description"; value = CharString(description) }
          { name = "@source_id"; value = UniqueId(sourceUuid) }
          { name = "@fi_reference"; value = CharString(fiReference) }
          { name = "@source_file"; value = CharString(sourceFile) }
          { name = "@status"; value = CharString(status) }
        ]
    executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        
let private reconstitute raw =
    result {
        let (sourceFileStr,
             uuid,
             entryDate,
             descriptionStr,
             sourceUuid,
             sourceNameStr,
             sourceCreated,
             sourceModified,
             fiReferenceStr,
             statusStr) =
            raw
        let! sourceFile = sourceFileStr |> SourceFile.create
        let stageEntryHeaderId = uuid |> StageEntryHeaderId.fromGuid
        let! description = descriptionStr |> JournalEntryDescription.create
        let ingestionSourceId = sourceUuid |> IngestionSourceId.fromGuid
        let! sourceName = sourceNameStr |> JournalRefFinancialInstitution.create
        let ingestionSource = IngestionSource.create ingestionSourceId sourceName sourceCreated sourceModified
        let! fiReference = fiReferenceStr |> JournalExternalReferenceText.create
        let! status = statusStr |> StagedEntryStatus.fromString
        return
            create
                sourceFile
                stageEntryHeaderId
                entryDate
                description
                ingestionSource
                fiReference
                status
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getString "source_file"),
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getDate "entry_date"),
    (row |> RowReader.getString "description"),
    (row |> RowReader.getUuid "source_id"),
    (row |> RowReader.getString "source_name"),
    (row |> RowReader.getInstant "source_created"),
    (row |> RowReader.getInstant "source_modified"),
    (row |> RowReader.getString "fi_reference"),
    (row |> RowReader.getString "status")
    
let private readRowsFromDb
    (context: Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<StageEntryHeader list, AppError> =
    let select =
        """
        e.unique_id, e.entry_date, e.description, e.source_id, e.fi_reference, e.source_file, e.status
        s.source_name, s.created_at as source_created, s.modified_at as source_modified
        """
    let from = "ingestion.staged_entry e"
    let join = "left join ingestion.source s on e.source_id = s.unique_id"
    let query = buildReadQuery select from (Some join) predicate limit None None
    executeReaderQuery
        (context |> getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context) (accountId: StageEntryHeaderId) : Result<StageEntryHeader, AppError> =
    let predicate = "e.unique_id = @unique_id"
    let accountIdGuid = accountId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ]
    readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByStatus (context: Context) (status: StagedEntryStatus) : Result<StageEntryHeader list, AppError> =
    let predicate = "e.status = @status"
    let statusStr = status |> StagedEntryStatus.toString
    let parameters = [ { name = "@status"; value = CharString statusStr } ]
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

let private updateDb
    (context: Context)
    (sourceFileUpdate: FieldUpdate<SourceFile>)
    (entryDateUpdate: FieldUpdate<LocalDate>)
    (descriptionUpdate: FieldUpdate<JournalEntryDescription>)
    (ingestionSourceUpdate: FieldUpdate<IngestionSource>)
    (fiReferenceUpdate: FieldUpdate<JournalExternalReferenceText>)
    (statusUpdate: FieldUpdate<StagedEntryStatus>)
    (stageEntryHeaderId : StageEntryHeaderId)
    : Result<StageEntryHeader, AppError> =
    let uuid = stageEntryHeaderId |> StageEntryHeaderId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              sourceFileUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  (", source_file = @source_file",
                   { name = "@source_file"; value = CharString(SourceFile.value n) }))
              
              entryDateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  (", entry_date = @entry_date",
                   { name = "@entry_date"; value = DbLocalDate(n) }))
              
              descriptionUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  (", description = @description",
                   { name = "@description"; value = CharString(JournalEntryDescription.value n) }))
              
              ingestionSourceUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let uuid = n |> ingestionSourceId |> IngestionSourceId.value
                  (", source_id = @source_id",
                   { name = "@source_id"; value = UniqueId(uuid) }))
              
              fiReferenceUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  (", fi_reference = @fi_reference",
                   { name = "@fi_reference"; value = CharString(JournalExternalReferenceText.value n) }))
              
              statusUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  (", status = @status",
                   { name = "@status"; value = CharString(StagedEntryStatus.toString n) }))
        ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ""
    let parameters = baseParams @ (updates |> List.map snd)

    let query =
        $"""
        UPDATE ledger.account
        set
            modified_at = @modified
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates.IsEmpty then Error(AccountUpdateNoOp) else Ok()
        let! () = executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        return! stageEntryHeaderId |> fetchById context
    }

/// updateStatus assumes the orchestrator is validating the status change and adding a record to the audit table 
let private updateStatus
    (context: Context)
    (newStatus: StagedEntryStatus)
    (stageEntryHeaderId : StageEntryHeaderId)
    : Result<StageEntryHeader, AppError> =
    stageEntryHeaderId
    |> updateDb context NoChange NoChange NoChange NoChange NoChange (SetTo newStatus)
    
