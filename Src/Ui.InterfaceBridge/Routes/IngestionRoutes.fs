module Ui.InterfaceBridge.Routes.IngestionRoutes

open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.File
open App.Utility.Json
open App.Utility.Result
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryHeader
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.TrialBalanceReport
open Ui.InterfaceBridge.InterfaceContracts.IngestionContracts
open Ui.InterfaceBridge.BoundaryConverters.IngestionFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.ReportConverters
open Ui.InterfaceBridge.CommandRoute

let private ingestRawEntries payload _ =
    result {
        let! input = Json.fromJson<IngestRawFileToStageInput> payload
        let! toBeProcessedPath = createFullPath input.importDir input.fileName
        let! converted =
            runCommandRouteAndAutoCompleteTransaction IngestRawEntries (fun context ->
                result {
                    do! confirmFileExists toBeProcessedPath
                    do! confirmDirectoryExists input.processedDir
                    let! sourceFile = toBeProcessedPath |> SourceFile.create
                    let! linesStr = readTextFileLines toBeProcessedPath
                    let rejected lineNumbers groupId error : DataIngestionError.IngestionRejectedRecord =
                        { lineNumbers = lineNumbers; groupId = groupId; error = error }
                    // every line is checked, so one rejection can name every failing record. Blank lines are not
                    // records, but they are counted, so a line number matches what an editor shows
                    let parsed =
                        linesStr
                        |> List.mapi (fun index line -> index + 1, line)
                        |> List.filter (fun (_, line) -> System.String.IsNullOrWhiteSpace line |> not)
                        |> List.map (fun (lineNumber, line) ->
                            match Json.fromJson<BaseStageRawRowInput> line with
                            | Error e -> Error (rejected [ lineNumber ] None e)
                            | Ok input ->
                                input
                                |> ``convert [BaseStageRawRowInput] to [BaseStageRawRow]`` context
                                |> Result.map (fun row -> lineNumber, row)
                                |> Result.mapError (rejected [ lineNumber ] (Some input.baseStageEntryGroupId)))
                    let recordFailures = parsed |> List.choose (function Error r -> Some r | Ok _ -> None)
                    let goodRows = parsed |> List.choose (function Ok row -> Some row | Error _ -> None)
                    // a group that lost a record would fail its group checks only for that reason, so it is not checked
                    let groupsWithFailedRecords = recordFailures |> List.choose _.groupId |> Set.ofList
                    let checkableRows =
                        goodRows
                        |> List.filter (fun (_, row) ->
                            groupsWithFailedRecords
                            |> Set.contains (row.baseStageEntryGroupId |> BaseStageEntry.BaseStageEntryGroupId.value)
                            |> not)
                    let constructed =
                        checkableRows |> List.map snd |> StageEntryOrchestration.constructFromRaw context sourceFile
                    let groupFailures =
                        match constructed with
                        | Ok _ -> []
                        | Error failures ->
                            failures
                            |> List.map (fun (groupId, e) ->
                                let lineNumbers =
                                    checkableRows
                                    |> List.filter (fun (_, row) -> row.baseStageEntryGroupId = groupId)
                                    |> List.map fst
                                rejected lineNumbers (Some (groupId |> BaseStageEntry.BaseStageEntryGroupId.value)) e)
                    let! entries =
                        match recordFailures @ groupFailures, constructed with
                        | [], Ok entries -> Ok entries
                        | failures, _ ->
                            let inFileOrder = failures |> List.sortBy (fun r -> r.lineNumbers |> List.min)
                            DataIngestionError.error (DataIngestionError.IngestionFileRejected(toBeProcessedPath, inFileOrder))
                    let! staged = entries |> StageEntryOrchestration.persistConstructed context
                    return!
                        staged
                        |> List.map (``convert [StageEntry] to [StageEntryReturn]`` context)
                        |> convertListOfResultsToResultsList })
        // the file moves only once its entries have committed. moved earlier, a failed commit would leave a file that
        // looks processed with nothing staged, and the next run would skip it
        let timeStamp = Clock.now() |> Clock.instantToString "yyyy-MM-dd.HHmmss.fff"
        let! moveToPath = createFullPath input.processedDir $"{timeStamp}-{input.fileName}"
        do!
            moveFile toBeProcessedPath moveToPath
            |> Result.mapError (fun e ->
                DataIngestionError.IngestionStagedButFileNotMoved(
                    toBeProcessedPath, moveToPath, e.ToMessage()) :> IAppError)
        return! Json.toJson<StageEntryReturn list> converted
    }

