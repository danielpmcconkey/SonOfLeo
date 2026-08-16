module InterfaceBridge.Routes.IngestionRoutes


open DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.IngestionFieldConverters
open InterfaceBridge.BoundaryConverters.ReportConverters
open InterfaceBridge.InterfaceContracts.IngestionContracts
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.BaseStageRaw
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryHeader
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.ClassificationOrchestration
open ModelOrchestrator.TrialBalanceReport
open Utilities
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.FieldUpdate.FieldUpdate
open Utilities.FileIO
open Utilities.Json
open InterfaceBridge.CommandRoute
open Utilities.ResultHelper

let private ingestRawEntries payload _ =
    runCommandRouteAndAutoCompleteTransaction IngestRawEntries (fun context ->
        result {
            let! input = Json.fromJson<IngestRawFileToStageInput> payload
            let! toBeProcessedPath = createFullPath input.importDir input.fileName
            do! confirmFileExists toBeProcessedPath
            let processedDir = input.processedDir
            do! confirmDirectoryExists processedDir
            let! sourceFile = toBeProcessedPath |> SourceFile.create
            let! linesStr = readTextFileLines toBeProcessedPath
            let! baseStageRawRows =
                linesStr
                |> List.map(fun l -> l |> Json.fromJson<BaseStageRawRow>)
                |> convertListOfResultsToResultsList
            let! fullResult =
                    baseStageRawRows
                    |> StageEntryOrchestration.ingestRawToStageThenDeduplicateAndClassify context sourceFile
            let timeStamp = Clock.now() |> Clock.instantToString "yyyy-MM-dd.HHmmss.fff"
            let! moveToPath = createFullPath processedDir $"{timeStamp}-{input.fileName}"
            do! moveFile toBeProcessedPath moveToPath
            let fullReturn = fullResult |> ``convert [IngestionFullResult] to [IngestionFullResultReturn]``
            return! Json.toJson<IngestionFullResultReturn> fullReturn })

let private newClassificationRule payload _ =
    let context = Context.create NoTransaction IngestNewClassificationRule
    result {
        let! input = Json.fromJson<NewClassificationRuleInput> payload
        let! name = input.classificationRuleName |> ClassificationRuleName.create
        let codeAtMatchStr = input.codeAtMatch
        let! codeAtMatch = codeAtMatchStr |> AccountCode.create
        let! _ = codeAtMatchStr |> LookupCache.accountCodeToId.fetch context // check that it's a real code
        let priority = input.priority
        let! ruleGroups = input.ruleGroups |> ``convert [ClassificationRuleGroupContract list] to [ClassificationRuleGroup list]``
        let isActive = true // no new rules that are already inactive
        let! model =
            createNewClassificationRule
                context
                name
                codeAtMatch
                priority
                ruleGroups
                isActive
        let returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]``
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private fetchClassificationRuleById payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRuleByIdInput> payload
        let classificationRuleId = input.classificationRuleId |> ClassificationRuleId.fromGuid
        let! model = classificationRuleId |> ClassificationRule.fetchById context
        let returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]``
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private fetchClassificationRuleByName payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRuleByNameInput> payload
        let! name = input.classificationRuleName |> ClassificationRuleName.create
        let! model = name |> ClassificationRule.fetchByName context
        let returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]``
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private fetchClassificationRuleFiltered payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRuleFilteredInput> payload
        let! model = fetchRulesFiltered context input.filter input.sort
        let returnVal = model |> ``convert [ClassificationRule list] to [ClassificationRuleReturn list]``
        return! Json.toJson<ClassificationRuleReturn list> returnVal
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
            let! statusUpdate = input.status |> convertFieldUpdateToNewTypeFallible StagedEntryStatus.fromString
            let (headerUpdates:StageEntryHeaderFieldUpdates) = {
                headerIdToUpdate = input.stageEntryHeaderId |> StageEntryHeaderId.fromGuid
                sourceFileUpdate = sourceFileUpdate
                entryDateUpdate = input.entryDate
                descriptionUpdate = descriptionUpdate
                ingestionSourceUpdate = ingestionSourceUpdate
                fiReferenceUpdate = fiReferenceUpdate
                statusUpdate = statusUpdate }
            let! lineUpdates = input.lines |> ``convert [UpdateStageEntryLineInput list] to [StageEntryLineFieldUpdates list]``
            let! model = StageEntryOrchestration.updateStageEntry context headerUpdates lineUpdates
            let returnVal = model |> ``convert [StageEntry] to [StageEntryReturn]``
            return! Json.toJson<StageEntryReturn> returnVal })
    
let private postWithExternallyManagedTransaction
    (context: Context.Context)
    : Result<PostStageEntriesTrialBalancesResult, AppError> =
    result {
        let asOf = Calendar.today()
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

let ingestionDomainCommandRoutes: CommandRoute list =
    [
      { domain = "Ingestion"
        verb = "IngestRawFileToStage"
        description = "Read a raw jsonl file and write to the stage database. Automatically runs deduplication and classification. It also moves the file from its current directory to the processed directory."
        inputContract = typeof<IngestRawFileToStageInput>.Name
        outputContract = typeof<IngestionFullResultReturn>.Name
        handler = ingestRawEntries }
      
      { domain = "Ingestion"
        verb = "NewClassificationRule"
        description = "Create a new ClassificationRule."
        inputContract = typeof<NewClassificationRuleInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = newClassificationRule }
      
      { domain = "Ingestion"
        verb = "FetchClassificationRuleById"
        description = "Fetch a specific ClassificationRule by providing its Id."
        inputContract = typeof<FetchClassificationRuleByIdInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = fetchClassificationRuleById }
      
      { domain = "Ingestion"
        verb = "FetchClassificationRuleByName"
        description = "Fetch a specific ClassificationRule by providing its name."
        inputContract = typeof<FetchClassificationRuleByNameInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = fetchClassificationRuleByName }
      
      { domain = "Ingestion"
        verb = "FetchClassificationRuleFiltered"
        description = "Fetch a whichever rules match a specific combination of filter inputs. This is more computationally expensive than the more basic FetchByX."
        inputContract = typeof<FetchClassificationRuleFilteredInput>.Name
        outputContract = typeof<ClassificationRuleReturn list>.Name
        handler = fetchClassificationRuleFiltered }
      
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
      
    ]
