module Model.DataIngestion.StageEntryHeader

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
        /// currentStatus is not a column in the database. it is a read-time cache of the latest status. The source of
        /// truth is the table of status transitions. This field is here for read convenience.
        currentStatus: StagedEntryStatus option
    }

type StageEntryHeaderFieldUpdates = {
    headerIdToUpdate: StageEntryHeaderId
    sourceFileUpdate: FieldUpdate<SourceFile>
    entryDateUpdate: FieldUpdate<LocalDate>
    descriptionUpdate: FieldUpdate<JournalEntryDescription>
    ingestionSourceUpdate: FieldUpdate<IngestionSource>
    fiReferenceUpdate: FieldUpdate<JournalExternalReferenceText>
    statusUpdate: FieldUpdate<StagedEntryStatus * StageStatusChangeMechanism> }

let sourceFile g = g.sourceFile
let stageEntryHeaderId g = g.stageEntryHeaderId
let entryDate g = g.entryDate
let description g = g.description
let ingestionSource g = g.ingestionSource
let fiReference g = g.fiReference
let currentStatus g = g.currentStatus
    
let create
    (sourceFile: SourceFile)
    (stageEntryHeaderId : StageEntryHeaderId)
    (entryDate : LocalDate)
    (description: JournalEntryDescription)
    (ingestionSource: IngestionSource)
    (fiReference: JournalExternalReferenceText)
    (currentStatus: StagedEntryStatus option)
    : StageEntryHeader = {
        sourceFile = sourceFile
        stageEntryHeaderId = stageEntryHeaderId
        entryDate = entryDate
        description = description
        ingestionSource = ingestionSource
        fiReference = fiReference
        currentStatus = currentStatus }