let private createNewSource payload _ =
    let context = Context.create NoTransaction IngestNewSource
    result {
        let! input = Json.fromJson<CreateNewIngestionSourceInput> payload
        let! name = input.name |> JournalRefFinancialInstitution.create
        let! model = name |> StageEntryOrchestration.createNewSource context
        let returnVal = model |> ``convert [IngestionSource] to [IngestionSourceReturn]``
        return! Json.toJson<IngestionSourceReturn> returnVal
    }

let private updateStageEntry payload _ =
    runCommandRouteAndAutoCompleteTransaction IngestUpdateStageEntry (fun context ->
        result {
            let! input = Json.fromJson<UpdateStageEntryInput> payload
            let! sourceFileUpdate = input.sourceFileUpdate |> convertFieldUpdateToNewTypeFallible SourceFile.create
            let! descriptionUpdate = input.description |> convertFieldUpdateToNewTypeFallible JournalEntryDescription.create
            let! ingestionSourceUpdate =
                match input.ingestionSource with
                | NoChange -> Ok NoChange
                | SetTo nameStr ->
                    result {
                        let! name = nameStr |> JournalRefFinancialInstitution.create
                        let! source = name |> IngestionSource.fetchByName context
                        return SetTo source }
            let! fiReferenceUpdate = input.fiReference |> convertFieldUpdateToNewTypeFallible JournalExternalReferenceText.create
            let! statusUpdate =
                match input.status with
                | NoChange -> Ok NoChange
                | SetTo statusUpdateInput -> result {
                    let! newStatus = statusUpdateInput.newStatus
                                     |> StagedEntryStatus.fromString
                    return SetTo newStatus }
            let (headerUpdates:StageEntryHeaderFieldUpdates) = {
                headerIdToUpdate = input.stageEntryHeaderId |> StageEntryHeaderId.fromGuid
                sourceFileUpdate = sourceFileUpdate
                entryDateUpdate = input.entryDate
                descriptionUpdate = descriptionUpdate
                ingestionSourceUpdate = ingestionSourceUpdate
                fiReferenceUpdate = fiReferenceUpdate
                journalEntryHeaderIdUpdate = NoChange
                statusUpdate = statusUpdate }
            let! lineUpdates =
                input.lines
                |> ``convert [UpdateStageEntryLineInput list] to [StageEntryLineFieldUpdates list]`` context
            let! linesToAdd =
                input.linesToAdd
                |> List.map (``convert [AddStageEntryLineInput] to [StageEntryLineAddition]`` context)
                |> convertListOfResultsToResultsList
            let lineIdsToRemove = input.lineIdsToRemove |> List.map StageEntryLineId.fromGuid
            let! model =
                StageEntryOrchestration.updateStageEntry context headerUpdates lineUpdates linesToAdd lineIdsToRemove
            let! returnVal = model |> ``convert [StageEntry] to [StageEntryReturn]`` context
            return! Json.toJson<StageEntryReturn> returnVal })
    
let private postWithExternallyManagedTransaction
    (context: Context.Context)
    : Result<PostStageEntriesTrialBalancesResult, IAppError> =
    result {
        let asOf = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
        // get the "before" snapshot        
        let! trialBalanceDataBefore = fetchTrialBalanceData context asOf
        let trialBalanceRowsBefore =
            trialBalanceDataBefore
            |> ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
        // post
        do! StageEntryOrchestration.post context
        // get the "after" snapshot        
        let! trialBalanceDataAfter = fetchTrialBalanceData context asOf
        let trialBalanceRowsAfter =
            trialBalanceDataAfter
            |> ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
        return { trialBalanceBefore = trialBalanceRowsBefore
                 trialBalanceAfter = trialBalanceRowsAfter } 
    }
    
