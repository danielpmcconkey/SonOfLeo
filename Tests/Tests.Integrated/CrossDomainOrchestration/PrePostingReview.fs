namespace Tests.Integrated.CrossDomainOrchestration

open System
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.PrePostingReview
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open Xunit

(* Builds the pre-posting review's inputs inside the caller's context. The fixture's staged entries are all Posted,
   Duplicate or Ignored, so the review's only entries are the ones a test stages; tests still look only at their own.
   Staged entries are 100.00, Debit F-2230 and Credit F-1280, the fixture's cash flow accounts. *)
type PrePostingScenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today()
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cashId = accountIdOf "F-1280"
    let sourceNamed name =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = name)
    let start = Clock.now()
    let mutable ticks = 0L
    /// A later instant each call, so no two transitions, runs or entries share one.
    let nextInstant () =
        ticks <- ticks + 1L
        start.Plus(Duration.FromSeconds ticks)

    member _.Today = today
    member _.testBank = sourceNamed "TestBank"
    member _.testCreditCardCo = sourceNamed "TestCreditCardCo"
    member _.loanCode = "F-2230"
    member _.cashCode = "F-1280"

    /// A staged entry that went Ingested, then through each given (status, mechanism). Lines are
    /// (amount, line type, account code, memo).
    member _.stagedEntryWith
        (source: IngestionSource.IngestionSource)
        (description: string)
        (fiReference: string)
        (entryDate: LocalDate)
        (lines: (decimal * string * string option * string option) list)
        (statuses: (string * string) list) =
        result {
            let first = nextInstant ()
            let transitions =
                ("Ingested", "StageIngestion") :: statuses
                |> List.pairwise
                |> List.map (fun ((prior, _), (status, mechanism)) -> (Some prior, status, nextInstant (), mechanism))
                |> fun later -> (None, "Ingested", first, "StageIngestion") :: later
            return!
                createStageEntryForTest context "/tmp/pre-posting-review-test.dat" description fiReference source entryDate
                    (lines |> List.map (fun (amount, lineType, code, memo) -> (amount, lineType, code, memo, None)))
                    transitions
        }

    /// A 100.00 staged entry, Debit F-2230 then Credit F-1280, through the given statuses.
    member this.stagedEntry (description: string) (entryDate: LocalDate) (statuses: (string * string) list) =
        this.stagedEntryWith this.testBank description (Guid.NewGuid().ToString()) entryDate
            [ (100.00M, "Debit", Some "F-2230", None); (100.00M, "Credit", Some "F-1280", None) ]
            statuses

    member this.classifiedEntry (description: string) =
        this.stagedEntry description today [ ("Classified", "Classifier") ]

    /// An active account rule with a unique name.
    member _.accountRule (accountCode: string) (priority: int) =
        result {
            let! pattern = "pre-posting review test" |> StringSearchPattern.create
            return!
                createClassificationRuleForTest context $"Pre-posting review test {Guid.NewGuid()}" accountCode priority
                    [ ("And", [ FieldMatch.Description pattern ], None) ]
        }

    /// Records one classification run matching each given rule against the line, at a later instant than any before.
    member _.recordRun (line: StageEntryLine.StageEntryLine) (rules: ClassificationRule.ClassificationRule list) =
        let runId = ClassificationRunId.create()
        let instant = nextInstant ()
        rules
        |> List.map (fun rule ->
            RuleMatch.create (ClassificationMatchId.create()) runId (line |> StageEntryLine.stageEntryLineId)
                (rule |> ClassificationRule.classificationRuleId) instant
            |> RuleMatch.persist context)
        |> convertListOfResultsToResultsList
        |> Result.map ignore

    /// An Outgo agreement, monthly on the 1st, with one 100.00 leg (debit F-2230, credit F-1280). Returns the
    /// agreement's ID and its leg's.
    member _.agreement (name: string) (legNameStr: string) =
        result {
            let firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Pre-posting review test lender" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (firstOfThisMonth.PlusMonths(-2)) None
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = legNameStr |> PaymentAgreementName.create
            let! expected = Money.fromDecimal 100.00M
            let! due = 0 |> DaysDueAfterInvoiceDate.create
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None
                    [ (legName, (DebitAccount.create loanId), (CreditAccount.create cashId), Some expected, Some due, None) ]
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legId =
                agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
            return agreementId, legId
        }

    member _.link legId (line: StageEntryLine.StageEntryLine) =
        CashFlowOps.constructNewAndPersist context legId (line |> StageEntryLine.stageEntryLineId)
        |> Result.map ignore

    /// An Invoice of invoiceAmount on its own Instance dated instanceDate, with one Payment on the staged line.
    member _.invoicePaidByLine
        agreementId legId (instanceDate: LocalDate) (invoiceDate: LocalDate) (dueDate: LocalDate)
        (invoiceAmount: decimal) (line: StageEntryLine.StageEntryLine) =
        result {
            let! amount = Money.fromDecimal invoiceAmount
            let! _ =
                InstanceOrchestration.constructNewAndPersist
                    context agreementId instanceDate
                    [ (legId, None, InvoiceDate.create(invoiceDate), DueDate.create(dueDate), InvoiceAmount.create(amount),
                       InvoiceReceived, None, None,
                       [ (TransactionPointer.Staged (line |> StageEntryLine.stageEntryLineId), None, None, None) ]) ]
            return ()
        }