let insertNewStatusTransitionToDb
    (context: Context.Context)
    (stageEntryStatusTransition: StageEntryStatusTransition.StageEntryStatusTransition)
    : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.staged_entry_audit(
	        unique_id, entry_id, from_status, to_status, modified_at, change_mechanism)
        values (
	        @unique_id, 
            @entry_id, 
            @from_status, 
            @to_status, 
            @modified_at,
            @change_mechanism);"""
    let uuid = stageEntryStatusTransition
               |> StageEntryStatusTransition.stageEntryStatusTransitionId
               |> StageEntryStatusTransitionId.value
    let headerUuid =
               stageEntryStatusTransition
               |> StageEntryStatusTransition.stageEntryHeaderId 
               |> StageEntryHeaderId.value
    let fromStatus =
               stageEntryStatusTransition
               |> StageEntryStatusTransition.fromStatus
               |> Option.map StagedEntryStatus.toString
    let toStatus =
               stageEntryStatusTransition
               |> StageEntryStatusTransition.toStatus
               |> StagedEntryStatus.toString
    let stageStatusChangeMechanism =
               stageEntryStatusTransition
               |> StageEntryStatusTransition.stageStatusChangeMechanism
               |> StageStatusChangeMechanism.toString
    let instant =
               stageEntryStatusTransition
               |> StageEntryStatusTransition.instant
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId(uuid) }
          { name = "@entry_id"; value = UniqueId(headerUuid) }
          { name = "@from_status"; value = NullableCharString(fromStatus) }
          { name = "@to_status"; value = CharString(toStatus) }
          { name = "@modified_at"; value = DbInstant(instant) }
          { name = "@change_mechanism"; value = CharString(stageStatusChangeMechanism) }
        ]
    executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        
let updateHeaderStatus
    (context: Context.Context)
    (newStatus: StagedEntryStatus)
    (mechanism: StageStatusChangeMechanism)
    (headerId: StageEntryHeaderId)
    : Result<unit, AppError> =
    result {
        let! statusTransitions = headerId |> StageEntryStatusTransition.fetchByHeaderId context
        let fromStatus =
            if statusTransitions |> List.isEmpty then None
            else 
            statusTransitions
            |> List.sortByDescending(fun s -> s |> StageEntryStatusTransition.instant)
            |> List.head
            |> StageEntryStatusTransition.toStatus
            |> Some
        let newTransitionId = StageEntryStatusTransitionId.create()
        let instant = context |> Context.getInitiationInstant
        let newTransition =
            StageEntryStatusTransition.create newTransitionId headerId
                fromStatus newStatus instant mechanism
        do! newTransition |> StageEntryStatusTransition.confirmValidTransition
        return! newTransition |> insertNewStatusTransitionToDb context
    }

let insertNewToDb
    (context: Context.Context)
    (initialStatus: StagedEntryStatus)
    (statusChangeMechanism: StageStatusChangeMechanism)
    (stageEntryHeader: StageEntryHeader)
    : Result<unit, AppError> =
    result {
        let query =
            """
            insert into ingestion.staged_entry(
	            unique_id, entry_date, description, source_id, fi_reference, source_file)
            values (
	            @unique_id, 
                @entry_date, 
                @description, 
                @source_id, 
                @fi_reference,
                @source_file);"""
        let uuid = stageEntryHeader.stageEntryHeaderId |> StageEntryHeaderId.value
        let description = stageEntryHeader.description |> JournalEntryDescription.value
        let sourceUuid = stageEntryHeader.ingestionSource |> ingestionSourceId |> IngestionSourceId.value
        let fiReference = stageEntryHeader.fiReference |> JournalExternalReferenceText.value
        let sourceFile = stageEntryHeader.sourceFile |> SourceFile.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@entry_date"; value = DbLocalDate(stageEntryHeader.entryDate) }
              { name = "@description"; value = CharString(description) }
              { name = "@source_id"; value = UniqueId(sourceUuid) }
              { name = "@fi_reference"; value = CharString(fiReference) }
              { name = "@source_file"; value = CharString(sourceFile) }
            ]
        do! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! stageEntryHeader.stageEntryHeaderId
            |> updateHeaderStatus context initialStatus statusChangeMechanism
    }
        
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
        let! status = statusStr |> convertOptionToDesiredTypeWithFallibleConverter StagedEntryStatus.fromString
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
    (row |> RowReader.getStringOption "current_status")

let readRowsFromDb
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<StageEntryHeader list, AppError> =
    let from = "ingestion.staged_entry e"
    let query = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchGenericRead
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
      : Result<StageEntryHeader list, AppError> =    
    let latestStatusCtes = StageEntryStatusTransition.formLatestStatusCte
    let select = """
        e.unique_id, e.entry_date, e.description, e.source_id, e.fi_reference, e.source_file, 
        latest_statuses.to_status as current_status, s.source_name, s.created_at as source_created,
        s.modified_at as source_modified
        """
    let joinList =
        [
            "left join ingestion.source s on e.source_id = s.unique_id"
            "left join latest_statuses on e.unique_id = latest_statuses.entry_id"
        ]
    readRowsFromDb context (Some latestStatusCtes) select (Some joinList) predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (headerId: StageEntryHeaderId) : Result<StageEntryHeader, AppError> =
    let predicate = "e.unique_id = @unique_id"
    let uuid = headerId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchGenericRead context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByStatus (context: Context.Context) (status: StagedEntryStatus) : Result<StageEntryHeader list, AppError> =
    let predicate = "latest_statuses.to_status = @status"
    let statusStr = status |> StagedEntryStatus.toString
    let parameters = [ { name = "@status"; value = CharString statusStr } ]
    fetchGenericRead context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchBySourceFile // todo: determine if we shouldn't just fold all fetch functions into the orchestrator's fetchFiltered 
    (context: Context.Context)
    (statusFilter: StagedEntryStatus list option)
    (sourceFile: SourceFile)
    : Result<StageEntryHeader list, AppError> =
    let statusListClause =
        match statusFilter with
        | None -> ""
        | Some l ->
            let strings =
                l
                |> List.map(fun x -> $"'{x |> StagedEntryStatus.toString}'") // direct interpolation is okay since this is directly pulled from the DU
                |> String.concat ","
            $"and latest_statuses.to_status in ({strings})"
    let predicate = $"""
        e.source_file = @source_file
        {statusListClause}
    """
    let fileStr = sourceFile |> SourceFile.value
    let parameters = [ { name = "@source_file"; value = CharString fileStr } ]
    fetchGenericRead context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchDuplicates (context: Context.Context) : Result<StageEntryHeader list, AppError> =
    let latestStatusCtes = StageEntryStatusTransition.formLatestStatusCte
    let earliestStatusCtes = StageEntryStatusTransition.formEarliestStatusCte
    let dedupCtes =
        [
            """all_in_ledger as (
            select
                jex.journal_entry_id,
                jex.financial_institution,
                jex.reference
            from ledger.journal_entry_ext_reference jex
            left join ledger.journal_entry je on jex.journal_entry_id = je.unique_id
            where je.voided_at is null
            )"""
            """all_in_stage as (
            select
                se.unique_id as stage_entry_id,
                s.source_name,
                se.fi_reference,
                se.description as stage_entry_description,
                latest_statuses.to_status as current_status,
                earliest_statuses.modified_at as earliest_status_time_stamp,
                row_number() 
                    over (partition by s.unique_id, se.fi_reference 
                    order by earliest_statuses.modified_at nulls first, se.unique_id) as ordinal
            from ingestion.staged_entry se
            join ingestion.source s on se.source_id = s.unique_id
            left join earliest_statuses on se.unique_id = earliest_statuses.entry_id
            left join latest_statuses on se.unique_id = latest_statuses.entry_id
            )"""
            """duplicates as (
            select distinct
                ais.stage_entry_id
            from all_in_stage ais
            left join all_in_ledger ail -- note this join creates duplicates because 2 JEs can share the same FI and reference
                on ais.source_name = ail.financial_institution
                and ais.fi_reference = ail.reference
            where ais.current_status not in ('Duplicate', 'Posted', 'Ignored', 'Reviewed')
            and (ais.ordinal > 1 or ail.journal_entry_id is not null)
            )"""
        ]
    let cteList = latestStatusCtes@earliestStatusCtes@dedupCtes
    let select = """
            e.unique_id, e.entry_date, e.description, e.source_id, e.fi_reference, e.source_file, latest_statuses.to_status as current_status,
            s.source_name, s.created_at as source_created, s.modified_at as source_modified
            """
    let joinList =
        [
            "join duplicates d on e.unique_id = d.stage_entry_id"
            "join ingestion.source s on e.source_id = s.unique_id"
            "left join latest_statuses on e.unique_id = latest_statuses.entry_id"
        ]
    readRowsFromDb context (Some cteList) select (Some joinList) None None None None [] AnyQuantityIsAcceptable

/// updateDb is incredibly powerful and should only be used very deliberately. It will let you update your database in a
/// type-unsafe manner. Only use it with controlled database transactions and with certainty that you are validating
/// your resultant data state appropriately.
let updateDb
    (context: Context.Context)
    (fieldUpdates: StageEntryHeaderFieldUpdates)
    : Result<StageEntryHeader, AppError> =
    let headerId = fieldUpdates.headerIdToUpdate
    let sourceFileUpdate = fieldUpdates.sourceFileUpdate
    let entryDateUpdate = fieldUpdates.entryDateUpdate
    let descriptionUpdate = fieldUpdates.descriptionUpdate
    let ingestionSourceUpdate = fieldUpdates.ingestionSourceUpdate
    let fiReferenceUpdate = fieldUpdates.fiReferenceUpdate
    let statusUpdate = fieldUpdates.statusUpdate
    let uuid = headerId |> StageEntryHeaderId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              sourceFileUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  ("source_file = @source_file",
                   { name = "@source_file"; value = CharString(SourceFile.value n) }))
              
              entryDateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  ("entry_date = @entry_date",
                   { name = "@entry_date"; value = DbLocalDate(n) }))
              
              descriptionUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  ("description = @description",
                   { name = "@description"; value = CharString(JournalEntryDescription.value n) }))
              
              ingestionSourceUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let uuid = n |> ingestionSourceId |> IngestionSourceId.value
                  ("source_id = @source_id",
                   { name = "@source_id"; value = UniqueId(uuid) }))
              
              fiReferenceUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  ("fi_reference = @fi_reference",
                   { name = "@fi_reference"; value = CharString(JournalExternalReferenceText.value n) }))
        ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let query =
        $"""
        UPDATE ingestion.staged_entry
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty && statusUpdate = NoChange then Error(IngestionStageEntryHeaderNoOp) else Ok()
        do! match statusUpdate with
            | NoChange -> Ok ()
            | SetTo (newStatus, mechanism) ->
                headerId |> updateHeaderStatus context newStatus mechanism
        let! _ =
            if updates |> List.isEmpty = false
            then executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
            else Ok()
        return! headerId |> fetchById context
    }
    
