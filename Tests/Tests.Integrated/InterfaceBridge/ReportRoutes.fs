namespace Tests.Integrated.InterfaceBridge

open App.Session
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.InterfaceContracts.BalanceSheetIntegrityContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.InterfaceContracts.ReconciliationContracts
open App.Utility.Json.Json
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open App.Utility.Result
open Xunit
open Ui.ReportCli.ReportCliError
open App.Operation.CoreAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Ui.InterfaceBridge.CommandRoute
open Tests.Integrated.CrossDomainOrchestration

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

    (* Form 4: the route reads through its own connection, so the staged entry it reports on is committed first and
       deleted in finally. *)
    let withCommittedClassifiedEntry (description: string) (test: StageEntryHeaderId -> Result<unit, IAppError>) =
        let mutable headerToCleanUp = None
        try
            result {
                let! entry =
                    runCommandRouteAndAutoCompleteTransaction FetchOnly (fun context ->
                        PrePostingScenario(fixture, context).classifiedEntry description)
                let headerId = entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                headerToCleanUp <- Some headerId
                return! test headerId
            }
            |> railroadWrapper
        finally
            Cleanup.cleanUpStageEntryHeaderId headerToCleanUp |> railroadWrapper

    let runPrePostingReview (reportOutput: OutputSpecifier) =
        result {
            let! payload = { reportOutput = reportOutput } |> toJson<PrePostingReviewInput>
            let! returnPayload = routeReportingCommandForTesting "PrePostingReview" [] payload
            return! returnPayload |> fromJson<PrePostingReviewReturn>
        }

    let runIntegrity asOf (reportOutput: OutputSpecifier) =
        result {
            let input: BalanceSheetIntegrityInput = { asOf = ({ asOf = asOf }: ReportAsOf); reportOutput = reportOutput }
            let! payload = input |> toJson<BalanceSheetIntegrityInput>
            let! returnPayload = routeReportingCommandForTesting "BalanceSheetIntegrity" [] payload
            return! returnPayload |> fromJson<BalanceSheetIntegrityReturn>
        }

    let integrityReportPath asOf interpolate fileName =
        result {
            let! returned =
                runIntegrity asOf (OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = interpolate; fileName = fileName })
            return!
                match returned with
                | BalanceSheetIntegrityReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                | BalanceSheetIntegrityReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
        }

    [<Fact>]
    member _.``REQ-RPT-2.2 data-only mode returns boundary-type rows with expected field types``() =
        let input: TrialBalanceReportInput = { asOf = { asOf = nextMonth }; reportOutput = OutputSpecifier.DataOnly }
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
            |> List.collect jeLines
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
            let! payload = input |> toJson<TrialBalanceReportInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReportReturn>
            return!
                match returned with
                | TrialBalanceReportReturn.DataOnly rows ->
                    Assert.Equal(expectedCount, rows |> List.length)
                    let leafRow = rows |> List.find(fun r -> r.accountCode = leafCode)
                    Assert.Equal(expectedDebits, leafRow.totalDebits)
                    Assert.Equal(expectedCredits, leafRow.totalCredits)
                    Assert.Equal(expectedNet, leafRow.netBalance)
                    Ok ()
                | TrialBalanceReportReturn.Report _ ->
                    Error (TestingError "Expected DataOnly but got Report")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.3 report mode writes an HTML file and returns the file path``() =
        let input: TrialBalanceReportInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "rpt-2-3-test" } }
        result {
            let! payload = input |> toJson<TrialBalanceReportInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReportReturn>
            return!
                match returned with
                | TrialBalanceReportReturn.Report pathReturn ->
                    Assert.True(System.IO.File.Exists pathReturn.fullyQualifiedPath)
                    Assert.Contains(".html", pathReturn.fullyQualifiedPath)
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    Ok ()
                | TrialBalanceReportReturn.DataOnly _ ->
                    Error (TestingError "Expected Report but got DataOnly")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.4 date interpolation appends yyyy-MM-dd to filename before extension``() =
        let input: TrialBalanceReportInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = true; fileName = "rpt-2-4-test" } }
        let expectedDateStr = nextMonth |> Calendar.localDateToString "yyyy-MM-dd"
        let expectedPath =
            System.IO.Path.Combine(testOutputDir, $"rpt-2-4-test-{expectedDateStr}.html")
        result {
            let! payload = input |> toJson<TrialBalanceReportInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReportReturn>
            return!
                match returned with
                | TrialBalanceReportReturn.Report pathReturn ->
                    (* Containment proves the date is somewhere in the path. The requirement
                       is about where: base dir, then the file name, then a hyphen and the
                       date, then the extension. Only the whole path asserts that. *)
                    Assert.Equal(expectedPath, pathReturn.fullyQualifiedPath)
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    Ok ()
                | TrialBalanceReportReturn.DataOnly _ ->
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

    [<Fact>]
    member _.``REQ-RPT-7.7 pre-posting review data-only mode returns every entry and its lines as boundary types``() =
        withCommittedClassifiedEntry "Pre-posting review route 7.7 data-only" (fun headerId ->
            result {
                let! returned = runPrePostingReview OutputSpecifier.DataOnly
                return!
                    match returned with
                    | PrePostingReviewReturn.DataOnly entries ->
                        let entry = entries |> List.find (fun e -> e.stageEntryHeaderId = (headerId |> StageEntryHeaderId.value))
                        Assert.Equal("Pre-posting review route 7.7 data-only", entry.description)
                        Assert.Equal("TestBank", entry.sourceName)
                        Assert.Equal("Classified", entry.status)
                        Assert.Equal<string list>([ "Debit"; "Credit" ], entry.lines |> List.map _.lineType)
                        Assert.Equal<string list>([ "F-2230"; "F-1280" ], entry.lines |> List.map _.accountCode)
                        Assert.All(entry.lines, fun l -> Assert.Equal(100.00M, l.amount))
                        Ok ()
                    | PrePostingReviewReturn.Report _ -> TestError.error (TestingError "Expected DataOnly but got Report")
            })

    [<Fact>]
    member _.``REQ-RPT-7.7 pre-posting review report mode writes an HTML file and returns its path``() =
        withCommittedClassifiedEntry "Pre-posting review route 7.7 report" (fun _ ->
            result {
                let! returned =
                    runPrePostingReview
                        (OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "rpt-7-7-test" })
                return!
                    match returned with
                    | PrePostingReviewReturn.Report pathReturn ->
                        Assert.Equal(System.IO.Path.Combine(testOutputDir, "rpt-7-7-test.html"), pathReturn.fullyQualifiedPath)
                        let html = System.IO.File.ReadAllText pathReturn.fullyQualifiedPath
                        System.IO.File.Delete pathReturn.fullyQualifiedPath
                        Assert.Contains("Pre-posting review route 7.7 report", html)
                        Assert.DoesNotContain("tag not implemented", html)
                        Ok ()
                    | PrePostingReviewReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
            })

    [<Fact>]
    member _.``REQ-RPT-7.7 pre-posting review date interpolation appends today's date as yyyy-MM-dd to the file name before the extension``() =
        let expectedDateStr = Calendar.today() |> Calendar.localDateToString "yyyy-MM-dd"
        let expectedPath = System.IO.Path.Combine(testOutputDir, $"rpt-7-7-interpolated-{expectedDateStr}.html")
        result {
            let! returned =
                runPrePostingReview
                    (OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = true; fileName = "rpt-7-7-interpolated" })
            return!
                match returned with
                | PrePostingReviewReturn.Report pathReturn ->
                    Assert.Equal(expectedPath, pathReturn.fullyQualifiedPath)
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    Ok ()
                | PrePostingReviewReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-4.1 REQ-RPT-6.4 the Reconciliation report route returns one data-only row per input row``() =
        let today = Calendar.today()
        let yesterday = today.PlusDays(-1)
        let input: ReconciliationInput =
            { rows =
                [ { accountCode = "F-5650"; externalBalance = 12.34M; asOf = today }
                  { accountCode = "F-1270"; externalBalance = 0.00M; asOf = yesterday } ] }
        let context = Context.create NoTransaction FetchOnly
        result {
            let! trialBalanceToday = TrialBalanceReport.fetchTrialBalanceData context today
            let! trialBalanceYesterday = TrialBalanceReport.fetchTrialBalanceData context yesterday
            let expectedRow code (trialBalance: TrialBalanceReport.TrialBalanceRowFlattened list) =
                trialBalance |> List.find (fun row -> row.accountCode |> AccountCode.value = code)
            let! payload = input |> toJson<ReconciliationInput>
            let! returnPayload = routeReportingCommandForTesting "Reconciliation" [] payload
            let! rows = returnPayload |> fromJson<ReconciliationReturnRow list>
            Assert.Equal(2, rows |> List.length)
            [ "F-5650", 12.34M, today, trialBalanceToday
              "F-1270", 0.00M, yesterday, trialBalanceYesterday ]
            |> List.iter (fun (code, external, asOf, trialBalance) ->
                let expected = trialBalance |> expectedRow code
                let row = rows |> List.filter (fun r -> r.accountCode = code) |> List.exactlyOne
                Assert.Equal(expected.accountName |> AccountName.value, row.accountName)
                Assert.Equal(asOf, row.asOf)
                Assert.Equal(external, row.externalBalance)
                Assert.Equal(expected.netBalance |> Money.amount, row.ledgerBalance)
                Assert.Equal(external - (expected.netBalance |> Money.amount), row.delta))
            return ()
        }
        |> railroadWrapper

    (* The fixture has entries dated today, so integrity as of yesterday differs from integrity as of today; the test
       asserts that first, so a route that ignored the as-of date would fail it. *)
    [<Fact>]
    member _.``REQ-RPT-6.4 the integrity report route in data-only mode returns the same totals, equality flag, account-type net balances, net income and residual as the integrity computation for the same as-of date``() =
        let yesterday = Calendar.today().PlusDays(-1)
        let context = Context.create NoTransaction FetchOnly
        result {
            let! expected = BalanceSheetIntegrity.computeBalanceSheetIntegrity context yesterday
            let! asOfToday = BalanceSheetIntegrity.computeBalanceSheetIntegrity context (Calendar.today())
            Assert.NotEqual(asOfToday.totalDebits |> Money.amount, expected.totalDebits |> Money.amount)
            let! returned = runIntegrity yesterday OutputSpecifier.DataOnly
            return!
                match returned with
                | BalanceSheetIntegrityReturn.DataOnly row ->
                    let amount = Money.amount
                    Assert.Equal(yesterday, row.asOf)
                    Assert.Equal(expected.totalDebits |> amount, row.totalDebits)
                    Assert.Equal(expected.totalCredits |> amount, row.totalCredits)
                    Assert.Equal(expected.debitsEqualCredits, row.debitsEqualCredits)
                    Assert.Equal(expected.assets |> amount, row.assets)
                    Assert.Equal(expected.liabilities |> amount, row.liabilities)
                    Assert.Equal(expected.equity |> amount, row.equity)
                    Assert.Equal(expected.revenue |> amount, row.revenue)
                    Assert.Equal(expected.expenses |> amount, row.expenses)
                    Assert.Equal(expected.netIncome |> amount, row.netIncome)
                    Assert.Equal(expected.residual |> amount, row.residual)
                    Ok ()
                | BalanceSheetIntegrityReturn.Report _ -> TestError.error (TestingError "Expected DataOnly but got Report")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-2.3 the integrity report route in report mode returns a fully qualified path at which an HTML file now exists``() =
        // a file left behind by an earlier run would satisfy File.Exists, so it goes first
        System.IO.File.Delete(System.IO.Path.Combine(testOutputDir, "rpt-6-4-integrity-exists.html"))
        result {
            let! path = integrityReportPath (Calendar.today()) false "rpt-6-4-integrity-exists"
            Assert.True(System.IO.Path.IsPathFullyQualified path)
            Assert.True(System.IO.File.Exists path)
            let html = System.IO.File.ReadAllText path
            System.IO.File.Delete path
            Assert.StartsWith("<!DOCTYPE html>", html.TrimStart(), System.StringComparison.OrdinalIgnoreCase)
            Assert.DoesNotContain("tag not implemented", html)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-2.4 the integrity report file without date interpolation is the caller's base directory and file name with .html appended``() =
        result {
            let! path = integrityReportPath (Calendar.today()) false "rpt-6-4-integrity"
            System.IO.File.Delete path
            Assert.Equal(System.IO.Path.Combine(testOutputDir, "rpt-6-4-integrity.html"), path)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-2.4 the integrity report file with date interpolation is the caller's base directory and file name followed by a hyphen, the as-of date as yyyy-MM-dd, and .html``() =
        let asOf = Calendar.today().PlusDays(-3)
        let expectedDateStr = asOf |> Calendar.localDateToString "yyyy-MM-dd"
        result {
            let! path = integrityReportPath asOf true "rpt-6-4-integrity-interpolated"
            System.IO.File.Delete path
            Assert.Equal(System.IO.Path.Combine(testOutputDir, $"rpt-6-4-integrity-interpolated-{expectedDateStr}.html"), path)
            return ()
        }
        |> railroadWrapper
