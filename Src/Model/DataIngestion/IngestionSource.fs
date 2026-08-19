module Model.DataIngestion.IngestionSource

open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper

type IngestionSource =
    private {
        ingestionSourceId: IngestionSourceId
        name: JournalRefFinancialInstitution
        createdAt: Instant
        modifiedAt: Instant
    }

let ingestionSourceId s = s.ingestionSourceId
let name s = s.name
let createdAt s = s.createdAt
let modifiedAt s = s.modifiedAt

let create
    (ingestionSourceId: IngestionSourceId)
    (name: JournalRefFinancialInstitution)
    (createdAt: Instant)
    (modifiedAt: Instant)
    = {
        ingestionSourceId = ingestionSourceId
        name = name
        createdAt = createdAt
        modifiedAt = modifiedAt
    }
    
let insertNewToDb (context: Context.Context) (ingestionSource: IngestionSource) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.source(
	        unique_id, source_name, created_at, modified_at)
        values (
	        @unique_id, 
            @source_name, 
            @created_at, 
            @modified_at);"""
    let uuid = ingestionSource.ingestionSourceId |> IngestionSourceId.value
    let sourceName = ingestionSource.name |> JournalRefFinancialInstitution.value
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId(uuid) }
          { name = "@source_name"; value = CharString(sourceName) }
          { name = "@created_at"; value = DbInstant ingestionSource.createdAt }
          { name = "@modified_at"; value = DbInstant ingestionSource.modifiedAt }
        ]
    executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    
let private reconstitute raw =
    result {
        let (uuid,
             sourceString,
             createdAt,
             modifiedAt) =
            raw
        let ingestionSourceId = uuid |> IngestionSourceId.fromGuid
        let! name = sourceString |> JournalRefFinancialInstitution.create
        return
            create
                ingestionSourceId
                name
                createdAt
                modifiedAt
    }
    
let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "source_name"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private readRowsFromDb
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<IngestionSource list, AppError> =
    let select =
        """
        s.unique_id, s.source_name, s.created_at, s.modified_at
        """
    let from = "ingestion.source s"
    let query = buildReadQuery select from None predicate limit None None
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchByName (context: Context.Context) (name: JournalRefFinancialInstitution) : Result<IngestionSource, AppError> =
    let predicate = "s.source_name = @source_name"
    let nameStr = name |> JournalRefFinancialInstitution.value
    let parameters = [ { name = "@source_name"; value = CharString(nameStr) } ]
    match readRowsFromDb context (Some predicate) None parameters ExactlyOne with
    | Ok x -> x |> List.head |> Ok
    | Error(DalResultantRowsDidntMatchExpectation (_, 0)) -> Error (IngestionSourceNameNotFound nameStr)
    | Error e -> Error e

        
