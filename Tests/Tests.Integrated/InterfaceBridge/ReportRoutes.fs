namespace Tests.Integrated.InterfaceBridge

open App.Session
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open App.Utility.Json.Json
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.EntityFunctions
open Business.FinancialServices.Ledger.LedgerAuditableAction
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

    (* Form 4 for several staged entries at once: each is committed before the test runs and deleted in finally. *)
    let withCommittedStagedEntries
        (stage: PrePostingScenario -> Result<StageEntryOrchestration.StageEntry list, IAppError>)
        (test: StageEntryOrchestration.StageEntry list -> Result<unit, IAppError>) =
        let headersToCleanUp = ref []
        try
            result {
                let! entries =
                    runCommandRouteAndAutoCompleteTransaction FetchOnly (fun context -> stage (PrePostingScenario(fixture, context)))
                headersToCleanUp.Value <-
                    entries |> List.map (StageEntryOrchestration.stageEntryHeader >> StageEntryHeader.stageEntryHeaderId)
                return! test entries
            }
            |> railroadWrapper
        finally
            headersToCleanUp.Value |> List.iter (fun id -> Cleanup.cleanUpStageEntryHeaderId (Some id) |> railroadWrapper)

    (* The text of a rendered report's <header> element. *)
    let headerOf (html: string) =
        let headerStart = html.IndexOf("<header")
        let headerEnd = html.IndexOf("</header>")
        Assert.True(headerStart >= 0 && headerEnd > headerStart, "the rendered report has no <header> element")
        html.Substring(headerStart, headerEnd - headerStart)

    (* The text of the header's <h1>, the report's title. *)
    let titleIn (header: string) =
        let m = System.Text.RegularExpressions.Regex.Match(header, "<h1[^>]*>(.*?)</h1>", System.Text.RegularExpressions.RegexOptions.Singleline)
        Assert.True(m.Success, "the report header has no <h1> title")
        m.Groups.[1].Value.Trim()

    let runPrePostingReview (reportOutput: OutputSpecifier) =
        result {
            let! payload = { reportOutput = reportOutput } |> toJson<PrePostingReviewInput>
            let! returnPayload = routeReportingCommandForTesting "PrePostingReview" [] payload
            return! returnPayload |> fromJson<PrePostingReviewReturn>
        }

    (* Period activity routes read the committed fixture; a wide range takes in all of its Revenue and Expense activity. *)
    let activityBegin = Calendar.today().PlusDays(-400)
    let activityEnd = Calendar.today().PlusDays(100)

    let runPeriodActivity beginDate endDate (reportOutput: OutputSpecifier) =
        result {
            let input: PeriodActivityInput = { beginDate = beginDate; endDate = endDate; reportOutput = reportOutput }
            let! payload = input |> toJson<PeriodActivityInput>
            let! returnPayload = routeReportingCommandForTesting "PeriodActivity" [] payload
            return! returnPayload |> fromJson<PeriodActivityReturn>
        }

    let periodActivityReportPath interpolate fileName =
        result {
            let! returned =
                runPeriodActivity activityBegin activityEnd
                    (OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = interpolate; fileName = fileName })
            return!
                match returned with
                | PeriodActivityReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                | PeriodActivityReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
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
    member _.``REQ-RPT-2.2 data-only mode returns one boundary-type row per account, with the boundary account's name, generation and totals and every row's generation from the fixture's parent chain``() =
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
        let leafAccount = fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = leafId)
        let expectedGenerations = fixture.Data.accounts |> TrialBalanceFixture.expectedGenerations
        let leafGeneration = expectedGenerations |> List.find (fun (code, _) -> code = leafCode) |> snd
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
                    Assert.Equal(leafAccount |> Account.accountName |> AccountName.value, leafRow.accountName)
                    Assert.True(leafGeneration > 0, "the boundary row's account should have a parent")
                    Assert.Equal(leafGeneration, leafRow.generation)
                    Assert.Equal<(string * int) list>(
                        expectedGenerations,
                        rows |> List.map (fun r -> r.accountCode, r.generation) |> List.sort)
                    Ok ()
                | TrialBalanceReportReturn.Report _ ->
                    Error (TestingError "Expected DataOnly but got Report")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.3 report mode writes a new HTML file showing the fixture's accounts and returns its fully qualified path``() =
        let input: TrialBalanceReportInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "rpt-2-3-test" } }
        let expectedPath = System.IO.Path.Combine(testOutputDir, "rpt-2-3-test.html")
        let fixtureCode =
            fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = fixture.Data.food5350Id)
            |> Account.code |> AccountCode.value
        // a file left behind by an earlier run would satisfy every check below, so it goes first
        System.IO.File.Delete expectedPath
        result {
            Assert.False(System.IO.File.Exists expectedPath)
            let! payload = input |> toJson<TrialBalanceReportInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReportReturn>
            return!
                match returned with
                | TrialBalanceReportReturn.Report pathReturn ->
                    Assert.True(System.IO.Path.IsPathFullyQualified pathReturn.fullyQualifiedPath)
                    Assert.Equal(expectedPath, pathReturn.fullyQualifiedPath)
                    let html = System.IO.File.ReadAllText pathReturn.fullyQualifiedPath
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    Assert.StartsWith("<!DOCTYPE html>", html.TrimStart(), System.StringComparison.OrdinalIgnoreCase)
                    Assert.Contains(fixtureCode, html)
                    Assert.DoesNotContain("tag not implemented", html)
                    Ok ()
                | TrialBalanceReportReturn.DataOnly _ ->
                    Error (TestingError "Expected Report but got DataOnly")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-3.1 the rendered trial balance header shows the report title and the as-of date``() =
        let asOf = Calendar.today().PlusDays(-9)
        let input: TrialBalanceReportInput =
            { asOf = { asOf = asOf }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "rpt-3-1-trial-balance-header" } }
        result {
            let! payload = input |> toJson<TrialBalanceReportInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReportReturn>
            return!
                match returned with
                | TrialBalanceReportReturn.Report pathReturn ->
                    let html = System.IO.File.ReadAllText pathReturn.fullyQualifiedPath
                    System.IO.File.Delete pathReturn.fullyQualifiedPath
                    let header = headerOf html
                    Assert.Equal("Trial Balance Report", titleIn header)
                    Assert.Contains(asOf |> Calendar.localDateToString "yyyy-MM-dd", header)
                    Ok ()
                | TrialBalanceReportReturn.DataOnly _ ->
                    Error (TestingError "Expected Report but got DataOnly")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-3.1 REQ-RPT-6.4 the rendered balance-sheet integrity header shows the report title and the as-of date``() =
        let asOf = Calendar.today().PlusDays(-11)
        result {
            let! path = integrityReportPath asOf false "rpt-3-1-integrity-header"
            let html = System.IO.File.ReadAllText path
            System.IO.File.Delete path
            let header = headerOf html
            Assert.Equal("Balance-Sheet Integrity", titleIn header)
            Assert.Contains(asOf |> Calendar.localDateToString "yyyy-MM-dd", header)
            return ()
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
    member _.``REQ-RPT-7.7 REQ-RPT-3.1 pre-posting review report mode writes an HTML file whose header shows the report title, the run date and the staged entries' entry and line counts, and returns its path``() =
        (* The fixture stages nothing Classified or Reviewed (its staged entries are Posted, Duplicate or Ignored), so
           the review holds exactly the entries staged here: a two-line Classified one and a three-line Reviewed one. *)
        let stage (s: PrePostingScenario) =
            result {
                let! classified = s.classifiedEntry "Pre-posting review route 7.7 report"
                let! reviewed =
                    s.stagedEntryWith s.testBank "Pre-posting review route 7.7 report, three lines" (System.Guid.NewGuid().ToString()) s.Today
                        [ (100.00M, "Credit", Some s.cashCode, None)
                          (60.00M, "Debit", Some s.loanCode, None)
                          (40.00M, "Debit", Some s.loanCode, None) ]
                        [ ("Classified", "Classifier"); ("Reviewed", "Operator") ]
                return [ classified; reviewed ]
            }
        withCommittedStagedEntries stage (fun staged ->
            let expectedEntries = staged |> List.length
            let expectedLines = staged |> List.sumBy (StageEntryOrchestration.seLines >> List.length)
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
                        let header = headerOf html
                        Assert.Equal("Pre-Posting Review", titleIn header)
                        Assert.Contains(Calendar.today() |> Calendar.localDateToString "yyyy-MM-dd", header)
                        Assert.Equal((2, 5), (expectedEntries, expectedLines))
                        Assert.Matches($@"(?<!\d){expectedEntries} entries, {expectedLines} lines(?!\d)", header)
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

    [<Fact>]
    member _.``REQ-RPT-4.3 an account code that resolves to no account fails with a typed error naming the code`` () =
        let today = Calendar.today()
        let input: ReconciliationInput =
            { rows =
                [ { accountCode = "F-5650"; externalBalance = 0.00M; asOf = today }
                  { accountCode = "RC-9999"; externalBalance = 0.00M; asOf = today } ] }
        result {
            let! payload = input |> toJson<ReconciliationInput>
            return!
                match routeReportingCommandForTesting "Reconciliation" [] payload with
                | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId code)) -> Assert.Equal("RC-9999", code); Ok ()
                | Error e -> Error (TestingError $"Wrong error. {e.ToMessage()}")
                | Ok _ -> Error (TestingError "Expected failure on an unknown account code; got success")
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

    (* Form 4: the route reads through its own connection, so every write is committed and deleted in finally. RPT-6.4
       is zeroed by a 40.00 debit and credit dated 20 and 15 days ago, deactivated with an active end 10 days ago, then
       debited 30.00 dated 12 days ago. All three entries are posted today, after its active end. *)
    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-5.4 the integrity report route in data-only mode returns a deactivated account holding a balance with its code, name, active end, balance and the entries posted after its active end``() =
        let today = Calendar.today()
        let activeEnd = today.PlusDays(-10)
        let mutable accountsToCleanUp = []
        let mutable entriesToCleanUp = []
        try
            result {
                let! retiredId, liabilityId =
                    runCommandRouteAndAutoCompleteTransaction AccountCreate (fun context ->
                        result {
                            let! _, retiredId =
                                createTestAccountFromPrimitives
                                    context "RPT-6.4" "Report route retired asset" "Asset" (today.PlusYears(-1)) None (Some "Cash") None None
                            let! _, liabilityId =
                                createTestAccountFromPrimitives
                                    context "RPT-6.4L" "Report route liability" "Liability" (today.PlusYears(-1)) None
                                    (Some "CurrentLiability") None None
                            return retiredId, liabilityId
                        })
                accountsToCleanUp <- [ Some retiredId; Some liabilityId ]
                let post description (date: NodaTime.LocalDate) retiredSide liabilitySide amount =
                    runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew (fun context ->
                        createTestJournalEntryFromPrimitives
                            context description None date
                            [ (retiredId, amount, retiredSide, None); (liabilityId, amount, liabilitySide, None) ] [] []
                        |> Result.map snd)
                    |> Result.map (fun id ->
                        entriesToCleanUp <- Some id :: entriesToCleanUp
                        id)
                let! debit = post "Report route retired debit" (today.PlusDays(-20)) "Debit" "Credit" 40.00M
                let! credit = post "Report route retired credit" (today.PlusDays(-15)) "Credit" "Debit" 40.00M
                let! _ =
                    runCommandRouteAndAutoCompleteTransaction AccountDeactivate (fun context ->
                        retiredId |> Account.fetchById context
                        |> Result.bind (AccountDeactivation.deactivateAccount context (Some activeEnd)))
                let! backdated = post "Report route retired backdated" (today.PlusDays(-12)) "Debit" "Credit" 30.00M
                let! returned = runIntegrity today OutputSpecifier.DataOnly
                return!
                    match returned with
                    | BalanceSheetIntegrityReturn.DataOnly row ->
                        match row.deactivatedAccountsWithBalance |> List.filter (fun a -> a.accountCode = "RPT-6.4") with
                        | [ account ] ->
                            Assert.Equal(("RPT-6.4", "Report route retired asset", activeEnd, 30.00M),
                                         (account.accountCode, account.accountName, account.activeEnd, account.balance))
                            Assert.Equal<Set<System.Guid>>(
                                [ debit; credit; backdated ] |> List.map JournalEntryHeaderId.value |> Set.ofList,
                                account.entriesAfterActiveEnd |> List.map _.journalEntryId |> Set.ofList)
                            Ok ()
                        | other -> TestError.error (TestingError $"Expected RPT-6.4 listed once; listed {other |> List.length} times")
                    | BalanceSheetIntegrityReturn.Report _ -> TestError.error (TestingError "Expected DataOnly but got Report")
            }
            |> railroadWrapper
        finally
            Cleanup.cleanUpJournalEntryList entriesToCleanUp |> railroadWrapper
            Cleanup.cleanUpAccountList accountsToCleanUp |> railroadWrapper

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

    [<Fact>]
    member _.``REQ-RPT-6.4 the period activity report route in data-only mode returns, for a range with activity on several Revenue and Expense accounts, the same non-empty accounts, net totals and lines, in the same order, as the period activity computation for the same begin and end dates``() =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! expected = PeriodActivity.fetchPeriodActivity context activityBegin activityEnd
            Assert.True(expected |> List.length >= 2)
            Assert.All(expected, fun a -> Assert.NotEmpty a.lines)
            let! returned = runPeriodActivity activityBegin activityEnd OutputSpecifier.DataOnly
            return!
                match returned with
                | PeriodActivityReturn.DataOnly rows ->
                    let expectedRows =
                        expected
                        |> List.map (fun a ->
                            a.accountCode |> AccountCode.value,
                            a.accountName |> AccountName.value,
                            a.netTotal |> Money.amount,
                            a.lines
                            |> List.map (fun l ->
                                l.entryDate,
                                l.journalEntryId |> JournalEntryHeaderId.value,
                                l.description |> JournalEntryDescription.value,
                                l.lineType |> JournalEntryLineType.toString,
                                l.amount |> Money.amount,
                                l.memo |> Option.map JournalEntryLineMemo.value))
                    let actualRows =
                        rows
                        |> List.map (fun r ->
                            r.accountCode,
                            r.accountName,
                            r.netTotal,
                            r.lines
                            |> List.map (fun l -> l.entryDate, l.journalEntryId, l.description, l.lineType, l.amount, l.memo))
                    Assert.Equal<_ list>(expectedRows, actualRows)
                    Ok ()
                | PeriodActivityReturn.Report _ -> TestError.error (TestingError "Expected DataOnly but got Report")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-2.3 the period activity report route in report mode writes a new HTML file that did not exist before the call and returns its fully qualified path``() =
        let expectedPath = System.IO.Path.Combine(testOutputDir, "rpt-6-4-activity-new.html")
        System.IO.File.Delete expectedPath
        result {
            Assert.False(System.IO.File.Exists expectedPath)
            let! path = periodActivityReportPath false "rpt-6-4-activity-new"
            Assert.True(System.IO.Path.IsPathFullyQualified path)
            Assert.Equal(expectedPath, path)
            Assert.True(System.IO.File.Exists path)
            let html = System.IO.File.ReadAllText path
            System.IO.File.Delete path
            Assert.StartsWith("<!doctype html>", html.TrimStart(), System.StringComparison.OrdinalIgnoreCase)
            Assert.DoesNotContain("tag not implemented", html)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-2.4 the period activity report file without date interpolation is the caller's base directory and file name with .html appended``() =
        result {
            let! path = periodActivityReportPath false "rpt-6-4-activity"
            System.IO.File.Delete path
            Assert.Equal(System.IO.Path.Combine(testOutputDir, "rpt-6-4-activity.html"), path)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-2.4 the period activity report file with date interpolation is the caller's base directory and file name followed by a hyphen, the begin date and end date as yyyy-MM-dd_yyyy-MM-dd, and .html``() =
        let b = activityBegin |> Calendar.localDateToString "yyyy-MM-dd"
        let e = activityEnd |> Calendar.localDateToString "yyyy-MM-dd"
        result {
            let! path = periodActivityReportPath true "rpt-6-4-activity-interpolated"
            System.IO.File.Delete path
            Assert.Equal(System.IO.Path.Combine(testOutputDir, $"rpt-6-4-activity-interpolated-{b}_{e}.html"), path)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-6.4 REQ-RPT-3.1 the period activity rendered report header shows the report title and the begin and end dates of the range``() =
        let b = activityBegin |> Calendar.localDateToString "yyyy-MM-dd"
        let e = activityEnd |> Calendar.localDateToString "yyyy-MM-dd"
        result {
            let! path = periodActivityReportPath false "rpt-6-4-activity-header"
            let html = System.IO.File.ReadAllText path
            System.IO.File.Delete path
            let header = headerOf html
            Assert.Equal("Period Activity", titleIn header)
            Assert.Contains(b, header)
            Assert.Contains(e, header)
            return ()
        }
        |> railroadWrapper

    (* The routes build their own context, so a route test cannot see the initiation instant itself, only bracket it
       between two clock reads. Each test below therefore makes two observations: the writer, handed an instant far
       from now, renders that instant and no clock read of its own; and the route, run between two clock reads,
       renders a moment inside that window. *)

    [<Fact>]
    member _.``REQ-SYS-3.4 a rendered report's footer instant is the operation's initiation instant, not a later clock read`` () =
        let footerText (html: string) =
            let start = html.IndexOf("<footer")
            let contentStart = html.IndexOf(">", start) + 1
            html.Substring(contentStart, html.IndexOf("</footer>") - contentStart).Trim()
        let localSeconds (instant: NodaTime.Instant) =
            instant.InZone(Clock.timeZoneLocal).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
        result {
            // the writer renders the instant it is given
            let handedIn = Clock.now().Minus(NodaTime.Duration.FromDays(400)).Minus(NodaTime.Duration.FromSeconds(3917L))
            let! written =
                Ui.InterfaceBridge.ReportWriters.TrialBalanceWriter.write
                    { baseDir = testOutputDir; interpolateAsOf = false; fileName = "sys-3-4-footer-writer" } handedIn nextMonth []
            let! writerPath =
                match written with
                | TrialBalanceReportReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                | TrialBalanceReportReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
            let writerHtml = System.IO.File.ReadAllText writerPath
            System.IO.File.Delete writerPath
            Assert.Equal($"Generated: {handedIn |> localSeconds}", writerHtml |> footerText)
            // the route renders a moment inside the operation
            let before = Clock.now()
            let input: TrialBalanceReportInput =
                { asOf = { asOf = nextMonth }
                  reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "sys-3-4-footer-route" } }
            let! payload = input |> toJson<TrialBalanceReportInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let after = Clock.now()
            let! returned = returnPayload |> fromJson<TrialBalanceReportReturn>
            let! routePath =
                match returned with
                | TrialBalanceReportReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                | TrialBalanceReportReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
            let routeHtml = System.IO.File.ReadAllText routePath
            System.IO.File.Delete routePath
            let secondsInWindow =
                let first = NodaTime.Instant.FromUnixTimeSeconds(before.ToUnixTimeSeconds())
                Seq.initInfinite (fun i -> first.Plus(NodaTime.Duration.FromSeconds(int64 i)))
                |> Seq.takeWhile (fun i -> i <= after)
                |> Seq.map (fun i -> $"Generated: {i |> localSeconds}")
                |> List.ofSeq
            Assert.Contains(routeHtml |> footerText, secondsInWindow)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.4 REQ-RPT-7.7 the pre-posting review's run date, in its file name and its header, is the calendar date of the operation's initiation instant`` () =
        let headerText (html: string) =
            let start = html.IndexOf("<header")
            html.Substring(start, html.IndexOf("</header>") - start)
        let dateStr (d: NodaTime.LocalDate) = d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        result {
            // the writer names the file and heads the page with the run date it is given, not today's
            let handedInDate = Calendar.today().PlusDays(-400)
            let! written =
                Ui.InterfaceBridge.ReportWriters.PrePostingReviewWriter.write
                    { baseDir = testOutputDir; interpolateAsOf = true; fileName = "sys-3-4-run-date-writer" } (Clock.now()) handedInDate []
            let! writerPath =
                match written with
                | PrePostingReviewReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                | PrePostingReviewReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
            let writerHtml = System.IO.File.ReadAllText writerPath
            System.IO.File.Delete writerPath
            Assert.Equal(System.IO.Path.Combine(testOutputDir, $"sys-3-4-run-date-writer-{handedInDate |> dateStr}.html"), writerPath)
            Assert.Contains($">{handedInDate |> dateStr}<", writerHtml |> headerText)
            // the route's run date is the date of a moment inside the operation
            let before = Clock.now() |> Calendar.dateFromInstant
            let! returned =
                runPrePostingReview
                    (OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = true; fileName = "sys-3-4-run-date-route" })
            let after = Clock.now() |> Calendar.dateFromInstant
            let! routePath =
                match returned with
                | PrePostingReviewReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                | PrePostingReviewReturn.DataOnly _ -> TestError.error (TestingError "Expected Report but got DataOnly")
            let routeHtml = System.IO.File.ReadAllText routePath
            System.IO.File.Delete routePath
            // a run straddling midnight may carry either date, but its file name and header must agree
            let runDate = if routePath.EndsWith($"-{after |> dateStr}.html") then after else before
            Assert.Equal(System.IO.Path.Combine(testOutputDir, $"sys-3-4-run-date-route-{runDate |> dateStr}.html"), routePath)
            Assert.Contains($">{runDate |> dateStr}<", routeHtml |> headerText)
            return ()
        }
        |> railroadWrapper
