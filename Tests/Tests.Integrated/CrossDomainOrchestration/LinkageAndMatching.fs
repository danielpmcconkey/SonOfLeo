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


/// every link to any of the given Payment Agreements
let private linksTo context (agreementIds: PaymentAgreementId list) =
    PaymentAgreementLink.fetchAll context
    |> Result.map (List.filter (fun link -> agreementIds |> List.contains (link |> PaymentAgreementLink.paymentAgreementId)))

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
                    [ (legName, (DebitAccount.create debit), (CreditAccount.create credit), Some expected, Some due, None) ]
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

    /// An agreement with a 100.00 leg of each name, active since three months ago, monthly on the 1st. Returns the
    /// agreement's id and its legs' ids in the order given.
    member this.agreementWithLegs (name: string) (direction: FlowDirection) (legNames: string list) =
        result {
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Linkage and matching test counterparty" |> Counterparty.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! activityPeriod =
                ActivityPeriod.create (this.firstOfThisMonth.PlusMonths(-3)) None
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! expected = Money.fromDecimal 100.00M
            let! due = 0 |> DaysDueAfterInvoiceDate.create
            let debit, credit =
                match direction with
                | Outgo -> this.loanId, this.cashId
                | Income -> this.cashId, this.revenueId
            let! legs =
                legNames
                |> List.map (fun legName ->
                    legName
                    |> PaymentAgreementName.create
                    |> Result.map (fun paName ->
                        (paName, (DebitAccount.create debit), (CreditAccount.create credit), Some expected, Some due, None)))
                |> convertListOfResultsToResultsList
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName direction (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = this.firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None legs
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legIds =
                legNames
                |> List.map (fun legName ->
                    agreement
                    |> AgreementOrchestration.paymentAgreements
                    |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                    |> PaymentAgreement.paymentAgreementId)
            return agreementId, legIds
        }

    /// A 100.00 Invoice on its own Instance dated invoiceDate, due daysDue later, with the blocker and carrying
    /// Payments at the given pointers (each Payment's amount is its line's). Returns the Instance's id and the
    /// Invoice's id.
    member _.invoiceWithBlockerAndPayments agreementId legId (state: InvoiceState) (invoiceDate: LocalDate) (daysDue: int)
            (blocker: Blocker option) (payments: TransactionPointer list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let newPayments = payments |> List.map (fun pointer -> (pointer, None, None, None))
            let! created =
                InstanceOrchestration.constructNewAndPersist
                    context agreementId invoiceDate
                    [ (legId, None, InvoiceDate.create(invoiceDate), DueDate.create(invoiceDate.PlusDays(daysDue)),
                       InvoiceAmount.create(amount), state, blocker, None, newPayments) ]
            let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
            let invoiceId =
                created |> InstanceOrchestration.invoiceComposites |> List.head
                |> InstanceOrchestration.invoice |> Invoice.invoiceId
            return instanceId, invoiceId
        }

    /// A 100.00 Invoice on its own Instance dated invoiceDate, due daysDue later, carrying Payments at the given
    /// pointers. Returns the Instance's id and the Invoice's id.
    member this.invoiceWithPayments agreementId legId (state: InvoiceState) (invoiceDate: LocalDate) (daysDue: int)
            (payments: TransactionPointer list) =
        this.invoiceWithBlockerAndPayments agreementId legId state invoiceDate daysDue None payments

    /// An Instance dated instanceDate with no Invoice. Returns its id.
    member _.emptyInstance agreementId (instanceDate: LocalDate) =
        InstanceOrchestration.constructNewAndPersist context agreementId instanceDate []
        |> Result.map (InstanceOrchestration.instance >> Instance.instanceId)

    /// Adds a 100.00 Outgo Invoice dated invoiceDate and due on dueDate to the Instance, entered at this scenario's
    /// instant. Returns the Invoice's id.
    member _.addOutgoInvoice instanceId legId (invoiceDate: LocalDate) (dueDate: LocalDate) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let update: InstanceOrchestration.InstanceCompositeUpdate =
                { instanceUpdates = { instanceIdToUpdate = instanceId; isFulfilledUpdate = FieldUpdate.NoChange }
                  invoiceCompositeUpdates = []
                  newInvoices =
                    [ (legId, None, InvoiceDate.create invoiceDate, DueDate.create dueDate, InvoiceAmount.create amount,
                       InvoiceReceived, None, None, []) ] }
            let! composite = update |> InstanceOrchestration.updateInstanceComposite context
            return
                composite
                |> InstanceOrchestration.invoiceComposites
                |> List.map InstanceOrchestration.invoice
                |> List.find (fun invoice -> invoice |> Invoice.paymentAgreementId = legId)
                |> Invoice.invoiceId
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
            let! _ = CashFlowOps.constructNewAndPersist context legId debitLineId
            return debitLineId
        }

    /// An active rule claiming, for the leg, any line meeting every one of the field matches.
    member _.paymentAgreementRuleMatching legId (label: string) (priority: int) (fieldMatches: FieldMatch.FieldMatch list) =
        result {
            let! name = $"Linkage and matching test: {label} {priority} {Guid.NewGuid()}" |> ClassificationRuleName.create
            let! groups = [ ("And", fieldMatches, None) ] |> createClassificationRuleGroupListForTest
            return!
                ClassificationOrchestration.constructNewAndPersist
                    context name (ClassificationClaimant.PaymentAgreement legId) priority groups
        }

    /// An active rule claiming, for the leg, any entry whose description matches, optionally pinned to a line type.
    member this.paymentAgreementRuleWith legId (description: string) (priority: int) (lineType: string option) =
        result {
            let! pattern = description |> StringSearchPattern.create
            let! lineTypeMatch =
                match lineType with
                | None -> Ok []
                | Some lt -> lt |> JournalEntryLineType.fromString |> Result.map (fun t -> [ FieldMatch.LineType t ])
            return!
                this.paymentAgreementRuleMatching legId $"{description} {lineType}" priority
                    ((FieldMatch.Description pattern) :: lineTypeMatch)
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
            let! masterAgreements = MasterAgreement.fetchAll context
            let! instances =
                if masterAgreements |> List.isEmpty then Ok []
                else masterAgreements |> List.map MasterAgreement.agreementID |> Instance.fetchByMasterAgreementIdList context
            let instanceIds = instances |> List.map Instance.instanceId |> Set.ofList
            let! invoices =
                if instances |> List.isEmpty then Ok []
                else instances |> List.map Instance.instanceId |> Invoice.fetchByInstanceIdList context
            let invoiceIds = invoices |> List.map Invoice.invoiceId |> Set.ofList
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
                let! created = CashFlowOps.constructNewAndPersist context legId lineId
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                (* Y's rule matches only the Debit line, the one already linked to X. An unconstrained rule would also
                   match the unlinked Credit line, and as the only line it matched, that line would be linked to Y. *)
                let! _ = scenario.paymentAgreementRuleWith legYId "CF-12.2 existing payment" 500 (Some "Debit")
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = scenario.linksOf lineId
                let link = Assert.Single(links)
                Assert.Equal(legXId, link |> PaymentAgreementLink.paymentAgreementId)
                let! linksToY = linksTo context [ legYId ]
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! firstRun = ClassificationOrchestration.classifyPaymentAgreements context
                let! linksAfterFirst = scenario.linksOf lineId
                Assert.Empty(linksAfterFirst)
                Assert.Empty(firstRun |> decisionsNaming lineId)
                let! _ = rule |> scenario.setRuleActive true
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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

    (* The kept lines are the lines a claiming rule matched. Only when several are kept does the expected-account
       default choose: a Credit line on the credit account for Income, a Debit line on the debit account for Outgo. *)

    [<Fact>]
    member _.``REQ-CF-12.4 for each of Income and Outgo, a claiming rule with only description and source criteria matches every line of the entry alike, and the expected-account default links the one line on the expected account with the expected line type`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! source = "TestBank" |> StringSearchPattern.create
                let! cases =
                    [ Income, "CF-12.4 alike income", [ (100.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-4290") ], 1
                      Outgo, "CF-12.4 alike outgo", [ (100.00M, "Debit", "F-2230"); (100.00M, "Credit", "F-1280") ], 0 ]
                    |> List.map (fun (direction, label, lines, expectedIndex) ->
                        result {
                            let! agreementId, legId = scenario.agreement label direction
                            let state = match direction with | Income -> InvoiceSent | Outgo -> InvoiceReceived
                            let! _ = scenario.invoiceWithPayments agreementId legId state scenario.firstOfThisMonth 30 []
                            let! description = label |> StringSearchPattern.create
                            let! rule =
                                scenario.paymentAgreementRuleMatching legId label 500
                                    [ FieldMatch.Description description; FieldMatch.Source source ]
                            let! _, lineIds = scenario.stagedEntryWithLines label scenario.firstOfThisMonth "Classified" lines
                            return legId, rule, lineIds, lineIds.[expectedIndex]
                        })
                    |> convertListOfResultsToResultsList
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! recorded = run.runId |> ClassificationOrchestration.fetchRunMatchesWithRules context
                let! checks =
                    cases
                    |> List.map (fun (legId, rule, lineIds, expectedLineId) ->
                        result {
                            (* The rule matched both lines of its entry. *)
                            let matchedByRule =
                                recorded
                                |> List.filter (fun (_, r) -> r |> ClassificationRule.classificationRuleId = (rule |> ClassificationRule.classificationRuleId))
                                |> List.map (fst >> RuleMatch.stageEntryLineId)
                                |> Set.ofList
                            Assert.Equal<Set<StageEntryLineId>>(lineIds |> Set.ofList, matchedByRule)
                            let! links = linksTo context [ legId ]
                            let link = Assert.Single(links)
                            Assert.Equal(expectedLineId, link |> PaymentAgreementLink.stageEntryLineId)
                            return ()
                        })
                    |> convertListOfResultsToResultsList
                Assert.Equal(2, checks.Length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 a split tenant payment whose two claiming rules match the rent line and the utility-share line by amount links each line to its own Payment Agreement`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legIds = scenario.agreementWithLegs "CF-12.4 tenant" Income [ "CF-12.4 tenant rent"; "CF-12.4 tenant utility" ]
                let rentLegId, utilityLegId = legIds.[0], legIds.[1]
                let! amount = Money.fromDecimal 100.00M
                let invoiceFor legId =
                    (legId, None, InvoiceDate.create scenario.firstOfThisMonth, DueDate.create (scenario.firstOfThisMonth.PlusDays(30)),
                     InvoiceAmount.create amount, InvoiceSent, None, None, [])
                let! _ =
                    InstanceOrchestration.constructNewAndPersist context agreementId scenario.firstOfThisMonth
                        [ invoiceFor rentLegId; invoiceFor utilityLegId ]
                let! description = "CF-12.4 tenant payment" |> StringSearchPattern.create
                let! rent = Money.fromDecimal 100.00M
                let! utility = Money.fromDecimal 40.00M
                let exactly money = FieldMatch.Amount({ numericSearchOperator = ExactlyEqual; amount = money })
                let! _ = scenario.paymentAgreementRuleMatching rentLegId "CF-12.4 tenant rent" 500 [ FieldMatch.Description description; exactly rent ]
                let! _ = scenario.paymentAgreementRuleMatching utilityLegId "CF-12.4 tenant utility" 500 [ FieldMatch.Description description; exactly utility ]
                (* One deposit, split: rent and the utility share on separate Credit lines of the same revenue account. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 tenant payment" scenario.firstOfThisMonth "Classified"
                        [ (140.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-4290"); (40.00M, "Credit", "F-4290") ]
                let depositLineId, rentLineId, utilityLineId = lines.[0], lines.[1], lines.[2]
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context [ rentLegId; utilityLegId ]
                let linked = links |> List.map (fun l -> l |> PaymentAgreementLink.paymentAgreementId, l |> PaymentAgreementLink.stageEntryLineId) |> Set.ofList
                Assert.Equal<Set<PaymentAgreementId * StageEntryLineId>>(set [ rentLegId, rentLineId; utilityLegId, utilityLineId ], linked)
                let! depositLinks = scenario.linksOf depositLineId
                Assert.Empty(depositLinks)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 an Outgo agreement whose claiming rule matches the Credit line of a refund links that Credit line, although the expected-account default would want a Debit line on the debit account`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.4 refund" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 refund" 500 (Some "Credit")
                (* A refund: cash comes back, the liability goes up. The entry has no Debit line on the debit account. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 refund" scenario.firstOfThisMonth "Classified"
                        [ (100.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-2230") ]
                let debitLineId, creditLineId = lines.[0], lines.[1]
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context [ legId ]
                let link = Assert.Single(links)
                Assert.Equal(creditLineId, link |> PaymentAgreementLink.stageEntryLineId)
                let! debitLinks = scenario.linksOf debitLineId
                Assert.Empty(debitLinks)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 a single matched line on an account the Payment Agreement does not name is linked, the expected-account default not vetoing it`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.4 other account" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 other account" 500 (Some "Debit")
                (* The Debit line is on F-5350, an account the agreement does not name. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 other account" scenario.firstOfThisMonth "Classified"
                        [ (100.00M, "Debit", "F-5350"); (100.00M, "Credit", "F-1280") ]
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context [ legId ]
                let link = Assert.Single(links)
                Assert.Equal(lines.[0], link |> PaymentAgreementLink.stageEntryLineId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 for each of Income and Outgo, when the claiming rule matches several lines and exactly one is on the expected account with the expected line type, the claim links that one line`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                (* The rule picks out every line of the expected line type; one is on the expected account, one is not. *)
                let! cases =
                    [ Income, "CF-12.4 one survivor income", "Credit",
                      [ (60.00M, "Credit", "F-4290"); (40.00M, "Credit", "F-5350"); (100.00M, "Debit", "F-1280") ]
                      Outgo, "CF-12.4 one survivor outgo", "Debit",
                      [ (60.00M, "Debit", "F-2230"); (40.00M, "Debit", "F-5350"); (100.00M, "Credit", "F-1280") ] ]
                    |> List.map (fun (direction, label, lineType, lines) ->
                        result {
                            let! agreementId, legId = scenario.agreement label direction
                            let state = match direction with | Income -> InvoiceSent | Outgo -> InvoiceReceived
                            let! _ = scenario.invoiceWithPayments agreementId legId state scenario.firstOfThisMonth 30 []
                            let! _ = scenario.paymentAgreementRuleWith legId label 500 (Some lineType)
                            let! _, lineIds = scenario.stagedEntryWithLines label scenario.firstOfThisMonth "Classified" lines
                            return legId, lineIds.[0]
                        })
                    |> convertListOfResultsToResultsList
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! linked =
                    cases
                    |> List.map (fun (legId, _) ->
                        linksTo context [ legId ] |> Result.map (List.map PaymentAgreementLink.stageEntryLineId))
                    |> convertListOfResultsToResultsList
                Assert.Equal<StageEntryLineId list list>(cases |> List.map (fun (_, expected) -> [ expected ]), linked)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 for each of Income and Outgo, when the claiming rule matches several lines and more than one is on the expected account with the expected line type, the claim creates no link and is reported unlinked with the reason that the default left several lines`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! cases =
                    [ Income, "CF-12.4 several income", [ (60.00M, "Credit", "F-4290"); (40.00M, "Credit", "F-4290"); (100.00M, "Debit", "F-1280") ]
                      Outgo, "CF-12.4 several outgo", [ (60.00M, "Debit", "F-2230"); (40.00M, "Debit", "F-2230"); (100.00M, "Credit", "F-1280") ] ]
                    |> List.map (fun (direction, label, lines) ->
                        result {
                            let! _, legId = scenario.agreement label direction
                            let! _ = scenario.paymentAgreementRule legId label
                            let! _, lineIds = scenario.stagedEntryWithLines label scenario.firstOfThisMonth "Classified" lines
                            return legId, set [ lineIds.[0]; lineIds.[1] ]
                        })
                    |> convertListOfResultsToResultsList
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context (cases |> List.map fst)
                Assert.Empty(links)
                let reported =
                    cases
                    |> List.map (fun (legId, _) ->
                        run.decisionLog
                        |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                        |> List.map (fun d -> d.stageEntryLineId, d.outcome)
                        |> Set.ofList)
                let expected =
                    cases |> List.map (fun (_, survivors) -> survivors |> Set.map (fun line -> line, ManyLinesOnAgreementAccount))
                Assert.Equal<Set<StageEntryLineId * PaymentAgreementDecisionOutcome> list>(expected, reported)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 for each of Income and Outgo, when the claiming rule matches several lines and none is on the expected account with the expected line type, the claim creates no link and is reported unlinked with the reason that the default left no line`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                (* The line of the expected line type is on F-5350, not the expected account. *)
                let! cases =
                    [ Income, "CF-12.4 none income", [ (100.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-5350") ]
                      Outgo, "CF-12.4 none outgo", [ (100.00M, "Debit", "F-5350"); (100.00M, "Credit", "F-1280") ] ]
                    |> List.map (fun (direction, label, lines) ->
                        result {
                            let! _, legId = scenario.agreement label direction
                            let! _ = scenario.paymentAgreementRule legId label
                            let! _, lineIds = scenario.stagedEntryWithLines label scenario.firstOfThisMonth "Classified" lines
                            return legId, lineIds |> Set.ofList
                        })
                    |> convertListOfResultsToResultsList
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context (cases |> List.map fst)
                Assert.Empty(links)
                let reported =
                    cases
                    |> List.map (fun (legId, _) ->
                        run.decisionLog
                        |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                        |> List.map (fun d -> d.stageEntryLineId, d.outcome)
                        |> Set.ofList)
                let expected =
                    cases |> List.map (fun (_, kept) -> kept |> Set.map (fun line -> line, NoLineOnAgreementAccounts))
                Assert.Equal<Set<StageEntryLineId * PaymentAgreementDecisionOutcome> list>(expected, reported)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.4 when one claiming rule constrains line type and another does not, every line either rule matched is kept, so on a refund with no Debit line on the debit account the claim creates no link and both lines are reported with the no-line reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legId = scenario.agreement "CF-12.4 mixed" Outgo
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 mixed" 500 (Some "Credit")
                let! _ = scenario.paymentAgreementRuleWith legId "CF-12.4 mixed" 500 None
                (* The constrained rule matches the Credit line alone; the other matches both, so both are kept. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-12.4 mixed" scenario.firstOfThisMonth "Classified"
                        [ (100.00M, "Debit", "F-1280"); (100.00M, "Credit", "F-2230") ]
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context [ legId ]
                Assert.Empty(links)
                let reported =
                    run.decisionLog
                    |> List.filter (fun d -> d.paymentAgreementId = Some legId)
                    |> List.map (fun d -> d.stageEntryLineId, d.outcome)
                    |> Set.ofList
                let expected = lines |> List.map (fun line -> line, NoLineOnAgreementAccounts) |> Set.ofList
                Assert.Equal<Set<StageEntryLineId * PaymentAgreementDecisionOutcome>>(expected, reported)
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = linksTo context [ legId ]
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                (* The run did what the cases claim: one link, unlinked no-line claims, contested claims. *)
                let! links = linksTo context [ legId ]
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
                let! _ = CashFlowOps.constructNewAndPersist context legId lineId
                let! after = scenario.linksOf lineId
                let link = Assert.Single(after)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.7 creating a link for a line that is already linked is rejected with a typed error naming the existing link's ID and its Payment Agreement's name, and the existing link is unchanged`` () =
        runCommandRouteAndAutoRollback CreatePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, legXId = scenario.agreement "CF-12.7 already X" Outgo
                let! _, legYId = scenario.agreement "CF-12.7 already Y" Outgo
                let! _, lineId, _ = scenario.outgoEntry "CF-12.7 already payment" scenario.firstOfThisMonth "Classified"
                let! existing = CashFlowOps.constructNewAndPersist context legXId lineId
                let! legX = legXId |> PaymentAgreement.fetchById context
                let! _ =
                    match CashFlowOps.constructNewAndPersist context legYId lineId with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementLinkLineAlreadyLinked (namedLine, namedLink, namedAgreement))) ->
                        Assert.Equal(lineId |> StageEntryLineId.value, namedLine)
                        Assert.Equal(existing |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLinkId.value, namedLink)
                        Assert.Equal(legX |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value, namedAgreement)
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
                let! existing = CashFlowOps.constructNewAndPersist context legXId lineId
                let! _ =
                    CashFlowOps.updatePaymentAgreementLink context
                        { linkIdToUpdate = existing |> PaymentAgreementLink.paymentAgreementLinkId
                          paymentAgreementIdUpdate = FieldUpdate.SetTo legYId }
                let! after = scenario.linksOf lineId
                let link = Assert.Single(after)
                Assert.Equal(legYId, link |> PaymentAgreementLink.paymentAgreementId)
                let! linksToX = linksTo context [ legXId ]
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
                let! _ = link |> PaymentAgreementLink.paymentAgreementLinkId |> CashFlowOps.deletePaymentAgreementLink context
                let! afterDelete = scenario.linksOf lineId
                Assert.Empty(afterDelete)
                let! _ = scenario.paymentAgreementRule legId "CF-12.7 delete payment"
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! earlierRun = ClassificationOrchestration.classifyPaymentAgreements context
                (* Retire the earlier rule, so the later run does not match the earlier entry's unlinked Credit line
                   again: every row the earlier run recorded stays in the table and must stay out of the later run. *)
                let! earlierRows = earlierRun.runId |> ClassificationOrchestration.fetchRunMatchesWithRules context
                Assert.NotEmpty(earlierRows)
                let! _ = earlierRule |> scenario.setRuleActive false
                (* The later line is matched by two rules for X at different priorities; X is claimed by the winner. *)
                let! winner = scenario.paymentAgreementRuleWith legXId "CF-12.8 later" 150 None
                let! loser = scenario.paymentAgreementRuleWith legXId "CF-12.8 later" 350 None
                let! _, laterDebitId, laterCreditId = scenario.outgoEntry "CF-12.8 later" scenario.firstOfThisMonth "Classified"
                let! laterRun = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                        [ TransactionPointer.Staged paidLineId ]
                let! paidComposite = paidInvoiceId |> InstanceOrchestration.fetchCompositeByInvoiceId context
                let paidState = paidComposite |> InstanceOrchestration.invoice |> Invoice.invoiceLifeCycleState
                Assert.Equal(FullyPaid, (paidState |> CashFlowComponent.InvoiceLifeCycleState.paymentState))
                let! _, newerInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legId "CF-13.1 fully paid payment" scenario.firstOfThisMonth
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                        [ TransactionPointer.Staged paidLineId ]
                let! fulfilled = fulfilledInstanceId |> InstanceOrchestration.fetchCompositeByInstanceId context
                Assert.True(fulfilled |> InstanceOrchestration.instance |> Instance.isFulfilled)
                let! _, newerInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! lineId = scenario.linkedLine legId "CF-13.1 fulfilled payment" scenario.firstOfThisMonth
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                        [ TransactionPointer.Staged lineId ]
                let! _, openInvoiceId = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
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
                let! orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! _ = CashFlowOps.constructNewAndPersist context legId lineId
                let! journalEntriesBefore = scenario.journalEntryCount ()
                let! instancesBefore, invoicesBefore = scenario.instanceAndInvoiceIds ()
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                        [ TransactionPointer.Staged oldLineId ]
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let listed = run.openInstances |> List.map (InstanceOrchestration.instance >> Instance.instanceId) |> Set.ofList
                Assert.DoesNotContain(fulfilledByRunId, listed)
                Assert.Contains(leftOpenId, listed)
                (* the expected set comes from what the test and the fixture created, not from the system's own
                   open-Instance query: the fixture's open Instances are this month's on A, B and C, none of which the
                   run can pay, and the one this test left open *)
                let cashFlow = fixture.Data.cashFlow
                let instanceOfInvoice invoiceId = invoiceId |> Invoice.fetchById context |> Result.map Invoice.instanceId
                let! fixtureB = instanceOfInvoice cashFlow.openInvoiceBId
                let! fixtureC = instanceOfInvoice cashFlow.partlyPaidInvoiceCId
                let expected = Set.ofList [ cashFlow.openInstanceAId; fixtureB; fixtureC; leftOpenId ]
                Assert.Equal<Set<InstanceId>>(expected, listed)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.1, 13.4, 13.9 — tie-break, posted-to-FI date, blockers, links in the result
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.1 when two open Invoices share a due date, a linked line both windows cover is paid to the Invoice entered first, whichever of the two is entered first`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                (* Each agreement has last month's and this month's Instance, created empty, and an Invoice on each, both
                   due on the 1st of this month. On one agreement last month's Invoice is entered first, on the other
                   this month's. The second Invoice is entered at a later instant. *)
                let! cases =
                    [ "CF-13.1 tie older entered first", true; "CF-13.1 tie newer entered first", false ]
                    |> List.map (fun (name, olderFirst) ->
                        result {
                            let! agreementId, legId = scenario.agreement name Outgo
                            let! lastMonthInstanceId = scenario.emptyInstance agreementId scenario.firstOfLastMonth
                            let! thisMonthInstanceId = scenario.emptyInstance agreementId scenario.firstOfThisMonth
                            let due = scenario.firstOfThisMonth
                            let addOlder (s: Scenario) = s.addOutgoInvoice lastMonthInstanceId legId scenario.firstOfLastMonth due
                            let addNewer (s: Scenario) = s.addOutgoInvoice thisMonthInstanceId legId scenario.firstOfThisMonth due
                            let! firstId = if olderFirst then addOlder scenario else addNewer scenario
                            Threading.Thread.Sleep(10)
                            let later = Scenario(fixture, context |> TestContext.updateInitiationInstant)
                            let! secondId = if olderFirst then addNewer later else addOlder later
                            let! lineId = scenario.linkedLine legId $"{name} payment" scenario.firstOfThisMonth
                            return firstId, secondId, lineId
                        })
                    |> convertListOfResultsToResultsList
                (* The setup holds: equal due dates, the first entered strictly before the second. *)
                let! invoices = cases |> List.collect (fun (first, second, _) -> [ first; second ]) |> Invoice.fetchByIdList context
                let invoiceOf id = invoices |> List.find (fun invoice -> invoice |> Invoice.invoiceId = id)
                Assert.All(cases, fun (first, second, _) ->
                    Assert.Equal(invoiceOf first |> Invoice.dueDate, invoiceOf second |> Invoice.dueDate)
                    Assert.True((invoiceOf first |> Invoice.createdAt) < (invoiceOf second |> Invoice.createdAt)))
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! paidTo =
                    cases
                    |> List.map (fun (_, _, lineId) -> scenario.paymentsOn lineId |> Result.map (List.map Payment.invoiceId))
                    |> convertListOfResultsToResultsList
                Assert.Equal<InvoiceId list list>(cases |> List.map (fun (first, _, _) -> [ first ]), paidTo)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.4 a Payment created by matching carries its staged entry's date as its posted-to-FI date`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.4 FI date" Outgo
                let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                (* A date that is neither the Invoice's date nor its due date. *)
                let entryDate = scenario.firstOfThisMonth.PlusDays(3)
                let! lineId = scenario.linkedLine legId "CF-13.4 FI date payment" entryDate
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! payments = scenario.paymentsOn lineId
                let payment = Assert.Single(payments)
                Assert.Equal(Some entryDate, payment |> Payment.postedToFiDate |> Option.map PostedToFiDate.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.4 REQ-CF-13.9 when matching brings a blocked Invoice to FullyPaid, the Invoice reads back with no blocker state and no blocker note, and the result reports the cleared blocker`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.4 blocker cleared" Outgo
                let! note = "CF-13.4 waiting on the lender's statement" |> BlockerNote.create
                let blocker = Other note
                let! _, invoiceId =
                    scenario.invoiceWithBlockerAndPayments agreementId legId InvoiceReceived scenario.firstOfThisMonth 30 (Some blocker) []
                let! _ = scenario.linkedLine legId "CF-13.4 blocker cleared payment" scenario.firstOfThisMonth
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! composite = invoiceId |> InstanceOrchestration.fetchCompositeByInvoiceId context
                let state = composite |> InstanceOrchestration.invoice |> Invoice.invoiceLifeCycleState
                Assert.Equal(FullyPaid, state |> InvoiceLifeCycleState.paymentState)
                Assert.Equal(None, state |> InvoiceLifeCycleState.blocker)
                let cleared =
                    run |> invoiceDecisionsFor invoiceId |> List.choose (fun d -> match d.outcome with BlockerCleared b -> Some b | _ -> None)
                Assert.Equal<Blocker list>([ blocker ], cleared)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.4 when matching leaves a blocked Invoice PartiallyPaid, the blocker state and note are kept and the result reports no cleared blocker`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.4 blocker kept" Outgo
                let! note = "CF-13.4 the rest is disputed" |> BlockerNote.create
                let blocker = NeedsDecision note
                let! _, invoiceId =
                    scenario.invoiceWithBlockerAndPayments agreementId legId InvoiceReceived scenario.firstOfThisMonth 30 (Some blocker) []
                (* A 40.00 payment against the 100.00 Invoice. *)
                let! _, lines =
                    scenario.stagedEntryWithLines "CF-13.4 blocker kept payment" scenario.firstOfThisMonth "Classified"
                        [ (40.00M, "Debit", "F-2230"); (40.00M, "Credit", "F-1280") ]
                let! _ = CashFlowOps.constructNewAndPersist context legId lines.[0]
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                let! composite = invoiceId |> InstanceOrchestration.fetchCompositeByInvoiceId context
                let state = composite |> InstanceOrchestration.invoice |> Invoice.invoiceLifeCycleState
                Assert.Equal(PartiallyPaid, state |> InvoiceLifeCycleState.paymentState)
                Assert.Equal(Some blocker, state |> InvoiceLifeCycleState.blocker)
                let outcomes = run |> invoiceDecisionsFor invoiceId |> List.map _.outcome
                Assert.Equal<InvoiceDecisionOutcome list>([ PaymentCreated lines.[0] ], outcomes)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.9 the result lists every link the run created with that link's stored ID, and each decision that produced a link carries the ID of that link`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! linkedLines =
                    [ "CF-13.9 link id X"; "CF-13.9 link id Y" ]
                    |> List.map (fun name ->
                        result {
                            let! agreementId, legId = scenario.agreement name Outgo
                            let! _ = scenario.outgoInvoice agreementId legId scenario.firstOfThisMonth 30
                            let! _ = scenario.paymentAgreementRule legId $"{name} payment"
                            let! _, lineId, _ = scenario.outgoEntry $"{name} payment" scenario.firstOfThisMonth "Classified"
                            return lineId
                        })
                    |> convertListOfResultsToResultsList
                (* A contested agreement: its decisions produce no link. *)
                let! _, contestedLegId = scenario.agreement "CF-13.9 link id contested" Outgo
                let! _ = scenario.paymentAgreementRule contestedLegId "CF-13.9 link id contested payment"
                let! _ = scenario.outgoEntry "CF-13.9 link id contested payment one" scenario.firstOfThisMonth "Classified"
                let! _ = scenario.outgoEntry "CF-13.9 link id contested payment two" scenario.firstOfThisMonth "Classified"
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
                (* Every link the run wrote carries the run's instant; read them back from the store. *)
                let initiation = context |> Context.getInitiationInstant
                let! allLinks = PaymentAgreementLink.fetchAll context
                let stored =
                    allLinks
                    |> List.filter (fun link -> link |> PaymentAgreementLink.createdAt = initiation)
                    |> List.map (fun link -> link |> PaymentAgreementLink.paymentAgreementLinkId, link |> PaymentAgreementLink.stageEntryLineId)
                    |> Set.ofList
                let listed =
                    run.linksCreated
                    |> List.map (fun link -> link |> PaymentAgreementLink.paymentAgreementLinkId, link |> PaymentAgreementLink.stageEntryLineId)
                    |> Set.ofList
                Assert.Equal<Set<PaymentAgreementLinkId * StageEntryLineId>>(stored, listed)
                Assert.All(linkedLines, fun line -> Assert.Contains(line, listed |> Set.map snd))
                let linkDecisions =
                    run.decisionLog
                    |> List.filter (fun d -> d.outcome = Linked)
                    |> List.map (fun d -> d.paymentAgreementLinkId, d.stageEntryLineId)
                    |> Set.ofList
                Assert.Equal<Set<PaymentAgreementLinkId option * StageEntryLineId>>(listed |> Set.map (fun (id, line) -> Some id, line), linkDecisions)
                let contested = run.decisionLog |> List.filter (fun d -> d.paymentAgreementId = Some contestedLegId)
                Assert.Equal(2, contested.Length)
                Assert.All(contested, fun d -> Assert.Equal(None, d.paymentAgreementLinkId))
            })
        |> railroadWrapper
