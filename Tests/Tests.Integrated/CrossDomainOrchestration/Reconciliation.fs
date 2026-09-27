namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryVoiding
open Business.CrossDomainOrchestration.Reconciliation
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Xunit


(* The ledger side of every read-only test is built inside the test's own rolled-back transaction, on accounts that
   exist only there, so each expected balance is a sum of amounts written in this file rather than a reading of the
   shared fixture.

     RC-1000  Asset, parent of RC-1010          RC-2000  Liability
     RC-1010  Asset

     yesterday  debit RC-1010 100.00, credit RC-2000 100.00
     yesterday  debit RC-1010 500.00, credit RC-2000 500.00   voided
     today      debit RC-1000  30.00, credit RC-2000  30.00

   As of today: RC-1010 100.00, RC-1000 130.00 (its own 30.00 plus its child's 100.00), RC-2000 130.00.
   As of yesterday: RC-1010 100.00, RC-1000 100.00, RC-2000 100.00. *)
type ReconciliationLedger =
    { parentCode: string
      childCode: string
      liabilityCode: string
      childName: string
      liabilityName: string }

[<Collection("SharedTestData")>]
type ReconciliationTests(fixture: TestDataFixture) =

    let today = Calendar.today()
    let yesterday = today.PlusDays(-1)

    let voidReason =
        "Voided so reconciliation can ignore it"
        |> CommentText.create
        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))

    let buildLedger (context: Context.Context) : Result<ReconciliationLedger, IAppError> =
        let ledger =
            { parentCode = "RC-1000"
              childCode = "RC-1010"
              liabilityCode = "RC-2000"
              childName = "Reconciliation child asset"
              liabilityName = "Reconciliation liability" }
        result {
            let! _, parentId =
                createTestAccountFromPrimitives
                    context ledger.parentCode "Reconciliation parent asset" "Asset"
                    (today.PlusYears(-1)) None (Some "Cash") None None
            let! _, childId =
                createTestAccountFromPrimitives
                    context ledger.childCode ledger.childName "Asset"
                    (today.PlusYears(-1)) None (Some "Cash") (Some parentId) None
            let! _, liabilityId =
                createTestAccountFromPrimitives
                    context ledger.liabilityCode ledger.liabilityName "Liability"
                    (today.PlusYears(-1)) None (Some "CurrentLiability") None None
            let! _ =
                createTestJournalEntryFromPrimitives
                    context "Reconciliation yesterday" None yesterday
                    [ (childId, 100.00M, "Debit", None); (liabilityId, 100.00M, "Credit", None) ] [] []
            let! _, voidedId =
                createTestJournalEntryFromPrimitives
                    context "Reconciliation voided" None yesterday
                    [ (childId, 500.00M, "Debit", None); (liabilityId, 500.00M, "Credit", None) ] [] []
            let! _ = voidedId |> voidJournalEntry context None voidReason
            let! _ =
                createTestJournalEntryFromPrimitives
                    context "Reconciliation today" None today
                    [ (parentId, 30.00M, "Debit", None); (liabilityId, 30.00M, "Credit", None) ] [] []
            return ledger
        }

    let request code (externalBalance: decimal) asOf : Result<ReconciliationRequest, IAppError> =
        result {
            let! accountCode = code |> AccountCode.create
            let! external = externalBalance |> Money.fromDecimal
            return { accountCode = accountCode; externalBalance = external; asOf = asOf }
        }

    let requests (raw: (string * decimal * NodaTime.LocalDate) list) =
        raw |> List.map (fun (code, external, asOf) -> request code external asOf) |> convertListOfResultsToResultsList

    let rowFor code (rows: ReconciliationRow list) =
        rows |> List.filter (fun row -> row.accountCode |> AccountCode.value = code)

    let ledgerBalanceOf code rows =
        rows |> rowFor code |> List.exactlyOne |> _.ledgerBalance |> Money.amount

    /// Every ledger test runs inside this rolled-back transaction, with the ledger above already written.
    let withLedger (test: Context.Context -> ReconciliationLedger -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! ledger = buildLedger context
                return! test (context |> Context.updateInitiationInstant) ledger
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-RPT-4.1 – 4.3, 4.5 — the read-only reconciliation report
    // =========================================================================

    [<Fact>]
    member _.``REQ-RPT-4.1 each input row comes back once with account code, account name, as-of date, external balance, ledger net balance and delta`` () =
        withLedger (fun context ledger ->
            result {
                let! input = requests [ ledger.childCode, 104.25M, today; ledger.liabilityCode, 118.50M, yesterday ]
                let! rows = input |> reconcile context
                Assert.Equal(2, rows |> List.length)
                let child = rows |> rowFor ledger.childCode |> List.exactlyOne
                Assert.Equal(ledger.childName, child.accountName |> AccountName.value)
                Assert.Equal(today, child.asOf)
                Assert.Equal(104.25M, child.externalBalance |> Money.amount)
                Assert.Equal(100.00M, child.ledgerBalance |> Money.amount)
                Assert.Equal(4.25M, child.delta |> Money.amount)
                let liability = rows |> rowFor ledger.liabilityCode |> List.exactlyOne
                Assert.Equal(ledger.liabilityName, liability.accountName |> AccountName.value)
                Assert.Equal(yesterday, liability.asOf)
                Assert.Equal(118.50M, liability.externalBalance |> Money.amount)
                Assert.Equal(100.00M, liability.ledgerBalance |> Money.amount)
                Assert.Equal(18.50M, liability.delta |> Money.amount)
            })

    [<Fact>]
    member _.``REQ-RPT-4.1 REQ-RPT-4.5 the delta is external minus ledger, and a non-zero delta is returned as data, not an error`` () =
        withLedger (fun context ledger ->
            result {
                let! input =
                    requests
                        [ ledger.childCode, 110.00M, today
                          ledger.liabilityCode, 125.00M, today
                          ledger.parentCode, 130.00M, today ]
                let! rows = input |> reconcile context
                let deltaOf code = (rows |> rowFor code |> List.exactlyOne).delta |> Money.amount
                Assert.Equal(10.00M, deltaOf ledger.childCode)
                Assert.Equal(-5.00M, deltaOf ledger.liabilityCode)
                Assert.Equal(0.00M, deltaOf ledger.parentCode)
            })

    [<Fact>]
    member _.``REQ-RPT-4.2 a voided journal entry contributes nothing to the ledger balance`` () =
        withLedger (fun context ledger ->
            result {
                let! input = requests [ ledger.childCode, 0.00M, today ]
                let! rows = input |> reconcile context
                Assert.Equal(100.00M, rows |> ledgerBalanceOf ledger.childCode)
            })

    [<Fact>]
    member _.``REQ-RPT-4.2 an entry dated on the as-of date counts and one dated after it does not`` () =
        withLedger (fun context ledger ->
            result {
                let! asOfYesterday = requests [ ledger.parentCode, 0.00M, yesterday ]
                let! asOfToday = requests [ ledger.parentCode, 0.00M, today ]
                let! rowsYesterday = asOfYesterday |> reconcile context
                let! rowsToday = asOfToday |> reconcile context
                // yesterday's entry counts on its own date; today's does not count as of yesterday
                Assert.Equal(100.00M, rowsYesterday |> ledgerBalanceOf ledger.parentCode)
                Assert.Equal(130.00M, rowsToday |> ledgerBalanceOf ledger.parentCode)
            })

    [<Fact>]
    member _.``REQ-RPT-4.2 the ledger balance is debits minus credits for a debit-normal account and credits minus debits for a credit-normal one`` () =
        withLedger (fun context ledger ->
            result {
                let! input = requests [ ledger.childCode, 0.00M, today; ledger.liabilityCode, 0.00M, today ]
                let! rows = input |> reconcile context
                Assert.Equal(100.00M, rows |> ledgerBalanceOf ledger.childCode)
                Assert.Equal(130.00M, rows |> ledgerBalanceOf ledger.liabilityCode)
            })

    [<Fact>]
    member _.``REQ-RPT-4.2 a parent account's ledger balance includes its descendants`` () =
        withLedger (fun context ledger ->
            result {
                let! input = requests [ ledger.parentCode, 0.00M, today ]
                let! rows = input |> reconcile context
                Assert.Equal(130.00M, rows |> ledgerBalanceOf ledger.parentCode)
            })

    [<Fact>]
    member _.``REQ-RPT-4.2 rows in one request with different as-of dates are each computed as of their own date`` () =
        withLedger (fun context ledger ->
            result {
                let! input = requests [ ledger.parentCode, 0.00M, yesterday; ledger.liabilityCode, 0.00M, today ]
                let! rows = input |> reconcile context
                Assert.Equal(100.00M, rows |> ledgerBalanceOf ledger.parentCode)
                Assert.Equal(130.00M, rows |> ledgerBalanceOf ledger.liabilityCode)
            })

    [<Fact>]
    member _.``REQ-RPT-4.3 an account code that resolves to no account fails with a typed error naming the code`` () =
        withLedger (fun context ledger ->
            result {
                let! input = requests [ ledger.childCode, 0.00M, today; "RC-9999", 0.00M, today ]
                return!
                    match input |> reconcile context with
                    | Error (AsError (ReconciliationAccountCodeNotFound code)) -> Assert.Equal("RC-9999", code); Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> Error (TestingError "Expected failure on an unknown account code; got success")
            })

    [<Fact>]
    member _.``REQ-RPT-4.3 an account code given twice fails with a typed error naming the code`` () =
        withLedger (fun context ledger ->
            result {
                let! input =
                    requests
                        [ ledger.childCode, 100.00M, today
                          ledger.liabilityCode, 0.00M, today
                          ledger.childCode, 100.00M, yesterday ]
                return!
                    match input |> reconcile context with
                    | Error (AsError (ReconciliationAccountCodeGivenTwice code)) -> Assert.Equal(ledger.childCode, code); Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> Error (TestingError "Expected failure on a repeated account code; got success")
            })

    // =========================================================================
    // REQ-RPT-4.4, 4.6 — the shadow reconciliation, always rolled back
    // =========================================================================

    [<Fact>]
    member _.``REQ-RPT-4.4 REQ-RPT-4.6 the shadow reconciliation's ledger balance includes every postable staged entry as if it were posted`` () =
        withLedger (fun context ledger ->
            result {
                let! sourceFile = "/tmp/reconciliation-shadow.jsonl" |> SourceFile.create
                let! debit = StageTestData.makeRawRow context "grp-rc-shadow" today "Reconciliation shadow" "TestBank" "REF-RC-SHADOW-001" 40.00M "Debit" (Some ledger.childCode) None
                let! credit = StageTestData.makeRawRow context "grp-rc-shadow" today "Reconciliation shadow" "TestBank" "REF-RC-SHADOW-001" 40.00M "Credit" (Some ledger.liabilityCode) None
                let! staged = [ debit; credit ] |> StageTestData.ingestDeduplicateAndClassify context sourceFile
                Assert.Equal(Classified, staged.stagedEntries |> List.exactlyOne |> StageTestData.latestStatus)
                let! input = requests [ ledger.childCode, 0.00M, today; ledger.parentCode, 0.00M, today ]
                let! rows = input |> reconcileAfterPostingStagedEntries (context |> Context.updateInitiationInstant)
                Assert.Equal(140.00M, rows |> ledgerBalanceOf ledger.childCode)
                Assert.Equal(170.00M, rows |> ledgerBalanceOf ledger.parentCode)
            })

    (* The two tests above and below this one run inside a transaction the test itself rolls back, so they cannot see
       whether the shadow reconciliation left anything behind. This one stages its entry for real, runs the shadow
       reconciliation the way its route does, and then looks at the ledger and staging from outside. *)
    [<Fact>]
    member _.``REQ-RPT-4.4 after the shadow reconciliation the ledger and staging hold exactly what they held before`` () =
        let reference = "REF-RC-SHADOW-UNTOUCHED-001"
        let accountCode = "F-5650"
        let mutable idsToCleanUp = []
        let fetchOnly () = Context.create NoTransaction FetchOnly
        try
            result {
                let! staged =
                    runCommandRouteAndAutoCompleteTransaction IngestRawEntries (fun context ->
                        result {
                            let! sourceFile = "/tmp/reconciliation-shadow-untouched.jsonl" |> SourceFile.create
                            let! debit = StageTestData.makeRawRow context "grp-rc-untouched" today "Reconciliation shadow untouched" "TestBank" reference 21.40M "Debit" (Some accountCode) None
                            let! credit = StageTestData.makeRawRow context "grp-rc-untouched" today "Reconciliation shadow untouched" "TestBank" reference 21.40M "Credit" (Some "F-1270") None
                            return! [ debit; credit ] |> StageTestData.ingestDeduplicateAndClassify context sourceFile
                        })
                idsToCleanUp <-
                    staged.stagedEntries
                    |> List.map (fun entry -> entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId |> Some)
                let! postable = fetchAllForPosting (fetchOnly ())
                let accountId = fixture.Data.entertainment5650Id
                (* F-5650 is a debit-normal leaf, so posting moves its balance by exactly its postable debits minus
                   credits, whatever else is staged. *)
                let expectedMovement =
                    postable
                    |> List.collect seLines
                    |> List.filter (fun line -> line |> StageEntryLine.accountId = Some accountId)
                    |> List.sumBy (fun line ->
                        let amount = line |> StageEntryLine.amount |> Money.amount
                        if line |> StageEntryLine.lineType = Debit then amount else -amount)
                Assert.True(expectedMovement >= 21.40M, "The entry this test staged is not postable.")
                let! input = requests [ accountCode, 0.00M, today ]
                let! before = input |> reconcile (fetchOnly ())
                let! shadow =
                    runCommandRouteAndAutoRollback IngestShadowReconcile (fun context ->
                        input |> reconcileAfterPostingStagedEntries context)
                let! after = input |> reconcile (fetchOnly ())
                Assert.Equal(
                    (before |> ledgerBalanceOf accountCode) + expectedMovement,
                    shadow |> ledgerBalanceOf accountCode)
                Assert.Equal(before |> ledgerBalanceOf accountCode, after |> ledgerBalanceOf accountCode)
                let! financialInstitution = "TestBank" |> JournalRefFinancialInstitution.create
                let! referenceText = reference |> JournalExternalReferenceText.create
                let! posted =
                    JournalEntryOrchestration.JournalEntryOrchestration.fetchByReference
                        (fetchOnly ()) (Some financialInstitution) (Some referenceText)
                Assert.Empty(posted)
                let! postableAfter = fetchAllForPosting (fetchOnly ())
                let headerIdsOf entries =
                    entries
                    |> List.map (fun entry -> entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                    |> List.sort
                Assert.Equal<StageEntryHeaderId list>(headerIdsOf postable, headerIdsOf postableAfter)
                return ()
            }
            |> railroadWrapper
        finally
            match Cleanup.cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

    [<Fact>]
    member _.``REQ-RPT-4.4 a postable entry that shadow post would reject fails the shadow reconciliation with the same error`` () =
        withLedger (fun context ledger ->
            result {
                let closedPeriodDate = (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)
                let! sourceFile = "/tmp/reconciliation-shadow-closed-period.jsonl" |> SourceFile.create
                let! debit = StageTestData.makeRawRow context "grp-rc-closed" closedPeriodDate "Reconciliation closed period" "TestBank" "REF-RC-CLOSED-001" 50.00M "Debit" (Some "F-5350") None
                let! credit = StageTestData.makeRawRow context "grp-rc-closed" closedPeriodDate "Reconciliation closed period" "TestBank" "REF-RC-CLOSED-001" 50.00M "Credit" (Some "F-1270") None
                let! _ = [ debit; credit ] |> StageTestData.ingestDeduplicateAndClassify context sourceFile
                let! input = requests [ ledger.childCode, 0.00M, today ]
                return!
                    match input |> reconcileAfterPostingStagedEntries (context |> Context.updateInitiationInstant) with
                    | Error (AsError (JournalEntryHeaderEntryDateInvalid _)) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> Error (TestingError "Expected the closed-period entry to fail the shadow reconciliation; got success")
            })
