module Tests.Integrated.InterfaceBridge.IngestionRoutes

open System.IO
open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open Utilities
open Utilities.AppError
open Utilities.Json.Json
open Utilities.ResultHelper
open Xunit


[<Collection("SharedTestData")>]
type IngestionRouteTests(fixture: TestDataFixture) =

    (* The route reads a real file off disk and, on success, moves it into the processed
       directory under a timestamped name. These are container-local scratch directories;
       each test deletes the file it wrote, from both, in its finally. *)
    static let testRoot = Path.Combine(Path.GetTempPath(), "sonofleo-route-tests")
    static let importDir = Path.Combine(testRoot, "import")
    static let processedDir = Path.Combine(testRoot, "processed")

    static let today = Calendar.today().ToString("yyyy-MM-dd", null)

    static let quotedOrNull =
        function
        | Some(s: string) -> $"\"{s}\""
        | None -> "null"

    /// One line of the base staging format, as a parser would emit it.
    static let rawRow groupId entryDate description fiSource fiReference amount lineType accountCode memo =
        $"""{{"baseStageEntryGroupId":"%s{groupId}","entryDate":"%s{entryDate}","description":"%s{description}","fiSource":"%s{fiSource}","fiReference":"%s{fiReference}","amount":%s{amount},"entryType":"%s{lineType}","accountCode":%s{quotedOrNull accountCode},"memo":%s{quotedOrNull memo}}}"""

    static let writeImportFile fileName (rows: string list) =
        Directory.CreateDirectory importDir |> ignore
        Directory.CreateDirectory processedDir |> ignore
        File.WriteAllLines(Path.Combine(importDir, fileName), rows)

    static let deleteImportFile fileName =
        File.Delete(Path.Combine(importDir, fileName))
        if Directory.Exists processedDir then
            Directory.GetFiles(processedDir, $"*-{fileName}") |> Array.iter File.Delete

    static let ingestPayload fileName =
        { IngestRawFileToStageInput.fileName = fileName
          importDir = importDir
          processedDir = processedDir }
        |> toJson<IngestRawFileToStageInput>
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

    /// Writes a one-defect file, asserts the route rejects it with the exact error, cleans up.
    static let assertRouteRejects fileName rows expectedCaseConstructor =
        try
            writeImportFile fileName rows
            result {
                do!
                    isCorrectError
                        (routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName))
                        expectedCaseConstructor
                        None
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName

    [<Fact>]
    member _.``REQ-STG-3.1 IngestRawFileToStage route ingests valid file and returns result`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.5 IngestRawFileToStage rejects record with invalid entry_date`` () =
        let fileName = "req-stg-1-5.jsonl"
        let rows =
            [ rawRow "grp-1-5" "not-a-date" "Route test entry" "TestBank" "REF-1-5" "32.47" "Debit" None None
              rawRow "grp-1-5" today "Route test entry" "TestBank" "REF-1-5" "32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows InterfaceBridgeFailedJsonDeserialization

    [<Fact>]
    member _.``REQ-STG-1.6 IngestRawFileToStage rejects record with negative amount`` () =
        let fileName = "req-stg-1-6.jsonl"
        let rows =
            [ rawRow "grp-1-6" today "Route test entry" "TestBank" "REF-1-6" "-32.47" "Debit" None None
              rawRow "grp-1-6" today "Route test entry" "TestBank" "REF-1-6" "-32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows IngestionStageLineNonPositiveAmount

    [<Fact>]
    member _.``REQ-STG-1.7 IngestRawFileToStage rejects record with invalid line_type`` () =
        let fileName = "req-stg-1-7.jsonl"
        let rows =
            [ rawRow "grp-1-7" today "Route test entry" "TestBank" "REF-1-7" "32.47" "Sideways" None None
              rawRow "grp-1-7" today "Route test entry" "TestBank" "REF-1-7" "32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows JournalEntryLineTypeInvalid

    [<Fact>]
    member _.``REQ-STG-1.8 IngestRawFileToStage rejects record with empty account_code`` () =
        let fileName = "req-stg-1-8.jsonl"
        let rows =
            [ rawRow "grp-1-8" today "Route test entry" "TestBank" "REF-1-8" "32.47" "Debit" None None
              rawRow "grp-1-8" today "Route test entry" "TestBank" "REF-1-8" "32.47" "Credit" (Some "") None ]
        assertRouteRejects fileName rows AccountCodeIsEmpty

    [<Fact>]
    member _.``REQ-STG-1.9 IngestRawFileToStage rejects record with description over 1000 chars`` () =
        let fileName = "req-stg-1-9.jsonl"
        let tooLong = String.replicate 1001 "x"
        let rows =
            [ rawRow "grp-1-9" today tooLong "TestBank" "REF-1-9" "32.47" "Debit" None None
              rawRow "grp-1-9" today tooLong "TestBank" "REF-1-9" "32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows JournalEntryDescriptionTooLong

    [<Fact>]
    member _.``REQ-STG-1.10 IngestRawFileToStage rejects record with fi_source over 100 chars`` () =
        let fileName = "req-stg-1-10.jsonl"
        let tooLong = String.replicate 101 "x"
        let rows =
            [ rawRow "grp-1-10" today "Route test entry" tooLong "REF-1-10" "32.47" "Debit" None None
              rawRow "grp-1-10" today "Route test entry" tooLong "REF-1-10" "32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows JournalRefFinancialInstitutionTooLong

    [<Fact>]
    member _.``REQ-STG-1.11 IngestRawFileToStage rejects record with fi_reference over 100 chars`` () =
        let fileName = "req-stg-1-11.jsonl"
        let tooLong = String.replicate 101 "x"
        let rows =
            [ rawRow "grp-1-11" today "Route test entry" "TestBank" tooLong "32.47" "Debit" None None
              rawRow "grp-1-11" today "Route test entry" "TestBank" tooLong "32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows JournalEntryReferenceTextTooLong

    [<Fact>]
    member _.``REQ-STG-1.12 IngestRawFileToStage rejects record with memo over 1000 chars`` () =
        let fileName = "req-stg-1-12.jsonl"
        let tooLong = String.replicate 1001 "x"
        let rows =
            [ rawRow "grp-1-12" today "Route test entry" "TestBank" "REF-1-12" "32.47" "Debit" None (Some tooLong)
              rawRow "grp-1-12" today "Route test entry" "TestBank" "REF-1-12" "32.47" "Credit" (Some "F-1270") None ]
        assertRouteRejects fileName rows JournalEntryLineMemoTooLong

    [<Fact>]
    member _.``REQ-STG-6.1 REQ-STG-6.2 UpdateStageEntry route happy path`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-8.1 PostStageEntries shadow route returns trial balances and wasRolledBack true`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.1 PostStageEntries real route posts entries and returns wasRolledBack false`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.4 CreateIngestionSource route happy path`` () =
        Assert.Fail "not implemented"