let private post payload _ =
    result {
        let! input = Json.fromJson<PostStageEntriesInput> payload
        let runner, auditAction, willBeRolledBack =
            if input.isShadow
            then runCommandRouteAndAutoRollback, IngestShadowPostStageEntries, true
            else runCommandRouteAndAutoCompleteTransaction, IngestPostStageEntries, false
        return!
            runner auditAction (fun context ->
                result {
                    let! trialBalancesResult = postWithExternallyManagedTransaction context
                    let fullResult = {
                          trialBalanceBefore = trialBalancesResult.trialBalanceBefore
                          trialBalanceAfter = trialBalancesResult.trialBalanceAfter
                          wasRolledBack = willBeRolledBack }
                    return! fullResult |> Json.toJson<PostStageEntriesFullResult>
                })
    }
    
let private fetchStageEntryFiltered payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<StageEntryFetchFilteredInput> payload
        let! filter = input.filter |> ``convert [StageEntryFetchFilterInput] to [StageEntryFetchFilter]`` context
        let sort = input.sort
        let! fetched = filter |> StageEntryOrchestration.fetchFiltered context sort
        let! converted =
            fetched
            |> List.map (``convert [StageEntry] to [StageEntryReturn]`` context)
            |> convertListOfResultsToResultsList
        return! converted |> Json.toJson<StageEntryReturn list> }

let private deduplicateStageEntries _ _ =
    runCommandRouteAndAutoCompleteTransaction IngestDeduplicateStageEntries (fun context ->
        result {
            let! remaining = StageEntryOrchestration.deduplicateStagedEntries context
            let! converted =
                remaining
                |> List.map (``convert [StageEntry] to [StageEntryReturn]`` context)
                |> convertListOfResultsToResultsList
            return! Json.toJson<StageEntryReturn list> converted })

let ingestionDomainCommandRoutes: CommandRoute list =
    [
      { domain = "Ingestion"
        verb = "DeduplicateStageEntries"
        description = "Mark every staged entry that duplicates one already in the database, and return everything still Ingested."
        inputContract = typeof<Ui.InterfaceBridge.InterfaceContracts.SharedContracts.NoInput>.Name
        outputContract = typeof<StageEntryReturn list>.Name
        handler = deduplicateStageEntries }

      { domain = "Ingestion"
        verb = "IngestRawFileToStage"
        description = "Read a raw jsonl file and write to the stage database, then move the file from its current directory to the processed directory. Returns the staged entries. Deduplication and classification are separate steps."
        inputContract = typeof<IngestRawFileToStageInput>.Name
        outputContract = typeof<StageEntryReturn list>.Name
        handler = ingestRawEntries }
      
      { domain = "Ingestion"
        verb = "CreateIngestionSource"
        description = "Create a new ingestion source"
        inputContract = typeof<CreateNewIngestionSourceInput>.Name
        outputContract = typeof<IngestionSourceReturn>.Name
        handler = createNewSource }
      
      { domain = "Ingestion"
        verb = "UpdateStageEntry"
        description = "Manually update any aspect of a StageEntry. Warning: this can really screw stuff up. Measure twice, cut once."
        inputContract = typeof<UpdateStageEntryInput>.Name
        outputContract = typeof<StageEntryReturn>.Name
        handler = updateStageEntry }
      
      { domain = "Ingestion"
        verb = "PostStageEntries"
        description = "Writes all Classified and Reviewed stage entry rows to the ledger, updates their status, and returns both before and after trial balance data. If the shadow flag is set, that entire process is rolled back in the database."
        inputContract = typeof<PostStageEntriesInput>.Name
        outputContract = typeof<PostStageEntriesFullResult>.Name
        handler = post }
      
      { domain = "Ingestion"
        verb = "FetchStageEntryFiltered"
        description = "Fetch a list of full stage entries records matching the filter."
        inputContract = typeof<StageEntryFetchFilteredInput>.Name
        outputContract = typeof<StageEntryReturn list>.Name
        handler = fetchStageEntryFiltered }
      
    ]
