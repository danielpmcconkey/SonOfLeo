namespace Tests.Integrated.InterfaceBridge

open InterfaceBridge.InterfaceContracts.ReportsContracts
open Utilities.Json.Json
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries.JournalEntry
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type ReportRoutesTests(fixture: TestDataFixture) =

    let nextMonth = Calendar.today().PlusMonths(1)
    (* A container-local scratch directory. /tmp does not survive a container restart, so
       the directory is created here rather than assumed; each test deletes the file it
       wrote. *)
    let testOutputDir =
        let dir = "/tmp/son-of-leo-test-output"
        System.IO.Directory.CreateDirectory dir |> ignore
        dir

    [<Fact>]
    member _.``REQ-RPT-2.2 data-only mode returns boundary-type rows with expected field types``() =
        let input: TrialBalanceInput = { asOf = { asOf = nextMonth }; reportOutput = OutputSpecifier.DataOnly }
        let expectedCount = fixture.Data.accounts |> List.length
        let leafId = fixture.Data.food5350Id
        let leafCode =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = leafId)
            |> Account.code
            |> AccountCode.value
        let unvoidedLines =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                je |> header |> JournalEntryHeader.voidedAt |> Option.isNone)
            |> List.collect lines
        let expectedDebits =
            unvoidedLines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = leafId && l |> JournalEntryLine.lineType = Debit)
            |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
        let expectedCredits =
            unvoidedLines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = leafId && l |> JournalEntryLine.lineType = Credit)
            |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
        let expectedNet = expectedDebits - expectedCredits
        result {
            let! payload = input |> toJson<TrialBalanceInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReturn>
            return!
                match returned with
                | TrialBalanceReturn.DataOnly rows ->
                    Assert.Equal(expectedCount, rows |> List.length)
                    let leafRow = rows |> List.find(fun r -> r.accountCode = leafCode)
                    Assert.Equal(expectedDebits, leafRow.totalDebits)
                    Assert.Equal(expectedCredits, leafRow.totalCredits)
                    Assert.Equal(expectedNet, leafRow.netBalance)
                    Ok ()
                | TrialBalanceReturn.Report _ ->
                    Error (TestingError "Expected DataOnly but got Report")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.3 report mode writes an HTML file and returns the file path``() =
        let input: TrialBalanceInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "rpt-2-3-test" } }
        result {
            let! payload = input |> toJson<TrialBalanceInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReturn>
            return!
                match returned with
                | TrialBalanceReturn.Report pathReturn ->
                    Assert.True(System.IO.File.Exists pathReturn.fullyQualifiedPath)
                    Assert.Contains(".html", pathReturn.fullyQualifiedPath)
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    Ok ()
                | TrialBalanceReturn.DataOnly _ ->
                    Error (TestingError "Expected Report but got DataOnly")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.4 date interpolation appends yyyy-MM-dd to filename before extension``() =
        let input: TrialBalanceInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = true; fileName = "rpt-2-4-test" } }
        let expectedDateStr = nextMonth |> Calendar.localDateToString "yyyy-MM-dd"
        let expectedPath =
            System.IO.Path.Combine(testOutputDir, $"rpt-2-4-test-{expectedDateStr}.html")
        result {
            let! payload = input |> toJson<TrialBalanceInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReturn>
            return!
                match returned with
                | TrialBalanceReturn.Report pathReturn ->
                    (* Containment proves the date is somewhere in the path. The requirement
                       is about where: base dir, then the file name, then a hyphen and the
                       date, then the extension. Only the whole path asserts that. *)
                    Assert.Equal(expectedPath, pathReturn.fullyQualifiedPath)
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    Ok ()
                | TrialBalanceReturn.DataOnly _ ->
                    Error (TestingError "Expected Report but got DataOnly")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-NGUI-4.5 unknown report name fails with typed error``() =
        isCorrectError
            (routeReportingCommandForTesting "BogusReport" [] "{}")
            ReportingUnknownReportName
            None
        |> railroadWrapper
