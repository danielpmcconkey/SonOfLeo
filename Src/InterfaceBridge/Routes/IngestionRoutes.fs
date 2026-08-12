module InterfaceBridge.Routes.IngestionRoutes

open InterfaceBridge.BoundaryConverters.IngestionFieldConverters
open InterfaceBridge.InterfaceContracts.IngestionContracts
open Logger.Audit
open Model.DataIngestion
open Model.DataIngestion.BaseStageRaw
open ModelOrchestrator.StageEntryOrchestration
open Utilities
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
            let! stagedEntries = baseStageRawRows |> ingestRawToStage context sourceFile
            let timeStamp = Clock.now() |> Clock.instantToString "yyyy-MM-dd.HHmmss.fff"
            let! moveToPath = createFullPath processedDir $"{timeStamp}-{input.fileName}"
            do! moveFile toBeProcessedPath moveToPath
            let returnEntries = stagedEntries |> ``convert [StageEntry list] to [StageEntryReturn list]``
            return! Json.toJson<StageEntryReturn list> returnEntries })

let accountDomainCommandRoutes: CommandRoute list =
    [
      { domain = "Ingestion"
        verb = "IngestRawFileToStage"
        description = "Read a raw jsonl file and write to the stage database. No classification or deduplication is performed. It also moves the file from its current directory to the processed directory."
        inputContract = typeof<IngestRawFileToStageInput>.Name
        outputContract = typeof<StageEntryReturn list>.Name
        handler = ingestRawEntries }
    ]
