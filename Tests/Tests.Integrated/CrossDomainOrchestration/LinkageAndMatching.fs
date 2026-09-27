module Tests.Integrated.CrossDomainOrchestration.LinkageAndMatching

open System
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
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open Xunit

(* Every test here builds its own agreements, rules, staged entries and Invoices inside a transaction that is rolled
   back, on the fixture's cash flow accounts: F-2230 (a liability) and F-1280 (cash) for Outgo agreements, F-1280 and
   F-4290 (revenue) for Income agreements. Each agreement has one leg. A staged entry's lines all carry accounts, so a
   leg is found by the direction default unless a test says otherwise. *)

let private statusMechanism status =
    match status with
    | "Classified" | "NoMatch" | "Conflict" -> "Classifier"
    | "Reviewed" | "Ignored" -> "Operator"
    | "Duplicate" -> "Deduplicator"
    | "Posted" -> "LedgerPoster"
    | other -> failwith $"no mechanism for status {other}"

/// The status path from Ingested that ends at the given status.
let private pathTo status =
    match status with
    | "Ingested" -> []
    | "Classified" | "NoMatch" | "Conflict" | "Duplicate" | "Ignored" -> [ status ]
    | "Reviewed" -> [ "Classified"; "Reviewed" ]
    | "Posted" -> [ "Classified"; "Posted" ]
    | other -> failwith $"no path to status {other}"

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today()
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")

    member _.loanId = accountIdOf "F-2230"
    member _.cashId = accountIdOf "F-1280"
    member _.revenueId = accountIdOf "F-4290"
    member _.firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
    member this.firstOfLastMonth = this.firstOfThisMonth.PlusMonths(-1)

    /// An agreement with one 100.00 leg, active since three months ago. Returns the agreement's id and its leg's id.
    member this.agreementWithCadence (name: string) (direction: FlowDirection) (cadence: Cadence.CadenceType) (nextInstance: LocalDate) =
        result {
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Linkage and matching test counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (this.firstOfThisMonth.PlusMonths(-3)) None
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = $"{name} leg" |> PaymentAgreementName.create
            let! expected = Money.fromDecimal 100.00M
            let! due = 0 |> DaysDueAfterInvoiceDate.create
            let debit, credit =
                match direction with
                | Outgo -> this.loanId, this.cashId
                | Income -> this.cashId, this.revenueId
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName direction cadence { nextInstance = nextInstance } counterparty activityPeriod None
                    [ (legName, DebitAccount debit, CreditAccount credit, Some expected, Some due, None) ]
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legId =
                agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
            return agreementId, legId
        }

    /// Monthly on the 1st, next instance the 1st of next month.
    member this.agreement (name: string) (direction: FlowDirection) =
        result {
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            return!
                this.agreementWithCadence name direction (Cadence.Monthly(Cadence.DateInMonth first))
                    (this.firstOfThisMonth.PlusMonths(1))
        }

    /// A 100.00 Invoice on its own Instance dated invoiceDate, due daysDue later, carrying the given Payments.
    /// Returns the Instance's id and the Invoice's id.
    member _.invoiceWithPayments agreementId legId (state: InvoiceState) (invoiceDate: LocalDate) (daysDue: int)
            (payments: (TransactionPointer * decimal) list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let! newPayments =
                payments
                |> List.map (fun (pointer, paid) ->
                    Money.fromDecimal paid |> Result.map (fun money -> (pointer, { money = money }, None, None, None)))
                |> convertListOfResultsToResultsList
            let! created =
                InstanceOrchestration.createInstanceCompositeAndSaveToDb
                    context agreementId invoiceDate
                    [ (legId, None, { localDate = invoiceDate }, { localDate = invoiceDate.PlusDays(daysDue) },
                       { money = amount }, state, None, None, newPayments) ]
            let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
            let invoiceId =
                created |> InstanceOrchestration.invoiceComposites |> List.head
                |> InstanceOrchestration.invoice |> Invoice.invoiceId
            return instanceId, invoiceId
        }

    member this.outgoInvoice agreementId legId (invoiceDate: LocalDate) (daysDue: int) =
        this.invoiceWithPayments agreementId legId InvoiceReceived invoiceDate daysDue []

    /// A staged entry with the given lines (amount, line type, account code) that went Ingested, then along the
    /// path to the given status. Returns its header id and its lines in the order given.
    member _.stagedEntryWithLines (description: string) (entryDate: LocalDate) (status: string)
            (lines: (decimal * string * string) list) =
        result {
            let start = Clock.now()
            let transitions =
                ("Ingested" :: pathTo status)
                |> List.pairwise
                |> List.mapi (fun i (prior, next) ->
                    (Some prior, next, start.Plus(Duration.FromMilliseconds(int64 ((i + 1) * 10))), statusMechanism next))
                |> fun later -> (None, "Ingested", start, "StageIngestion") :: later
            let! entry =
                createStageEntryForTest context "/tmp/linkage-and-matching-test.dat" description
                    (Guid.NewGuid().ToString()) testBank entryDate
                    (lines |> List.map (fun (amount, lineType, code) -> (amount, lineType, Some code, None, None)))
                    transitions
            let headerId = entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
            let created = entry |> StageEntryOrchestration.seLines
            (* Return the lines in the order the test gave them, matched on line type, amount and account. *)
            let ordered =
                lines
                |> List.fold (fun (taken: StageEntryLine.StageEntryLine list, remaining) (amount, lineType, code) ->
                    let wanted = accountIdOf code
                    let found =
                        remaining
                        |> List.find (fun line ->
                            (line |> StageEntryLine.amount |> Money.amount) = amount
                            && (line |> StageEntryLine.lineType |> JournalEntryLineType.toString) = lineType
                            && (line |> StageEntryLine.accountId) = Some wanted)
                    taken @ [ found ], remaining |> List.filter (fun line -> line <> found))
                    ([], created)
                |> fst
                |> List.map StageEntryLine.stageEntryLineId
            return headerId, ordered
        }

    /// A 100.00 Outgo-shaped entry (Debit F-2230, Credit F-1280). Returns its header id, its Debit line, its Credit line.
    member this.outgoEntry (description: string) (entryDate: LocalDate) (status: string) =
        result {
            let! headerId, lines =
                this.stagedEntryWithLines description entryDate status
                    [ (100.00M, "Debit", "F-2230"); (100.00M, "Credit", "F-1280") ]
            return headerId, lines.[0], lines.[1]
        }

    /// An Outgo-shaped Classified entry whose Debit line is linked to the leg. Returns the Debit line.
    member this.linkedLine legId (description: string) (entryDate: LocalDate) =
        result {
            let! _, debitLineId, _ = this.outgoEntry description entryDate "Classified"
            let! _ = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legId debitLineId
            return debitLineId
        }

    /// An active rule claiming, for the leg, any entry whose description matches, optionally pinned to a line type.
    member _.paymentAgreementRuleWith legId (description: string) (priority: int) (lineType: string option) =
        result {
            let! name = $"Linkage and matching test: {description} {priority} {lineType} {Guid.NewGuid()}" |> ClassificationRuleName.create
            let! pattern = description |> StringSearchPattern.create
            let! lineTypeMatch =
                match lineType with
                | None -> Ok []
                | Some lt -> lt |> JournalEntryLineType.fromString |> Result.map (fun t -> [ FieldMatch.LineType t ])
            let! groups =
                [ ("And", (FieldMatch.Description pattern) :: lineTypeMatch, None) ]
                |> createClassificationRuleGroupListForTest
            return!
                ClassificationOrchestration.createNewClassificationRule
                    context name (ClassificationClaimant.PaymentAgreement legId) priority groups
        }

    member this.paymentAgreementRule legId (description: string) =
        this.paymentAgreementRuleWith legId description 500 None

    member _.setRuleActive (isActive: bool) (rule: ClassificationRule.ClassificationRule) =
        rule
        |> ClassificationRule.classificationRuleId
        |> ClassificationOrchestration.updateClassificationRule
            context FieldUpdate.NoChange FieldUpdate.NoChange FieldUpdate.NoChange FieldUpdate.NoChange
            (FieldUpdate.SetTo isActive)

    member _.statusOf (headerId: StageEntryHeaderId) =
        headerId |> StageEntryHeader.fetchById context |> Result.map StageEntryHeader.currentStatus

    member _.linksOf (lineId: StageEntryLineId) =
        lineId |> PaymentAgreementLink.fetchByStageEntryLineId context

    member _.paymentsOn (lineId: StageEntryLineId) =
        [ lineId ] |> Payment.fetchByStageEntryLineIdList context

    member _.paymentsOnInvoice (invoiceId: InvoiceId) =
        [ invoiceId ] |> Payment.fetchByInvoiceIdList context

    /// Journal entries in every fiscal period the fixture made.
    member _.journalEntryCount () =
        (fixture.Data.closedFiscalPeriodId :: fixture.Data.openFiscalPeriodIds)
        |> List.map (fun periodId -> periodId |> JournalEntryHeader.fetchByPeriod context |> Result.map List.length)
        |> convertListOfResultsToResultsList
        |> Result.map List.sum

    /// Every Instance and every Invoice in the book, fulfilled or not.
    member _.instanceAndInvoiceIds () =
        result {
            let! openOnes = false |> InstanceOrchestration.fetchCompositesByIsFulfilled context
            let! fulfilledOnes = true |> InstanceOrchestration.fetchCompositesByIsFulfilled context
            let all = openOnes @ fulfilledOnes
            let instanceIds = all |> List.map (InstanceOrchestration.instance >> Instance.instanceId) |> Set.ofList
            let invoiceIds =
                all
                |> List.collect InstanceOrchestration.invoiceComposites
                |> List.map (InstanceOrchestration.invoice >> Invoice.invoiceId)
                |> Set.ofList
            return instanceIds, invoiceIds
        }

