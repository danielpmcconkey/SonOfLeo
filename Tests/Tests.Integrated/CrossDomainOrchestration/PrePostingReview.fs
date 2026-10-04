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

    /// An active account rule with the given name.
    member _.namedAccountRule (name: string) (accountCode: string) (priority: int) =
        result {
            let! pattern = "pre-posting review test" |> StringSearchPattern.create
            return!
                createClassificationRuleForTest context name accountCode priority
                    [ ("And", [ FieldMatch.Description pattern ], None) ]
        }

    /// An active account rule with a unique name.
    member this.accountRule (accountCode: string) (priority: int) =
        this.namedAccountRule $"Pre-posting review test {Guid.NewGuid()}" accountCode priority

    /// An active payment-agreement-claimant rule with a unique name.
    member _.paymentAgreementRule (legId: PaymentAgreementId) (priority: int) =
        result {
            let! pattern = "pre-posting review test" |> StringSearchPattern.create
            let! name = $"Pre-posting review test {Guid.NewGuid()}" |> ClassificationRuleName.create
            let! groups = [ ("And", [ FieldMatch.Description pattern ], None) ] |> createClassificationRuleGroupListForTest
            return!
                ClassificationOrchestration.constructNewAndPersist
                    context name (ClassificationClaimant.PaymentAgreement legId) priority groups
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
    /// Returns the Invoice's ID.
    member _.invoicePaidByLine
        agreementId legId (instanceDate: LocalDate) (invoiceDate: LocalDate) (dueDate: LocalDate)
        (invoiceAmount: decimal) (line: StageEntryLine.StageEntryLine) =
        result {
            let! amount = Money.fromDecimal invoiceAmount
            let! composite =
                InstanceOrchestration.constructNewAndPersist
                    context agreementId instanceDate
                    [ (legId, None, InvoiceDate.create(invoiceDate), DueDate.create(dueDate), InvoiceAmount.create(amount),
                       InvoiceReceived, None, None,
                       [ (TransactionPointer.Staged (line |> StageEntryLine.stageEntryLineId), None, None, None) ]) ]
            return
                composite |> InstanceOrchestration.invoiceComposites |> List.head
                |> InstanceOrchestration.invoice |> Invoice.invoiceId
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
                let! winner = s.accountRule s.loanCode 10
                let! loser = s.accountRule s.loanCode 20
                let! otherAccount = s.accountRule s.cashCode 99
                do! s.recordRun line [ olderRule ]
                // the latest run: two rules carry the line's account, and the winning priority (the lower value,
                // REQ-CR-1.6) is named
                do! s.recordRun line [ loser; winner; otherAccount ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(Some (winner |> ClassificationRule.classificationRuleName), reviewed.ruleName)
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
                let! _ = s.invoicePaidByLine agreementId legId instanceDate invoiceDate dueDate 120.00M line
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

    [<Fact>]
    member _.``REQ-RPT-7.3 when two rules in the line's latest run claim its current account at different priorities, the line names the rule with the lower priority value`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 priority"
                let line = entry |> debitLine
                let prefix = $"Pre-posting review 7.3 {Guid.NewGuid()}"
                // the higher priority value sorts first by name, so only the priority can name the winner
                let! loser = s.namedAccountRule $"{prefix} A" s.loanCode 20
                let! winner = s.namedAccountRule $"{prefix} B" s.loanCode 10
                do! s.recordRun line [ loser; winner ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(Some $"{prefix} B", reviewed.ruleName |> Option.map ClassificationRuleName.value)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 when two rules in the line's latest run claim its current account at the same priority, the line names the one first by rule name`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 name"
                let line = entry |> debitLine
                let prefix = $"Pre-posting review 7.3 {Guid.NewGuid()}"
                // created and recorded second-name-first, so neither order can stand in for the name
                let! second = s.namedAccountRule $"{prefix} B" s.loanCode 30
                let! first = s.namedAccountRule $"{prefix} A" s.loanCode 30
                do! s.recordRun line [ second; first ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(Some $"{prefix} A", reviewed.ruleName |> Option.map ClassificationRuleName.value)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 when two runs both matched the line's current account with different rules, the line names the rule from the later run`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 later run"
                let line = entry |> debitLine
                let prefix = $"Pre-posting review 7.3 {Guid.NewGuid()}"
                // the earlier run's rule would win on priority and on name, so only the run can name the later one
                let! earlier = s.namedAccountRule $"{prefix} A" s.loanCode 10
                let! later = s.namedAccountRule $"{prefix} B" s.loanCode 50
                do! s.recordRun line [ earlier ]
                do! s.recordRun line [ later ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(Some $"{prefix} B", reviewed.ruleName |> Option.map ClassificationRuleName.value)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 a later run that recorded the line only against a different account does not displace an earlier run's match with the line's current account`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! entry = s.classifiedEntry "Pre-posting review 7.3 other account later"
                let line = entry |> debitLine
                let! currentAccount = s.accountRule s.loanCode 50
                let! otherAccount = s.accountRule s.cashCode 10
                do! s.recordRun line [ currentAccount ]
                do! s.recordRun line [ otherAccount ]
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal(Some (currentAccount |> ClassificationRule.classificationRuleName), reviewed.ruleName)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.3 when a payment-agreement claimant and an account claimant both match the line in its latest run, the line names the account claimant, whatever their priorities`` () =
        inRolledBackTransaction (fun s context ->
            result {
                // the leg's debit account is the line's account, so a review that read a claimant's agreement
                // accounts would name the payment-agreement rule
                let! _, legId = s.agreement "Pre-posting review 7.3 claimant" "Pre-posting review 7.3 claimant leg"
                let! accountRule = s.accountRule s.loanCode 50
                let! winningPaymentRule = s.paymentAgreementRule legId 1
                let! losingPaymentRule = s.paymentAgreementRule legId 99
                let! winsOnPriority = s.classifiedEntry "Pre-posting review 7.3 claimant ahead"
                let! losesOnPriority = s.classifiedEntry "Pre-posting review 7.3 claimant behind"
                do! s.recordRun (winsOnPriority |> debitLine) [ winningPaymentRule; accountRule ]
                do! s.recordRun (losesOnPriority |> debitLine) [ losingPaymentRule; accountRule ]
                let! review = fetchPrePostingReview context
                let! ahead = reviewedLine review winsOnPriority (winsOnPriority |> debitLine)
                let! behind = reviewedLine review losesOnPriority (losesOnPriority |> debitLine)
                let expected = Some (accountRule |> ClassificationRule.classificationRuleName)
                Assert.Equal(expected, ahead.ruleName)
                Assert.Equal(expected, behind.ruleName)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.4 a line referenced by several Payments carries each with its amount, its Invoice's invoice date, due date, amount and payment state, and its Instance's date`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! firstAgreementId, firstLegId = s.agreement "Pre-posting review 7.4 several A" "Pre-posting review 7.4 several A leg"
                let! secondAgreementId, secondLegId = s.agreement "Pre-posting review 7.4 several B" "Pre-posting review 7.4 several B leg"
                let! entry = s.classifiedEntry "Pre-posting review 7.4 several"
                let line = entry |> debitLine
                do! s.link firstLegId line
                let firstOfThisMonth = LocalDate(s.Today.Year, s.Today.Month, 1)
                // the staged line is 100.00, so it fully pays the 100.00 Invoice and partly pays the 250.00 one
                let fully = (firstOfThisMonth, firstOfThisMonth.PlusDays(-3), firstOfThisMonth.PlusDays(7), 100.00M)
                let partly = (firstOfThisMonth.PlusMonths(1), firstOfThisMonth.PlusDays(2), firstOfThisMonth.PlusDays(20), 250.00M)
                let instanceDate (d, _, _, _) = d
                let invoiceDate (_, d, _, _) = d
                let dueDate (_, _, d, _) = d
                let invoiceAmount (_, _, _, a) = a
                let! fullyPaidId =
                    s.invoicePaidByLine firstAgreementId firstLegId (instanceDate fully) (invoiceDate fully) (dueDate fully)
                        (invoiceAmount fully) line
                let! partlyPaidId =
                    s.invoicePaidByLine secondAgreementId secondLegId (instanceDate partly) (invoiceDate partly) (dueDate partly)
                        (invoiceAmount partly) line
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal<Set<InvoiceId>>(set [ fullyPaidId; partlyPaidId ], reviewed.payments |> List.map _.invoiceId |> Set.ofList)
                let check invoiceId expected expectedState =
                    let payment = reviewed.payments |> List.find (fun p -> p.invoiceId = invoiceId)
                    Assert.Equal(100.00M, payment.paymentAmount |> Money.amount)
                    Assert.Equal(invoiceDate expected, payment.invoiceDate)
                    Assert.Equal(dueDate expected, payment.dueDate)
                    Assert.Equal(invoiceAmount expected, payment.invoiceAmount |> Money.amount)
                    Assert.Equal(expectedState, payment.paymentState)
                    Assert.Equal(instanceDate expected, payment.instanceDate)
                check fullyPaidId fully FullyPaid
                check partlyPaidId partly PartiallyPaid
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-7.4 Payments on a line are ordered by Instance date, and those sharing an Instance date by Invoice due date, whatever order they were created in`` () =
        inRolledBackTransaction (fun s context ->
            result {
                let! laterInstanceAgreementId, laterInstanceLegId = s.agreement "Pre-posting review 7.4 order A" "Pre-posting review 7.4 order A leg"
                let! laterDueAgreementId, laterDueLegId = s.agreement "Pre-posting review 7.4 order B" "Pre-posting review 7.4 order B leg"
                let! earlierDueAgreementId, earlierDueLegId = s.agreement "Pre-posting review 7.4 order C" "Pre-posting review 7.4 order C leg"
                let! entry = s.classifiedEntry "Pre-posting review 7.4 order"
                let line = entry |> debitLine
                let firstOfThisMonth = LocalDate(s.Today.Year, s.Today.Month, 1)
                let invoiceDate = firstOfThisMonth.PlusDays(-5)
                // created in the reverse of the expected order; the later Instance has the earliest due date
                let! laterInstance =
                    s.invoicePaidByLine laterInstanceAgreementId laterInstanceLegId (firstOfThisMonth.PlusMonths(1))
                        invoiceDate (firstOfThisMonth.PlusDays(1)) 300.00M line
                let! laterDue =
                    s.invoicePaidByLine laterDueAgreementId laterDueLegId firstOfThisMonth
                        invoiceDate (firstOfThisMonth.PlusDays(20)) 300.00M line
                let! earlierDue =
                    s.invoicePaidByLine earlierDueAgreementId earlierDueLegId firstOfThisMonth
                        invoiceDate (firstOfThisMonth.PlusDays(10)) 300.00M line
                let! review = fetchPrePostingReview context
                let! reviewed = reviewedLine review entry line
                Assert.Equal<InvoiceId list>([ earlierDue; laterDue; laterInstance ], reviewed.payments |> List.map _.invoiceId)
                return ()
            })