module PrePostingReviewTestHelpers =
    let headerIdOf (entry: StageEntryOrchestration.StageEntry) =
        entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

    let lineOfType lineType (entry: StageEntryOrchestration.StageEntry) =
        entry |> StageEntryOrchestration.seLines |> List.find (fun l -> l |> StageEntryLine.lineType = lineType)

    let debitLine entry = entry |> lineOfType Debit

    let reviewedEntry (review: PrePostingEntry list) (entry: StageEntryOrchestration.StageEntry) =
        match review |> List.tryFind (fun e -> e.stageEntryHeaderId = (entry |> headerIdOf)) with
        | Some e -> Ok e
        | None -> TestError.error (TestingError "The staged entry is not in the review")

    let reviewedLine (review: PrePostingEntry list) (entry: StageEntryOrchestration.StageEntry) line =
        result {
            let! e = reviewedEntry review entry
            return e.lines |> List.find (fun l -> l.stageEntryLineId = (line |> StageEntryLine.stageEntryLineId))
        }

open PrePostingReviewTestHelpers

[<Collection("SharedTestData")>]
type PrePostingReviewTests(fixture: TestDataFixture) =

    let inRolledBackTransaction (test: PrePostingScenario -> Context.Context -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback FetchOnly (fun context -> test (PrePostingScenario(fixture, context)) context)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-7.1 the review includes every staged entry with status Classified or Reviewed`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! classified = s.classifiedEntry "Pre-posting review 7.1 classified"
                let! reviewed =
                    s.stagedEntry "Pre-posting review 7.1 reviewed" s.Today
                        [ ("Classified", "Classifier"); ("Reviewed", "Operator") ]
                let! review = fetchPrePostingReview context
                let! classifiedRow = reviewedEntry review classified
                let! reviewedRow = reviewedEntry review reviewed
                Assert.Equal(StagedEntryStatus.Classified, classifiedRow.status)
                Assert.Equal(StagedEntryStatus.Reviewed, reviewedRow.status)
                return ()
            })

    [<Theory>]
    [<InlineData("Ingested", false)>]
    [<InlineData("Classified", true)>]
    [<InlineData("NoMatch", false)>]
    [<InlineData("Conflict", false)>]
    [<InlineData("Reviewed", true)>]
    [<InlineData("Duplicate", false)>]
    [<InlineData("Posted", false)>]
    [<InlineData("Ignored", false)>]
    member _.``REQ-RPT-7.1 a staged entry is in the review exactly when its status is Classified or Reviewed, for each of the eight statuses`` (status: string, included: bool) =
        let statuses =
            match status with
            | "Ingested" -> []
            | "Classified" -> [ ("Classified", "Classifier") ]
            | "NoMatch" -> [ ("NoMatch", "Classifier") ]
            | "Conflict" -> [ ("Conflict", "Classifier") ]
            | "Reviewed" -> [ ("Classified", "Classifier"); ("Reviewed", "Operator") ]
            | "Duplicate" -> [ ("Duplicate", "Deduplicator") ]
            | "Posted" -> [ ("Classified", "Classifier"); ("Reviewed", "Operator"); ("Posted", "LedgerPoster") ]
            | "Ignored" -> [ ("Ignored", "Operator") ]
            | other -> failwith $"no row for status {other}"
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.stagedEntry $"Pre-posting review 7.1 {status}" s.Today statuses
                let! review = fetchPrePostingReview context
                let reviewedIds = review |> List.map _.stageEntryHeaderId
                let () =
                    if included then Assert.Contains(entry |> headerIdOf, reviewedIds)
                    else Assert.DoesNotContain(entry |> headerIdOf, reviewedIds)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.2 each entry carries its entry date, description, source name, fi_reference and status`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let entryDate = s.Today.PlusDays(-3)
                let! entry =
                    s.stagedEntryWith s.testCreditCardCo "Pre-posting review 7.2 header" "FI-REF-7-2" entryDate
                        [ (40.00M, "Debit", Some s.loanCode, None); (40.00M, "Credit", Some s.cashCode, None) ]
                        [ ("Classified", "Classifier"); ("Reviewed", "Operator") ]
                let! review = fetchPrePostingReview context
                let! row = reviewedEntry review entry
                Assert.Equal(entryDate, row.entryDate)
                Assert.Equal("Pre-posting review 7.2 header", row.description |> JournalEntryDescription.value)
                Assert.Equal("TestCreditCardCo", row.sourceName |> JournalRefFinancialInstitution.value)
                Assert.Equal("FI-REF-7-2", row.fiReference |> JournalExternalReferenceText.value)
                Assert.Equal(StagedEntryStatus.Reviewed, row.status)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.2 each line carries its line type, amount, memo, account code and account name, and an entry's debit lines come before its credit lines`` () =
        inRolledBackTransaction (fun s context ->
            result {
                // credits staged first, so the order has to come from the review
                let! entry =
                    s.stagedEntryWith s.testBank "Pre-posting review 7.2 lines" (Guid.NewGuid().ToString()) s.Today
                        [ (100.00M, "Credit", Some s.cashCode, Some "cash out")
                          (60.00M, "Debit", Some s.loanCode, Some "principal")
                          (40.00M, "Debit", Some s.loanCode, None) ]
                        [ ("Classified", "Classifier") ]
                let! review = fetchPrePostingReview context
                let! row = reviewedEntry review entry
                Assert.Equal<JournalEntryLineType list>([ Debit; Debit; Credit ], row.lines |> List.map _.lineType)
                let cashAccount =
                    fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = s.cashCode)
                let credit = row.lines |> List.last
                Assert.Equal(100.00M, credit.amount |> Money.amount)
                Assert.Equal(Some "cash out", credit.memo |> Option.map JournalEntryLineMemo.value)
                Assert.Equal(s.cashCode, credit.accountCode |> AccountCode.value)
                Assert.Equal(cashAccount |> Account.accountName |> AccountName.value, credit.accountName |> AccountName.value)
                let principal = row.lines |> List.find (fun l -> l.amount |> Money.amount = 60.00M)
                Assert.Equal(Some "principal", principal.memo |> Option.map JournalEntryLineMemo.value)
                Assert.Equal(s.loanCode, principal.accountCode |> AccountCode.value)
                let unmemoed = row.lines |> List.find (fun l -> l.amount |> Money.amount = 40.00M)
                Assert.Equal(None, unmemoed.memo)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 a line carries the name of the rule recorded against it in its most recent classification run whose account is the line's current account`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 named"
                let line = entry |> debitLine
                let! olderRule = s.accountRule s.loanCode 50
                let! lowerPriority = s.accountRule s.loanCode 10
                let! winner = s.accountRule s.loanCode 20
                let! otherAccount = s.accountRule s.cashCode 99
                do! s.recordRun line [ olderRule ]
                // the latest run: two rules carry the line's account, and the higher priority one is named
                do! s.recordRun line [ lowerPriority; winner; otherAccount ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(Some (winner |> ClassificationRule.classificationRuleName), reviewed.ruleName)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 a line whose account no rule in its most recent classification run carries has an empty rule name, even when an older run recorded a rule with that account`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 changed"
                let line = entry |> debitLine
                let! olderRule = s.accountRule s.loanCode 50
                let! latestRule = s.accountRule s.cashCode 50
                do! s.recordRun line [ olderRule ]
                do! s.recordRun line [ latestRule ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(None, reviewed.ruleName)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 a line no classification run has recorded has an empty rule name`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 unrecorded"
                let! review = fetchPrePostingReview context
                let! row = reviewedEntry review entry
                Assert.All(row.lines, fun l -> Assert.Equal(None, l.ruleName))
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.4 a line linked to a Payment Agreement that no Payment references carries the Payment Agreement's and Master Agreement's names and no Payment, Invoice or Instance`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! _, legId = s.agreement "Pre-posting review 7.4 unpaid" "Pre-posting review 7.4 unpaid leg"
                let! entry = s.classifiedEntry "Pre-posting review 7.4 unpaid"
                let line = entry |> debitLine
                do! s.link legId line
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                let a = Assert.Single(reviewed.agreement |> Option.toList)
                Assert.Equal("Pre-posting review 7.4 unpaid leg", a.paymentAgreementName |> PaymentAgreementName.value)
                Assert.Equal("Pre-posting review 7.4 unpaid", a.masterAgreementName |> AgreementName.value)
                Assert.Empty(reviewed.payments)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.4 a linked line a Payment references also carries the Payment's amount, its Invoice's invoice date, due date, amount and payment state, and its Instance's date`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! agreementId, legId = s.agreement "Pre-posting review 7.4 paid" "Pre-posting review 7.4 paid leg"
                let! entry = s.classifiedEntry "Pre-posting review 7.4 paid"
                let line = entry |> debitLine
                do! s.link legId line
                let instanceDate = LocalDate(s.Today.Year, s.Today.Month, 1)
                let invoiceDate = instanceDate.PlusDays(-5)
                let dueDate = instanceDate.PlusDays(10)
                do! s.invoicePaidByLine agreementId legId instanceDate invoiceDate dueDate 120.00M line
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.True(reviewed.agreement.IsSome, "Expected the line to carry its agreement")
                let payment = Assert.Single(reviewed.payments)
                Assert.Equal(100.00M, payment.paymentAmount |> Money.amount)
                Assert.Equal(invoiceDate, payment.invoiceDate)
                Assert.Equal(dueDate, payment.dueDate)
                Assert.Equal(120.00M, payment.invoiceAmount |> Money.amount)
                Assert.Equal(PartiallyPaid, payment.paymentState)
                Assert.Equal(instanceDate, payment.instanceDate)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.4 a line with no link carries no agreement, Payment, Invoice or Instance`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.4 unlinked"
                let! review = fetchPrePostingReview context
                let! row = reviewedEntry review entry
                Assert.All(row.lines, fun l ->
                    Assert.Equal(None, l.agreement)
                    Assert.Empty(l.payments))
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.5 a postable entry with a line that has no account fails the review with a typed error naming the entry and the line`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry =
                    s.stagedEntryWith s.testBank "Pre-posting review 7.5" (Guid.NewGuid().ToString()) s.Today
                        [ (100.00M, "Debit", None, None); (100.00M, "Credit", Some s.cashCode, None) ]
                        [ ("Classified", "Classifier"); ("Reviewed", "Operator") ]
                let expectedEntry = entry |> headerIdOf |> StageEntryHeaderId.value
                let expectedLine = entry |> debitLine |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value
                return!
                    match fetchPrePostingReview context with
                    | Error (AsError (DataIngestionError.IngestionPrePostingReviewLineHasNoAccount (entryId, lineId))) ->
                        Assert.Equal(expectedEntry, entryId)
                        Assert.Equal(expectedLine, lineId)
                        Ok ()
                    | Error e -> TestError.error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> TestError.error (TestingError "Expected the review to fail; it succeeded")
            })

    [<Fact>]
    member _.``REQ-RPT-7.6 entries are ordered by entry date, then source name, then fi_reference`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let lines = [ (100.00M, "Debit", Some s.loanCode, None); (100.00M, "Credit", Some s.cashCode, None) ]
                let classified = [ ("Classified", "Classifier") ]
                let later = s.Today
                let earlier = s.Today.PlusDays(-1)
                let prefix = Guid.NewGuid().ToString()
                // staged in the reverse of the expected order, all with one description so only date, source
                // name and fi_reference can order them
                let! laterDate = s.stagedEntryWith s.testBank "7.6 ordering" $"{prefix}-A" later lines classified
                let! testCreditCardCoB = s.stagedEntryWith s.testCreditCardCo "7.6 ordering" $"{prefix}-B" earlier lines classified
                let! testCreditCardCoA = s.stagedEntryWith s.testCreditCardCo "7.6 ordering" $"{prefix}-A" earlier lines classified
                let! testBank = s.stagedEntryWith s.testBank "7.6 ordering" $"{prefix}-Z" earlier lines classified
                let expected = [ testBank; testCreditCardCoA; testCreditCardCoB; laterDate ] |> List.map headerIdOf
                let! review = fetchPrePostingReview context
                let actual =
                    review |> List.map _.stageEntryHeaderId |> List.filter (fun id -> expected |> List.contains id)
                Assert.Equal<StageEntryHeaderId list>(expected, actual)
                return ()
            })

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-RPT-7.3 when two rules in the line's latest run claim its current account at different priorities, the line names the rule with the lower priority value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 when two rules in the line's latest run claim its current account at the same priority, the line names the one first by rule name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 when two runs both matched the line's current account with different rules, the line names the rule from the later run`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 a later run that recorded the line only against a different account does not displace an earlier run's match with the line's current account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 when a payment-agreement claimant and an account claimant both match the line in its latest run, the line names the account claimant, whatever their priorities`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-7.4 a line referenced by several Payments carries each with its amount, its Invoice's invoice date, due date, amount and payment state, and its Instance's date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-7.4 Payments on a line are ordered by Instance date, and those sharing an Instance date by Invoice due date, whatever order they were created in`` () =
        Assert.Fail "Not yet implemented"