let private decisionsNaming (lineId: StageEntryLineId) (run: InstanceOrchestration.PaymentAgreementClassificationResult) =
    run.decisionLog |> List.filter (fun decision -> decision.stageEntryLineId = lineId)

let private invoiceDecisionsFor (invoiceId: InvoiceId) (run: InstanceOrchestration.PaymentAgreementClassificationResult) =
    run.invoiceDecisionLog |> List.filter (fun decision -> decision.invoiceId = invoiceId)

let private orphansIn (run: Result<'T, IAppError>) : Result<(Guid * Guid * CashFlowError.OrphanedLineReason) list, IAppError> =
    match run with
    | Error (AsError (CashFlowError.CashflowPaymentAgreementLinksOrphaned orphans)) -> Ok orphans
    | Error e -> TestError.error (TestingError $"Wrong error. {e.ToMessage()}")
    | Ok _ -> TestError.error (TestingError "Expected the run to fail on orphaned lines; it succeeded")

/// The candidate lines of a decision that found several, or an error naming what it found instead.
let private manyCandidatesOf (decision: InvoiceDecision) : Result<Set<StageEntryLineId>, IAppError> =
    match decision.outcome with
    | ManyCandidateEntries lineIds -> Ok (lineIds |> Set.ofList)
    | other -> TestError.error (TestingError $"Expected several candidates; got {other}")

let private cadenceFor (name: string) (date: LocalDate) : Cadence.CadenceType * LocalDate =
    let weekDay = date.DayOfWeek |> Cadence.WeekDay.fromIsoDayOfWeek
    let first = 1 |> Cadence.DateInMonthNumber.fromInt |> Result.defaultWith (fun e -> failwith (e.ToMessage()))
    let month = (Cadence.Month.fromString (date.ToString("MMMM", Globalization.CultureInfo.InvariantCulture)))
                |> Result.defaultWith (fun e -> failwith (e.ToMessage()))
    match name with
    | "Daily" -> Cadence.Daily, date.PlusDays(1)
    | "Weekly" -> Cadence.Weekly weekDay, date.PlusDays(7)
    | "EveryOtherWeek" -> Cadence.EveryOtherWeek weekDay, date.PlusDays(14)
    | "Monthly" -> Cadence.Monthly(Cadence.DateInMonth first), date.PlusMonths(1)
    | "Annually" -> Cadence.Annually(month, Cadence.DateInMonth first), date.PlusYears(1)
    | other -> failwith $"no cadence {other}"

[<Collection("SharedTestData")>]
type LinkageAndMatchingTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-12.1 — what a link carries
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.1 REQ-SYS-3.2 a link the operator creates reads back with a system-generated ID, the Payment Agreement and staged line it was created for, and created and modified instants both equal to the creating operation's initiation instant`` () =
        runCommandRouteAndAutoRollback CreatePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.1 operator" Outgo
                let! _, lineId, _ = scenario.outgoEntry "CF-12.1 operator payment" scenario.firstOfThisMonth "Classified"
                let! created = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legId lineId
                let! readBack = created |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLink.fetchById context
                let initiation = context |> Context.getInitiationInstant
                Assert.NotEqual(Guid.Empty, readBack |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLinkId.value)
                Assert.Equal(legId, readBack |> PaymentAgreementLink.paymentAgreementId)
                Assert.Equal(lineId, readBack |> PaymentAgreementLink.stageEntryLineId)
                Assert.Equal(initiation, readBack |> PaymentAgreementLink.createdAt)
                Assert.Equal(initiation, readBack |> PaymentAgreementLink.modifiedAt)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.1 REQ-SYS-3.2 a link a classification run creates reads back with a system-generated ID, the claimed Payment Agreement and line, and created and modified instants both equal to the run's initiation instant`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.1 run" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRule legId "CF-12.1 run payment"
                let! _, lineId, _ = scenario.outgoEntry "CF-12.1 run payment" scenario.firstOfThisMonth "Classified"
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                let link = Assert.Single(links)
                let initiation = context |> Context.getInitiationInstant
                Assert.NotEqual(Guid.Empty, link |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLinkId.value)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
                Assert.Equal(lineId, link |> PaymentAgreementLink.stageEntryLineId)
                Assert.Equal(initiation, link |> PaymentAgreementLink.createdAt)
                Assert.Equal(initiation, link |> PaymentAgreementLink.modifiedAt)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.2 — one Payment Agreement per line
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.2 REQ-CF-12.3 a classification run leaves an already-linked line with its one existing link when a rule for a different Payment Agreement claims it`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-12.2 existing X" Outgo
                let! _, legYId = scenario.agreement "CF-12.2 claimant Y" Outgo
                let! _ = scenario.outgoInvoice agreementXId legXId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legXId "CF-12.2 existing payment" scenario.firstOfThisMonth
                let! _ = scenario.paymentAgreementRule legYId "CF-12.2 existing payment"
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                let link = Assert.Single(links)
                Assert.Equal(legXId, link |> PaymentAgreementLink.paymentAgreementId)
                Assert.Empty(run |> decisionsNaming lineId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.2 REQ-CF-12.5 a line that rules of different priority for two Payment Agreements each claim, and that is the only claimant of each, is linked to the higher-priority rule's Payment Agreement and not the other`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-12.2 higher X" Outgo
                let! agreementYId, legYId = scenario.agreement "CF-12.2 lower Y" Outgo
                let! _ = scenario.outgoInvoice agreementXId legXId scenario.firstOfThisMonth 30
                let! _ = scenario.outgoInvoice agreementYId legYId scenario.firstOfThisMonth 30
                (* A lower priority value is the higher priority. *)
                let! _ = scenario.paymentAgreementRuleWith legXId "CF-12.2 priority payment" 100 None
                let! _ = scenario.paymentAgreementRuleWith legYId "CF-12.2 priority payment" 200 None
                let! _, lineId, _ = scenario.outgoEntry "CF-12.2 priority payment" scenario.firstOfThisMonth "Classified"
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                let link = Assert.Single(links)
                Assert.Equal(legXId, link |> PaymentAgreementLink.paymentAgreementId)
                let! linksToY = legYId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Empty(linksToY)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.3 — which lines linkage considers
    // =========================================================================

    [<Theory>]
    [<InlineData("Ingested")>]
    [<InlineData("Classified")>]
    [<InlineData("NoMatch")>]
    [<InlineData("Conflict")>]
    member _.``REQ-CF-12.3 for each of Ingested, Classified, NoMatch and Conflict, an unlinked line of a staged entry in that status that an active Payment Agreement rule claims is linked to that rule's Payment Agreement`` (status: string) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement $"CF-12.3 {status}" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRule legId $"CF-12.3 {status} payment"
                let! _, lineId, _ = scenario.outgoEntry $"CF-12.3 {status} payment" scenario.firstOfThisMonth status
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                let link = Assert.Single(links)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-CF-12.3 for each of Posted, Duplicate and Ignored, a line of a staged entry in that status that an active Payment Agreement rule claims is neither linked nor reported as a claim`` (status: string) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement $"CF-12.3 {status}" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRule legId $"CF-12.3 {status} payment"
                let! _, debitLineId, creditLineId =
                    scenario.outgoEntry $"CF-12.3 {status} payment" scenario.firstOfThisMonth status
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! debitLinks = scenario.linksOf debitLineId
                let! creditLinks = scenario.linksOf creditLineId
                Assert.Empty(debitLinks)
                Assert.Empty(creditLinks)
                Assert.Empty(run |> decisionsNaming debitLineId)
                Assert.Empty(run |> decisionsNaming creditLineId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.3 a line that an inactive Payment Agreement rule matches is neither linked nor reported as a claim, and the same rule made active links it`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.3 inactive" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! rule = scenario.paymentAgreementRule legId "CF-12.3 inactive payment"
                let! inactive = rule |> scenario.setRuleActive false
                Assert.False(inactive |> ClassificationRule.isActive)
                let! _, lineId, _ = scenario.outgoEntry "CF-12.3 inactive payment" scenario.firstOfThisMonth "Classified"
                let! firstRun = CashFlowOps.classifyPaymentAgreements context
                let! linksAfterFirst = scenario.linksOf lineId
                Assert.Empty(linksAfterFirst)
                Assert.Empty(firstRun |> decisionsNaming lineId)
                let! _ = rule |> scenario.setRuleActive true
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! linksAfterSecond = scenario.linksOf lineId
                let link = Assert.Single(linksAfterSecond)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.3 a line already assigned an account is linked when an active Payment Agreement rule claims it`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.3 assigned" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRule legId "CF-12.3 assigned payment"
                let! _, lineId, _ = scenario.outgoEntry "CF-12.3 assigned payment" scenario.firstOfThisMonth "Classified"
                let! lines = [ lineId ] |> StageEntryLine.fetchByIdList context
                let line = Assert.Single(lines)
                Assert.Equal(Some scenario.loanId, line |> StageEntryLine.accountId)
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                let link = Assert.Single(links)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.3 a line matched only by an active rule whose claimant is an account, not a Payment Agreement, is neither linked nor reported as a claim`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! pattern = "CF-12.3 account claimant payment" |> StringSearchPattern.create
                let! _ =
                    createClassificationRuleForTest context "Linkage and matching test: account claimant" "F-2230" 500
                        [ ("And", [ FieldMatch.Description pattern ], None) ]
                let! _, debitLineId, creditLineId =
                    scenario.outgoEntry "CF-12.3 account claimant payment" scenario.firstOfThisMonth "Classified"
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! debitLinks = scenario.linksOf debitLineId
                let! creditLinks = scenario.linksOf creditLineId
                Assert.Empty(debitLinks)
                Assert.Empty(creditLinks)
                Assert.Empty(run |> decisionsNaming debitLineId)
                Assert.Empty(run |> decisionsNaming creditLineId)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.4 — leg selection
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.4 with no claiming rule constraining line type, an Outgo agreement's claim links the entry's Debit line on the agreement's debit account and no other line`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.4 outgo default" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRule legId "CF-12.4 outgo default payment"
                let! _, debitLineId, creditLineId =
                    scenario.outgoEntry "CF-12.4 outgo default payment" scenario.firstOfThisMonth "Classified"
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                let link = Assert.Single(links)
                Assert.Equal(debitLineId, link |> PaymentAgreementLink.stageEntryLineId)
                let! creditLinks = scenario.linksOf creditLineId
                Assert.Empty(creditLinks)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 with no claiming rule constraining line type, an Income agreement's claim links the entry's Credit line on the agreement's credit account and no other line`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.4 income default" Income
                let! _ = scenario.invoiceWithPayments agreementId legId InvoiceSent scenario.firstOfThisMonth 30 []
                let! _ = scenario.paymentAgreementRule legId "CF-12.4 income default receipt"
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 income default receipt" scenario.firstOfThisMonth "Classified"
                        [ (100.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-4290") ]
                let debitLineId, creditLineId = lines.[0], lines.[1]
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                let link = Assert.Single(links)
                Assert.Equal(creditLineId, link |> PaymentAgreementLink.stageEntryLineId)
                let! debitLinks = scenario.linksOf debitLineId
                Assert.Empty(debitLinks)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 when a claiming rule constrains line type to Credit, an Outgo agreement's claim links the Credit line that rule matched, not the Debit line on the agreement's debit account`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.4 refund" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 refund" 500 (Some "Credit")
                (* A refund: cash comes back, the liability goes up. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 refund" scenario.firstOfThisMonth "Classified"
                        [ (100.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-2230") ]
                let debitLineId, creditLineId = lines.[0], lines.[1]
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                let link = Assert.Single(links)
                Assert.Equal(creditLineId, link |> PaymentAgreementLink.stageEntryLineId)
                let! debitLinks = scenario.linksOf debitLineId
                Assert.Empty(debitLinks)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 when one claiming rule constrains line type and another does not, the claim links the line the constraining rule matched`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.4 mixed" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                (* Equal priority, same Payment Agreement: both rules claim, neither wins over the other's claimant. *)
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 mixed" 500 (Some "Credit")
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 mixed" 500 None
                let! _, debitLineId, creditLineId = scenario.outgoEntry "CF-12.4 mixed" scenario.firstOfThisMonth "Classified"
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                let link = Assert.Single(links)
                Assert.Equal(creditLineId, link |> PaymentAgreementLink.stageEntryLineId)
                let! debitLinks = scenario.linksOf debitLineId
                Assert.Empty(debitLinks)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 with no line-type constraint, a claim on an entry with no line of the direction's line type on the agreement's account creates no link and is reported unlinked with the no-line reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.4 no line" Outgo
                let! _ = scenario.paymentAgreementRule legId "CF-12.4 no line payment"
                (* The Debit line is on F-5350, not the agreement's debit account F-2230. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 no line payment" scenario.firstOfThisMonth "Classified"
                        [ (100.00M, "Debit", "F-5350"); (100.00M, "Credit", "F-1280") ]
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Empty(links)
                let decisions = run.decisionLog |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                Assert.NotEmpty(decisions)
                Assert.All(decisions, fun d -> Assert.Equal(NoLineOnAgreementAccounts, d.outcome))
                Assert.All(decisions, fun d -> Assert.Contains(d.stageEntryLineId, lines))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 with no line-type constraint, a claim on an entry with two lines of the direction's line type on the agreement's account creates no link and is reported unlinked with the several-lines reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.4 two lines" Outgo
                let! _ = scenario.paymentAgreementRule legId "CF-12.4 two lines payment"
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 two lines payment" scenario.firstOfThisMonth "Classified"
                        [ (60.00M, "Debit", "F-2230"); (40.00M, "Debit", "F-2230"); (100.00M, "Credit", "F-1280") ]
                let debitLineIds = set [ lines.[0]; lines.[1] ]
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Empty(links)
                let decisions = run.decisionLog |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                Assert.Equal<Set<StageEntryLineId>>(debitLineIds, decisions |> List.map _.stageEntryLineId |> Set.ofList)
                Assert.All(decisions, fun d -> Assert.Equal(ManyLinesOnAgreementAccount, d.outcome))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 when a line-type-constrained rule matched two lines of the entry, the claim creates no link and is reported unlinked with the several-lines reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.4 constrained two" Outgo
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 constrained two" 500 (Some "Debit")
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 constrained two" scenario.firstOfThisMonth "Classified"
                        [ (60.00M, "Debit", "F-2230"); (40.00M, "Debit", "F-5350"); (100.00M, "Credit", "F-1280") ]
                let debitLineIds = set [ lines.[0]; lines.[1] ]
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Empty(links)
                let decisions = run.decisionLog |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                Assert.Equal<Set<StageEntryLineId>>(debitLineIds, decisions |> List.map _.stageEntryLineId |> Set.ofList)
                Assert.All(decisions, fun d -> Assert.Equal(ManyLinesOnAgreementAccount, d.outcome))
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.5 — resolution by Payment Agreement
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.5 a Payment Agreement that two lines from different entries claim links neither line, and both lines are reported as contested claimants of that Payment Agreement`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.5 contested" Outgo
                let! _ = scenario.paymentAgreementRule legId "CF-12.5 contested payment"
                let! _, firstLineId, _ = scenario.outgoEntry "CF-12.5 contested payment one" scenario.firstOfThisMonth "Classified"
                let! _, secondLineId, _ = scenario.outgoEntry "CF-12.5 contested payment two" scenario.firstOfThisMonth "Classified"
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Empty(links)
                let decisions =
                    run.decisionLog
                    |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                    |> List.map (fun d -> d.stageEntryLineId, d.outcome)
                    |> Set.ofList
                let expected = set [ firstLineId, ContestedAgreement; secondLineId, ContestedAgreement ]
                Assert.Equal<Set<StageEntryLineId * PaymentAgreementDecisionOutcome>>(expected, decisions)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.5 a line whose claim is a tie between two equal-priority rules for different Payment Agreements is linked to neither, and it is reported as a contested claimant of both`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legXId = scenario.agreement "CF-12.5 tie X" Outgo
                let! _, legYId = scenario.agreement "CF-12.5 tie Y" Outgo
                let! _ = scenario.paymentAgreementRuleWith legXId "CF-12.5 tie payment" 300 None
                let! _ = scenario.paymentAgreementRuleWith legYId "CF-12.5 tie payment" 300 None
                let! _, lineId, _ = scenario.outgoEntry "CF-12.5 tie payment" scenario.firstOfThisMonth "Classified"
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                Assert.Empty(links)
                let decisions =
                    run
                    |> decisionsNaming lineId
                    |> List.map (fun d -> d.paymentAgreementId, d.outcome)
                    |> Set.ofList
                let expected = set [ Some legXId, TiedClaimants; Some legYId, TiedClaimants ]
                Assert.Equal<Set<PaymentAgreementId option * PaymentAgreementDecisionOutcome>>(expected, decisions)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.6 — linkage leaves staging statuses alone
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.6 a run leaves every staged entry's status unchanged, whether its lines were linked, reported unlinked, contested, or unclaimed`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.6 linked" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _, noLineLegId = scenario.agreement "CF-12.6 no line" Outgo
                let! _, contestedLegId = scenario.agreement "CF-12.6 contested" Outgo
                let! _ = scenario.paymentAgreementRule legId "CF-12.6 linked payment"
                let! _ = scenario.paymentAgreementRule noLineLegId "CF-12.6 no line payment"
                let! _ = scenario.paymentAgreementRule contestedLegId "CF-12.6 contested payment"
                let statuses = [ "Ingested"; "Classified"; "NoMatch"; "Conflict"; "Reviewed" ]
                (* An agreement claimed by two lines links neither, so only one entry can be the linked case; it is
                   Classified. Every status appears among the no-line, contested and unclaimed cases. *)
                let! linkedHeader, _, _ = scenario.outgoEntry "CF-12.6 linked payment" scenario.firstOfThisMonth "Classified"
                let! noLineHeaders =
                    statuses
                    |> List.map (fun status ->
                        scenario.stagedEntryWithLines $"CF-12.6 no line payment {status}" scenario.firstOfThisMonth status
                            [ (100.00M, "Debit", "F-5350"); (100.00M, "Credit", "F-1280") ]
                        |> Result.map (fun (headerId, _) -> headerId, status))
                    |> convertListOfResultsToResultsList
                let! contestedHeaders =
                    statuses
                    |> List.map (fun status ->
                        scenario.outgoEntry $"CF-12.6 contested payment {status}" scenario.firstOfThisMonth status
                        |> Result.map (fun (headerId, _, _) -> headerId, status))
                    |> convertListOfResultsToResultsList
                let! unclaimedHeaders =
                    statuses
                    |> List.map (fun status ->
                        scenario.outgoEntry $"CF-12.6 unclaimed {status}" scenario.firstOfThisMonth status
                        |> Result.map (fun (headerId, _, _) -> headerId, status))
                    |> convertListOfResultsToResultsList
                let expected = (linkedHeader, "Classified") :: noLineHeaders @ contestedHeaders @ unclaimedHeaders
                let! run = CashFlowOps.classifyPaymentAgreements context
                (* The run did what the cases claim: one link, unlinked no-line claims, contested claims. *)
                let! links = legId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Single(links) |> ignore
                Assert.Contains(run.decisionLog, fun d -> d.outcome = NoLineOnAgreementAccounts)
                Assert.Contains(run.decisionLog, fun d -> d.outcome = ContestedAgreement)
                let! actual =
                    expected
                    |> List.map (fun (headerId, status) ->
                        scenario.statusOf headerId |> Result.map (fun current -> status, current))
                    |> convertListOfResultsToResultsList
                actual
                |> List.iter (fun (status, current) ->
                    let expectedStatus = status |> StagedEntryStatus.fromString |> Result.toOption
                    Assert.Equal(expectedStatus, current))
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.7 — operator link maintenance
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.7 creating a link for an unlinked staged line leaves the line with exactly one link, to the named Payment Agreement`` () =
        runCommandRouteAndAutoRollback CreatePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.7 create" Outgo
                let! _, lineId, _ = scenario.outgoEntry "CF-12.7 create payment" scenario.firstOfThisMonth "Classified"
                let! before = scenario.linksOf lineId
                Assert.Empty(before)
                let! _ = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legId lineId
                let! after = scenario.linksOf lineId
                let link = Assert.Single(after)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.7 creating a link for a line that is already linked is rejected with a typed error naming the existing link's Payment Agreement, and the existing link is unchanged`` () =
        runCommandRouteAndAutoRollback CreatePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legXId = scenario.agreement "CF-12.7 already X" Outgo
                let! _, legYId = scenario.agreement "CF-12.7 already Y" Outgo
                let! _, lineId, _ = scenario.outgoEntry "CF-12.7 already payment" scenario.firstOfThisMonth "Classified"
                let! existing = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legXId lineId
                let! _ =
                    match CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legYId lineId with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementLinkLineAlreadyLinked (namedLine, namedAgreement))) ->
                        Assert.Equal(lineId |> StageEntryLineId.value, namedLine)
                        Assert.Equal(legXId |> PaymentAgreementId.value, namedAgreement)
                        Ok ()
                    | Error e -> TestError.error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> TestError.error (TestingError "Expected failure; got success")
                let! after = scenario.linksOf lineId
                let link = Assert.Single(after)
                Assert.Equal(existing |> PaymentAgreementLink.paymentAgreementLinkId, link |> PaymentAgreementLink.paymentAgreementLinkId)
                Assert.Equal(legXId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.7 re-pointing a link to a different Payment Agreement leaves the line with exactly one link, to the new Payment Agreement`` () =
        runCommandRouteAndAutoRollback UpdatePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legXId = scenario.agreement "CF-12.7 repoint X" Outgo
                let! _, legYId = scenario.agreement "CF-12.7 repoint Y" Outgo
                let! _, lineId, _ = scenario.outgoEntry "CF-12.7 repoint payment" scenario.firstOfThisMonth "Classified"
                let! existing = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legXId lineId
                let! _ =
                    PaymentAgreementLink.update context
                        { linkIdToUpdate = existing |> PaymentAgreementLink.paymentAgreementLinkId
                          paymentAgreementIdUpdate = FieldUpdate.SetTo legYId }
                let! after = scenario.linksOf lineId
                let link = Assert.Single(after)
                Assert.Equal(legYId, link |> PaymentAgreementLink.paymentAgreementId)
                let! linksToX = legXId |> PaymentAgreementLink.fetchByPaymentAgreementId context
                Assert.Empty(linksToX)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.7 deleting a link leaves the line with no link, and the next run links it again when a rule claims it`` () =
        runCommandRouteAndAutoRollback DeletePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.7 delete" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legId "CF-12.7 delete payment" scenario.firstOfThisMonth
                let! existing = scenario.linksOf lineId
                let link = Assert.Single(existing)
                do! link |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLink.delete context
                let! afterDelete = scenario.linksOf lineId
                Assert.Empty(afterDelete)
                let! _ = scenario.paymentAgreementRule legId "CF-12.7 delete payment"
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! afterRun = scenario.linksOf lineId
                let relinked = Assert.Single(afterRun)
                Assert.Equal(legId, relinked |> PaymentAgreementLink.paymentAgreementId)
                Assert.NotEqual(link |> PaymentAgreementLink.paymentAgreementLinkId, relinked |> PaymentAgreementLink.paymentAgreementLinkId)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.8 — the run's matches are recorded
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.8 fetching a classification run by its ID returns, for every line the run matched, every rule that matched it with that rule's priority, and no match from any other run`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-12.8 X" Outgo
                let! agreementYId, legYId = scenario.agreement "CF-12.8 Y" Outgo
                (* Each agreement has an open Invoice covering this month, so a line either run links has somewhere to
                   go; otherwise the run fails on it as an orphan (REQ-CF-13.7) before its matches can be fetched. *)
                let! _ = scenario.outgoInvoice agreementXId legXId scenario.firstOfThisMonth 30
                let! _ = scenario.outgoInvoice agreementYId legYId scenario.firstOfThisMonth 30
                (* An earlier run matches a line of its own, so its match is in the table when the later run is fetched. *)
                let! earlierRule = scenario.paymentAgreementRuleWith legYId "CF-12.8 earlier" 250 None
                let! _, earlierLineId, _ = scenario.outgoEntry "CF-12.8 earlier" scenario.firstOfThisMonth "Classified"
                let! earlierRun = CashFlowOps.classifyPaymentAgreements context
                (* Retire the earlier rule, so the later run does not match the earlier entry's unlinked Credit line
                   again: every row the earlier run recorded stays in the table and must stay out of the later run. *)
                let! earlierRows = earlierRun.runId |> ClassificationOrchestration.fetchRunMatchesWithRules context
                Assert.NotEmpty(earlierRows)
                let! _ = earlierRule |> scenario.setRuleActive false
                (* The later line is matched by two rules for X at different priorities; X is claimed by the winner. *)
                let! winner = scenario.paymentAgreementRuleWith legXId "CF-12.8 later" 150 None
                let! loser = scenario.paymentAgreementRuleWith legXId "CF-12.8 later" 350 None
                let! _, laterDebitId, laterCreditId = scenario.outgoEntry "CF-12.8 later" scenario.firstOfThisMonth "Classified"
                let! laterRun = CashFlowOps.classifyPaymentAgreements context
                let! fetched = laterRun.runId |> ClassificationOrchestration.fetchRunMatchesWithRules context
                let fetchedMatches =
                    fetched
                    |> List.map (fun (ruleMatch, rule) ->
                        (ruleMatch |> RuleMatch.stageEntryLineId),
                        (rule |> ClassificationRule.classificationRuleId),
                        (rule |> ClassificationRule.priority))
                    |> Set.ofList
                let idOf = ClassificationRule.classificationRuleId
                let expected =
                    set [ for lineId in [ laterDebitId; laterCreditId ] do
                            yield lineId, idOf winner, 150
                            yield lineId, idOf loser, 350 ]
                Assert.Equal<Set<StageEntryLineId * ClassificationRuleId * int>>(expected, fetchedMatches)
                Assert.All(fetched, fun (ruleMatch, _) -> Assert.Equal(laterRun.runId, ruleMatch |> RuleMatch.runId))
                Assert.DoesNotContain(fetched, fun (ruleMatch, _) -> ruleMatch |> RuleMatch.stageEntryLineId = earlierLineId)
                Assert.NotEqual(earlierRun.runId, laterRun.runId)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.1 — candidate Invoices and their order
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.1 REQ-CF-13.4 a line that two open Invoices' windows both cover is paid to the Invoice with the older due date, whichever Invoice was created first, and the other Invoice gets no Payment from it`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.1 order" Outgo
                (* Created first: last month's Invoice, due 60 days on, so its due date is the later one. *)
                let! _, laterDueInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfLastMonth 60
                (* Created second: this month's, due on its date, so its due date is the earlier one. *)
                let! _, earlierDueInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 0
                let! lineId = scenario.linkedLine legId "CF-13.1 order payment" scenario.firstOfThisMonth
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(earlierDueInvoiceId, payment |> Payment.invoiceId)
                let! onLaterDue = scenario.paymentsOnInvoice laterDueInvoiceId
                Assert.Empty(onLaterDue)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.1 an older Invoice whose payment state is FullyPaid is passed over, and a linked line both Invoices' windows cover is paid to the newer Invoice`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.1 fully paid" Outgo
                (* Last month's Invoice is paid in full by a Staged Payment on a line of its own. Its Instance is
                   therefore fulfilled too, which is the next test's condition; this one is about the Invoice. *)
                let! _, paidLineId, _ = scenario.outgoEntry "CF-13.1 fully paid earlier" scenario.firstOfLastMonth "Classified"
                let! _, paidInvoiceId =
                    scenario.invoiceWithPayments agreementId legId InvoiceReceived scenario.firstOfLastMonth 30
                        [ (TransactionPointer.Staged paidLineId, 100.00M) ]
                let! paidComposite = paidInvoiceId |> InstanceOrchestration.fetchCompositeByInvoiceId context
                let paidState = paidComposite |> InstanceOrchestration.invoice |> Invoice.invoiceLifeCycleState
                Assert.Equal(FullyPaid, paidState.paymentState)
                let! _, newerInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legId "CF-13.1 fully paid payment" scenario.firstOfThisMonth
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(newerInvoiceId, payment |> Payment.invoiceId)
                let! onPaid = scenario.paymentsOnInvoice paidInvoiceId
                Assert.Equal(1, onPaid |> List.length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.1 an older Invoice on a fulfilled Instance is passed over, and a linked line both Invoices' windows cover is paid to the newer Invoice`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.1 fulfilled" Outgo
                let! _, paidLineId, _ = scenario.outgoEntry "CF-13.1 fulfilled earlier" scenario.firstOfLastMonth "Classified"
                let! fulfilledInstanceId, olderInvoiceId =
                    scenario.invoiceWithPayments agreementId legId InvoiceReceived scenario.firstOfLastMonth 30
                        [ (TransactionPointer.Staged paidLineId, 100.00M) ]
                let! fulfilled = fulfilledInstanceId |> InstanceOrchestration.fetchCompositeByInstanceId context
                Assert.True(fulfilled |> InstanceOrchestration.instance |> Instance.isFulfilled)
                let! _, newerInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legId "CF-13.1 fulfilled payment" scenario.firstOfThisMonth
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(newerInvoiceId, payment |> Payment.invoiceId)
                Assert.NotEqual(olderInvoiceId, payment |> Payment.invoiceId)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.2 — candidate lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.2 a linked line whose Staged Payment already pays one Invoice gets no Payment from another open Invoice whose window covers its date, and the run does not fail`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.2 staged paid" Outgo
                let! lineId = scenario.linkedLine legId "CF-13.2 staged paid payment" scenario.firstOfThisMonth
                (* Last month's Invoice, due 60 days on, is paid by a Staged Payment on the line; this month's is open
                   and its window covers the line's date too. *)
                let! _, paidInvoiceId =
                    scenario.invoiceWithPayments agreementId legId InvoiceReceived scenario.firstOfLastMonth 60
                        [ (TransactionPointer.Staged lineId, 100.00M) ]
                let! _, openInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(paidInvoiceId, payment |> Payment.invoiceId)
                let! onOpen = scenario.paymentsOnInvoice openInvoiceId
                Assert.Empty(onOpen)
                Assert.Empty(run |> invoiceDecisionsFor openInvoiceId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.2 a linked line gets no Payment from an open Invoice on a different Payment Agreement whose window covers its date`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-13.2 own X" Outgo
                let! agreementYId, legYId = scenario.agreement "CF-13.2 other Y" Outgo
                (* Y's Invoice has the older due date, so it would be first to take the line if the agreement did not
                   decide. *)
                let! _, otherInvoiceId = scenario.outgoInvoice agreementYId legYId scenario.firstOfThisMonth 0
                let! _, ownInvoiceId = scenario.outgoInvoice agreementXId legXId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legXId "CF-13.2 own X payment" scenario.firstOfThisMonth
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(ownInvoiceId, payment |> Payment.invoiceId)
                let! onOther = scenario.paymentsOnInvoice otherInvoiceId
                Assert.Empty(onOther)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.3 — grace periods by cadence
    // =========================================================================

    (* Each case builds an agreement on the named cadence whose Instance falls on the 1st of this month, with one
       Invoice dated that day and due 10 days later. The grace period stretches the window to
       [invoice date - grace, due date + grace]. *)

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated exactly its grace period before the Invoice date is paid to that Invoice`` (cadence: string, graceDays: int) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let invoiceDate = scenario.firstOfThisMonth
                let cadenceType, nextInstance = cadenceFor cadence invoiceDate
                let! agreementId, legId = scenario.agreementWithCadence $"CF-13.3 {cadence} early edge" Outgo cadenceType nextInstance
                let! _, invoiceId = scenario.outgoInvoice agreementId legId invoiceDate 10
                let! lineId = scenario.linkedLine legId $"CF-13.3 {cadence} early edge payment" (invoiceDate.PlusDays(-graceDays))
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(invoiceId, payment |> Payment.invoiceId)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated exactly its grace period after the due date is paid to that Invoice`` (cadence: string, graceDays: int) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let invoiceDate = scenario.firstOfThisMonth
                let cadenceType, nextInstance = cadenceFor cadence invoiceDate
                let! agreementId, legId = scenario.agreementWithCadence $"CF-13.3 {cadence} late edge" Outgo cadenceType nextInstance
                let! _, invoiceId = scenario.outgoInvoice agreementId legId invoiceDate 10
                let! lineId = scenario.linkedLine legId $"CF-13.3 {cadence} late edge payment" (invoiceDate.PlusDays(10 + graceDays))
                let! _ = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(invoiceId, payment |> Payment.invoiceId)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated one day more than its grace period before the Invoice date fails the run as an orphan of that agreement with the no-covering-Invoice reason`` (cadence: string, graceDays: int) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let invoiceDate = scenario.firstOfThisMonth
                let cadenceType, nextInstance = cadenceFor cadence invoiceDate
                let! agreementId, legId = scenario.agreementWithCadence $"CF-13.3 {cadence} too early" Outgo cadenceType nextInstance
                let! _ = scenario.outgoInvoice agreementId legId invoiceDate 10
                let! lineId = scenario.linkedLine legId $"CF-13.3 {cadence} too early payment" (invoiceDate.PlusDays(-graceDays - 1))
                let! orphans = CashFlowOps.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineId |> StageEntryLineId.value, orphanLine)
                Assert.Equal(legId |> PaymentAgreementId.value, orphanLeg)
                Assert.Equal(CashFlowError.NoOpenInvoiceCoversDate, reason)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated one day more than its grace period after the due date fails the run as an orphan of that agreement with the no-covering-Invoice reason`` (cadence: string, graceDays: int) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let invoiceDate = scenario.firstOfThisMonth
                let cadenceType, nextInstance = cadenceFor cadence invoiceDate
                let! agreementId, legId = scenario.agreementWithCadence $"CF-13.3 {cadence} too late" Outgo cadenceType nextInstance
                let! _ = scenario.outgoInvoice agreementId legId invoiceDate 10
                let! lineId = scenario.linkedLine legId $"CF-13.3 {cadence} too late payment" (invoiceDate.PlusDays(10 + graceDays + 1))
                let! orphans = CashFlowOps.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineId |> StageEntryLineId.value, orphanLine)
                Assert.Equal(legId |> PaymentAgreementId.value, orphanLeg)
                Assert.Equal(CashFlowError.NoOpenInvoiceCoversDate, reason)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.4 through 13.6 — what matching does with its candidates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.4 an Invoice with exactly one candidate line gets exactly one Payment, Staged, pointing at that line`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.4 single" Outgo
                let! _, invoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legId "CF-13.4 single payment" scenario.firstOfThisMonth
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOnInvoice invoiceId
                let payment = Assert.Single(payments)
                Assert.Equal(TransactionPointer.Staged lineId, payment |> Payment.transactionPointer)
                let decision = Assert.Single(run |> invoiceDecisionsFor invoiceId)
                Assert.Equal(PaymentCreated lineId, decision.outcome)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.4 REQ-CF-13.5 a line that lost a contested Invoice is paid to a later Invoice for which it is the only candidate`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.4 lost then won" Outgo
                (* Monthly grace is 7 days. Last month's Invoice, due 30 days on, covers last month's 1st through a
                   week past its due date, which takes in this month's 1st. This month's covers this month's 1st
                   onward, and not last month's 1st. Last month's has the older due date, so it is decided first. *)
                let! _, contestedInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfLastMonth 30
                let! _, laterInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                (* The early line: only the contested Invoice's window covers it. *)
                let! earlyLineId = scenario.linkedLine legId "CF-13.4 lost then won early" scenario.firstOfLastMonth
                (* The shared line: both windows cover it. *)
                let! sharedLineId = scenario.linkedLine legId "CF-13.4 lost then won shared" scenario.firstOfThisMonth
                let! run = CashFlowOps.classifyPaymentAgreements context
                let contested = Assert.Single(run |> invoiceDecisionsFor contestedInvoiceId)
                let! candidates = contested |> manyCandidatesOf
                Assert.Equal<Set<StageEntryLineId>>(set [ earlyLineId; sharedLineId ], candidates)
                let! onContested = scenario.paymentsOnInvoice contestedInvoiceId
                Assert.Empty(onContested)
                let! payments = scenario.paymentsOn sharedLineId
                let payment = Assert.Single(payments)
                Assert.Equal(laterInvoiceId, payment |> Payment.invoiceId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.5 an Invoice with more than one candidate line gets no Payment and is reported with every candidate line`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.5 many" Outgo
                let! _, invoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! firstLineId = scenario.linkedLine legId "CF-13.5 many one" scenario.firstOfThisMonth
                let! secondLineId = scenario.linkedLine legId "CF-13.5 many two" (scenario.firstOfThisMonth.PlusDays(1))
                let! thirdLineId = scenario.linkedLine legId "CF-13.5 many three" (scenario.firstOfThisMonth.PlusDays(2))
                let! run = CashFlowOps.classifyPaymentAgreements context
                let! payments = scenario.paymentsOnInvoice invoiceId
                Assert.Empty(payments)
                let decision = Assert.Single(run |> invoiceDecisionsFor invoiceId)
                let! candidates = decision |> manyCandidatesOf
                Assert.Equal<Set<StageEntryLineId>>(set [ firstLineId; secondLineId; thirdLineId ], candidates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.6 a Payment that takes an Invoice's paid total above its amount is reported as an overpayment of that Invoice, and the run creates no journal entry and no record other than its links and Payments`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.6 overpaid" Outgo
                let! _, invoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                (* A 150.00 payment against the 100.00 Invoice. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-13.6 overpaid payment" scenario.firstOfThisMonth "Classified"
                        [ (150.00M, "Debit", "F-2230"); (150.00M, "Credit", "F-1280") ]
                let lineId = lines.[0]
                let! _ = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legId lineId
                let! journalEntriesBefore = scenario.journalEntryCount ()
                let! instancesBefore, invoicesBefore = scenario.instanceAndInvoiceIds ()
                let! run = CashFlowOps.classifyPaymentAgreements context
                let outcomes = run |> invoiceDecisionsFor invoiceId |> List.map _.outcome
                Assert.Equal<InvoiceDecisionOutcome list>([ PaymentCreated lineId; Overpayment ], outcomes)
                let! journalEntriesAfter = scenario.journalEntryCount ()
                Assert.Equal(journalEntriesBefore, journalEntriesAfter)
                let! instancesAfter, invoicesAfter = scenario.instanceAndInvoiceIds ()
                Assert.Equal<Set<InstanceId>>(instancesBefore, instancesAfter)
                Assert.Equal<Set<InvoiceId>>(invoicesBefore, invoicesAfter)
                let! payments = scenario.paymentsOnInvoice invoiceId
                let payment = Assert.Single(payments)
                Assert.Equal(TransactionPointer.Staged lineId, payment |> Payment.transactionPointer)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.8 — a lost candidate is not an orphan
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.8 a line that was one of several candidates for an Invoice and got no Payment does not fail the run as an orphan`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.8 lost" Outgo
                let! _, invoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! firstLineId = scenario.linkedLine legId "CF-13.8 lost one" scenario.firstOfThisMonth
                let! secondLineId = scenario.linkedLine legId "CF-13.8 lost two" scenario.firstOfThisMonth
                (* An orphan fails the whole run, so a run that returns at all has no orphan. *)
                let! run = CashFlowOps.classifyPaymentAgreements context
                let decision = Assert.Single(run |> invoiceDecisionsFor invoiceId)
                let! candidates = decision |> manyCandidatesOf
                Assert.Equal<Set<StageEntryLineId>>(set [ firstLineId; secondLineId ], candidates)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.9 — the run's result
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.9 a line the run links is paid in that same run to the open Invoice whose window covers its date`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.9 same run" Outgo
                let! _, invoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRule legId "CF-13.9 same run payment"
                let! _, lineId, _ = scenario.outgoEntry "CF-13.9 same run payment" scenario.firstOfThisMonth "Classified"
                let! linksBefore = scenario.linksOf lineId
                Assert.Empty(linksBefore)
                let! run = CashFlowOps.classifyPaymentAgreements context
                Assert.Contains(run.decisionLog, fun d -> d.stageEntryLineId = lineId && d.outcome = Linked)
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(invoiceId, payment |> Payment.invoiceId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.9 the classification run ID in the result fetches a run whose recorded matches include every line the result lists as linked or as an unlinked claim`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.9 run id linked" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _, contestedLegId = scenario.agreement "CF-13.9 run id contested" Outgo
                let! _ = scenario.paymentAgreementRule legId "CF-13.9 run id linked payment"
                let! _ = scenario.paymentAgreementRule contestedLegId "CF-13.9 run id contested payment"
                let! _ = scenario.outgoEntry "CF-13.9 run id linked payment" scenario.firstOfThisMonth "Classified"
                let! _ = scenario.outgoEntry "CF-13.9 run id contested payment one" scenario.firstOfThisMonth "Classified"
                let! _ = scenario.outgoEntry "CF-13.9 run id contested payment two" scenario.firstOfThisMonth "Classified"
                let! run = CashFlowOps.classifyPaymentAgreements context
                let listedLines = run.decisionLog |> List.map _.stageEntryLineId |> Set.ofList
                Assert.Contains(run.decisionLog, fun d -> d.outcome = Linked)
                Assert.Contains(run.decisionLog, fun d -> d.outcome = ContestedAgreement)
                let! fetched = run.runId |> ClassificationOrchestration.fetchRunMatchesWithRules context
                let recordedLines = fetched |> List.map (fst >> RuleMatch.stageEntryLineId) |> Set.ofList
                Assert.NotEmpty(listedLines)
                Assert.Empty(Set.difference listedLines recordedLines)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.9 the result lists every link and every Payment the run created, and no link or Payment that existed before the run`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.9 created" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfLastMonth 0
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 0
                (* Linked before the run: paid by the run, but its link is not the run's. *)
                let! preLinkedLineId = scenario.linkedLine legId "CF-13.9 created pre-linked" scenario.firstOfLastMonth
                (* Linked by the run and paid by it. *)
                let! _ = scenario.paymentAgreementRule legId "CF-13.9 created by run"
                let! _, runLinkedLineId, _ = scenario.outgoEntry "CF-13.9 created by run" scenario.firstOfThisMonth "Classified"
                (* Linked and paid before the run. *)
                let! oldAgreementId, legOldId = scenario.agreement "CF-13.9 created old" Outgo
                let! oldLineId = scenario.linkedLine legOldId "CF-13.9 created old payment" scenario.firstOfLastMonth
                let! _ =
                    scenario.invoiceWithPayments oldAgreementId legOldId InvoiceReceived scenario.firstOfLastMonth 0
                        [ (TransactionPointer.Staged oldLineId, 100.00M) ]
                let! run = CashFlowOps.classifyPaymentAgreements context
                let linkedInRun =
                    run.decisionLog |> List.filter (fun d -> d.outcome = Linked) |> List.map _.stageEntryLineId |> Set.ofList
                let paidInRun =
                    run.invoiceDecisionLog
                    |> List.choose (fun d -> match d.outcome with PaymentCreated lineId -> Some lineId | _ -> None)
                    |> Set.ofList
                Assert.Contains(runLinkedLineId, linkedInRun)
                Assert.DoesNotContain(preLinkedLineId, linkedInRun)
                Assert.DoesNotContain(oldLineId, linkedInRun)
                Assert.Contains(runLinkedLineId, paidInRun)
                Assert.Contains(preLinkedLineId, paidInRun)
                Assert.DoesNotContain(oldLineId, paidInRun)
                (* Every Payment the result lists exists now, on the line it names. *)
                let! listedPayments = paidInRun |> Set.toList |> Payment.fetchByStageEntryLineIdList context
                Assert.Equal<Set<StageEntryLineId>>(
                    paidInRun,
                    listedPayments
                    |> List.choose (fun p -> match p |> Payment.transactionPointer with Staged l -> Some l | _ -> None)
                    |> Set.ofList)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.9 the result's open Instances are exactly the unfulfilled Instances after matching, so an Instance the run fulfilled is absent and one it left unfulfilled is present`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.9 open" Outgo
                let! fulfilledByRunId, _ = scenario.outgoInvoice agreementId legId scenario.firstOfLastMonth 0
                let! leftOpenId, _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 0
                let! _ = scenario.linkedLine legId "CF-13.9 open payment" scenario.firstOfLastMonth
                let! run = CashFlowOps.classifyPaymentAgreements context
                let listed = run.openInstances |> List.map (InstanceOrchestration.instance >> Instance.instanceId) |> Set.ofList
                Assert.DoesNotContain(fulfilledByRunId, listed)
                Assert.Contains(leftOpenId, listed)
                let! unfulfilledNow = false |> InstanceOrchestration.fetchCompositesByIsFulfilled context
                let expected = unfulfilledNow |> List.map (InstanceOrchestration.instance >> Instance.instanceId) |> Set.ofList
                Assert.Equal<Set<InstanceId>>(expected, listed)
            })
        |> railroadWrapper
