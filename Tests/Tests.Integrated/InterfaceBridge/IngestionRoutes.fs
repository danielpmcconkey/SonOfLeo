module Tests.Integrated.InterfaceBridge.IngestionRoutes

open System.IO
open InterfaceBridge.InterfaceContracts.IngestionContracts
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

    (* An InlineData attribute cannot hold a 1001-character literal, so over-length rows
       carry the sentinel "tooLong" and it is expanded here to one character past the
       field's documented maximum. The expansion is derived from the maximum rather than
       hard-coded so the row proves where the boundary actually sits. *)
    static let maxLengthOf =
        function
        | "description"
        | "memo" -> 1000
        | "fiSource"
        | "fiReference" -> 100
        | other -> failwith $"No maximum length is defined for field {other}."

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
    static let assertRouteRejects fileName rows expectedError =
        try
            writeImportFile fileName rows
            result {
                do!
                    isCorrectErrorString
                        (routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName))
                        expectedError
                        (Some "The file may have been moved to the processed directory.")
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName

    [<Fact>]
    member _.``REQ-STG-3.1 IngestRawFileToStage route ingests valid file and returns result`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("entryDate", "not-a-date", "InterfaceBridgeFailedJsonDeserialization")>]
    [<InlineData("amount", "32.475", "MoneyFailedToConvertImproperPrecision")>]
    [<InlineData("amount", "19999999999.99", "MoneyFailedToConvertExceededMax")>]
    [<InlineData("entryType", "Sideways", "JournalEntryLineTypeInvalid")>]
    [<InlineData("accountCode", "", "AccountCodeIsEmpty")>]
    [<InlineData("description", "", "JournalEntryDescriptionIsEmpty")>]
    [<InlineData("description", "tooLong", "JournalEntryDescriptionTooLong")>]
    [<InlineData("fiSource", "", "JournalRefFinancialInstitutionIsEmpty")>]
    [<InlineData("fiSource", "tooLong", "JournalRefFinancialInstitutionTooLong")>]
    [<InlineData("fiReference", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData("fiReference", "tooLong", "JournalEntryReferenceTextTooLong")>]
    [<InlineData("memo", "", "JournalEntryLineMemoIsEmpty")>]
    [<InlineData("memo", "tooLong", "JournalEntryLineMemoTooLong")>]
    member _.``REQ-STG-1.5 REQ-STG-1.6 REQ-STG-1.7 REQ-STG-1.8 REQ-STG-1.9 REQ-STG-1.10 REQ-STG-1.11 REQ-STG-1.12 IngestRawFileToStage validates input as valid types``
        (field: string, value: string, expectedError: string)
        =
        let valueToUse =
            if value = "tooLong" then String.replicate (maxLengthOf field + 1) "x" else value
        let entryDateToUse = if field = "entryDate" then valueToUse else today
        let descriptionToUse = if field = "description" then valueToUse else "Route validation test entry"
        let fiSourceToUse = if field = "fiSource" then valueToUse else "TestBank"
        let fiReferenceToUse = if field = "fiReference" then valueToUse else "REF-ROUTE-VALIDATION"
        let amountToUse = if field = "amount" then valueToUse else "32.47"
        let entryTypeToUse = if field = "entryType" then valueToUse else "Debit"
        let accountCodeToUse = if field = "accountCode" then Some valueToUse else None
        let memoToUse = if field = "memo" then Some valueToUse else None
        (* Header fields and the amount are repeated on both rows so the group stays
           internally consistent and balanced; only the defect under test is wrong. Line
           fields are fixed on the second row, so a line defect lands on the first alone. *)
        let rows =
            [ rawRow
                  "grp-route-validation"
                  entryDateToUse
                  descriptionToUse
                  fiSourceToUse
                  fiReferenceToUse
                  amountToUse
                  entryTypeToUse
                  accountCodeToUse
                  memoToUse
              rawRow
                  "grp-route-validation"
                  entryDateToUse
                  descriptionToUse
                  fiSourceToUse
                  fiReferenceToUse
                  amountToUse
                  "Credit"
                  (Some "F-1270")
                  None ]
        assertRouteRejects $"ingestion-route-{expectedError}.jsonl" rows expectedError

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
