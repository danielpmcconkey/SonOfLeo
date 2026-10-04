module Tests.Integrated.CrossDomainOrchestration.ClassificationClaimantsAndRuns

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.QueryParameter
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.Classification.FieldMatch
open Business.FinancialServices.Classification.ClassificationRuleGroup
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit
open Business.FinancialServices.Classification.ClassificationError

module Contracts = Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts

(* Staged entries here come from TestCreditCardCo, whose only fixture rule needs an "REI REI Co-op" description, so no
   fixture rule matches a line unless a test means it to. Each test's rules match on a description carrying a fresh
   tag. Most tests run inside a transaction that is rolled back. The REQ-CR-8.3 duplicate test and the REQ-CR-8.5
   tests read through the routes, so their setup commits and a finally deletes the run's match rows, the rules and the
   staged entries. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private withTransitions (status: string) =
    let start = Clock.now ()
    [ yield (None, "Ingested", start, "StageIngestion")
      if status = "Classified" then
          yield (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier") ]

let private candidateOf (entry: StageEntryOrchestration.StageEntry) (line: StageEntryLine.StageEntryLine) : MatchCandidate =
    let header = entry |> StageEntryOrchestration.stageEntryHeader
    { headerIdOfCandidate = header |> StageEntryHeader.stageEntryHeaderId
      lineIdOfCandidate = line |> StageEntryLine.stageEntryLineId
      ingestionSource = header |> StageEntryHeader.ingestionSource |> IngestionSource.name
      description = header |> StageEntryHeader.description
      amount = line |> StageEntryLine.amount
      lineType = line |> StageEntryLine.lineType
      memo = line |> StageEntryLine.memo }

let private debitLineOf (entry: StageEntryOrchestration.StageEntry) =
    entry
    |> StageEntryOrchestration.seLines
    |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)

let private lineIdsOf (entry: StageEntryOrchestration.StageEntry) =
    entry |> StageEntryOrchestration.seLines |> List.map StageEntryLine.stageEntryLineId

let private resultFor (lineId: StageEntryLineId) (results: ClassificationResult list) =
    results |> List.filter (fun r -> r.candidate.lineIdOfCandidate = lineId) |> List.exactlyOne

let private oneMatchRule (outcome: ClassifierOutcome) =
    match outcome with
    | OneMatch m -> Some m.ruleId
    | _ -> None

let private rowsOn (lineId: StageEntryLineId) (rows: RuleMatch.RuleMatch list) =
    rows |> List.filter (fun r -> r |> RuleMatch.stageEntryLineId = lineId)

let private ruleIdOf (rule: ClassificationRule.ClassificationRule) = rule |> ClassificationRule.classificationRuleId

let private noRuleFilter : ClassificationRuleFilter =
    { ruleId = None
      nameLike = None
      accountAtMatch = None
      paymentAgreementAtMatch = None
      claimantType = None
      sourceLike = None
      activeOnly = false }

let private groups (primitives: (string * FieldMatch list * FieldMatch list option) list) =
    primitives |> createClassificationRuleGroupListForTest |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private pattern (text: string) =
    text |> StringSearchPattern.create |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private describedAs (text: string) = FieldMatch.Description(pattern text)

type private Scenario(fixture: TestDataFixture, initialContext: Context.Context) =
    // A run advances the instant first: the staged-entry audit is keyed on entry and instant, so a run that updates an
    // entry at the instant it was created at is refused.
    let mutable context = initialContext
    let idOfCode code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let cardSource =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestCreditCardCo")

    member _.Context = context
    member _.accountIdOf code = idOfCode code

    /// A rule for the claimant with the groups.
    member _.rule (name: string) (claimant: ClassificationClaimant) (priority: int) (ruleGroups: ClassificationRuleGroup list) =
        result {
            let! ruleName = $"{name} {Guid.NewGuid():N}" |> ClassificationRuleName.create
            return! ClassificationOrchestration.constructNewAndPersist context ruleName claimant priority ruleGroups
        }

    /// An account rule claiming any line whose entry description matches the pattern.
    member this.accountRule (name: string) (code: string) (priority: int) (descriptionPattern: string) =
        this.rule name (ClassificationClaimant.Account(idOfCode code)) priority (groups [ ("And", [ describedAs descriptionPattern ], None) ])

    /// A payment agreement rule claiming any line whose entry description matches the pattern.
    member this.paymentAgreementRule (name: string) (legId: PaymentAgreementId) (priority: int) (descriptionPattern: string) =
        this.rule name (ClassificationClaimant.PaymentAgreement legId) priority (groups [ ("And", [ describedAs descriptionPattern ], None) ])

    /// An Outgo agreement with one leg on F-2230 and F-1280. Returns the leg.
    member _.leg () =
        result {
            let name = $"Classification test {Guid.NewGuid():N}"
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Classification test counterparty" |> Counterparty.create
            let! activityPeriod = ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = $"{name} leg" |> PaymentAgreementName.create
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first)) { nextInstance = LocalDate(2049, 3, 1) }
                    counterparty activityPeriod None
                    [ (legName, DebitAccount.create(idOfCode "F-2230"), CreditAccount.create(idOfCode "F-1280"), None, None, None) ]
            return agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
        }

    /// A TestCreditCardCo staged entry with the description: a 100.00 Debit and a 100.00 Credit, on F-2230 and F-1280
    /// when withAccounts, otherwise with no account; Ingested, or Classified.
    member _.entry (description: string) (status: string) (withAccounts: bool) =
        createStageEntryForTest context "/tmp/classification-test.dat" description (Guid.NewGuid().ToString()) cardSource
            (Calendar.today ())
            [ (100.00M, "Debit", (if withAccounts then Some "F-2230" else None), None, None)
              (100.00M, "Credit", (if withAccounts then Some "F-1280" else None), None, None) ]
            (withTransitions status)

    member _.accountRun () =
        context <- context |> TestContext.updateInitiationInstant
        ClassificationOrchestration.classifyAccounts context
    member _.paymentAgreementRun () =
        context <- context |> TestContext.updateInitiationInstant
        ClassificationOrchestration.classifyPaymentAgreements context
    member _.rowsOf (runId: ClassificationRunId) = runId |> RuleMatch.fetchByRunId context

let private rolledBack (fixture: TestDataFixture) (body: Scenario -> Result<unit, IAppError>) =
    runCommandRouteAndAutoRollback ClassifyAccounts (fun context -> body (Scenario(fixture, context))) |> railroadWrapper

/// Runs each step in its own committing Scenario.
type private Committer(fixture: TestDataFixture) =
    member _.Run(step: Scenario -> Result<'a, IAppError>) =
        runCommandRouteAndAutoCompleteTransaction ClassifyAccounts (fun context -> step (Scenario(fixture, context)))

[<Collection("SharedTestData")>]
type ClassificationClaimantsAndRunsTests(fixture: TestDataFixture) =

    (* Runs the test with a committing Scenario per step, then deletes the match rows of every rule it made, the rules
       and the staged entries. *)
    let withCommitted (test: Committer -> ResizeArray<ClassificationRuleId> -> ResizeArray<StageEntryHeaderId> -> Result<unit, IAppError>) =
        let rules = ResizeArray<ClassificationRuleId>()
        let entries = ResizeArray<StageEntryHeaderId>()
        let committed = Committer(fixture)
        let cleanUpFailures = ResizeArray<string>()
        try
            test committed rules entries |> railroadWrapper
        finally
            [ for id in rules do yield Cleanup.cleanUpRuleMatchesOfRuleId (Some id)
              for id in rules do yield Cleanup.cleanUpClassificationRuleId (Some id)
              for id in entries do yield Cleanup.cleanUpStageEntryHeaderId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

    let fetchRun (runId: Guid) =
        ({ runId = runId } : Contracts.FetchClassificationRunInput)
        |> Json.toJson
        |> Result.bind (routeUiCommandForTesting "Classification" "FetchClassificationRun" [])
        |> Result.bind Json.fromJson<Contracts.ClassificationRunReturn>

    // =========================================================================
    // REQ-CR-1.23, 1.24 — claimants
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-1.23 a rule created with an account claimant reads back with claimant type 'AccountClaimant', and one created with a payment agreement claimant reads back with 'PaymentAgreementClaimant'`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! legId = s.leg ()
                let! accountRule = s.accountRule tag "F-1280" 100 tag
                let! paymentRule = s.paymentAgreementRule tag legId 100 tag
                let! name = tag |> ClassificationRuleName.create
                let! accountTyped =
                    ClassificationOrchestration.fetchRulesFiltered s.Context { noRuleFilter with nameLike = Some name; claimantType = Some AccountClaimant } None
                let! paymentTyped =
                    ClassificationOrchestration.fetchRulesFiltered s.Context { noRuleFilter with nameLike = Some name; claimantType = Some PaymentAgreementClaimant } None
                Assert.Equal<ClassificationRuleId list>([ ruleIdOf accountRule ], accountTyped |> List.map ruleIdOf)
                Assert.Equal<ClassificationRuleId list>([ ruleIdOf paymentRule ], paymentTyped |> List.map ruleIdOf)
            })

    [<Theory>]
    [<InlineData("both")>]
    [<InlineData("neither")>]
    member _.``REQ-CR-1.24 for each of both claimants set and neither set, reading a stored rule in that state by ID, by name or by filter fails with a typed error naming the rule's ID`` (claimants: string) =
        rolledBack fixture (fun s ->
            result {
                let! legId = s.leg ()
                let ruleUuid = Guid.NewGuid()
                let name = $"Broken claimant rule {newTag ()}"
                let accountUuid, legUuid =
                    if claimants = "both" then Some(s.accountIdOf "F-1280" |> AccountId.value), Some(legId |> PaymentAgreementId.value)
                    else None, None
                let now = s.Context |> Context.getInitiationInstant
                let! _ =
                    executeNonQuery (s.Context |> Context.getDatabaseTransaction)
                        """insert into classification.classification_rule
                           (unique_id, rule_name, account_at_match, payment_agreement_at_match, priority, rule_groups, is_active, created_at, modified_at)
                           values (@id, @name, @account, @leg, 100, @groups, true, @now, @now)"""
                        [ { name = "@id"; value = UniqueId ruleUuid }
                          { name = "@name"; value = CharString name }
                          { name = "@account"; value = NullableUniqueId accountUuid }
                          { name = "@leg"; value = NullableUniqueId legUuid }
                          { name = "@groups"; value = Jsonb """[{"chainOne": {"chain": [{"Case": "Description", "Fields": ["x"]}]}, "chainTwo": null, "connector": {"Case": "And"}}]""" }
                          { name = "@now"; value = DbInstant now } ]
                        ExactlyOne
                let! ruleName = name |> ClassificationRuleName.create
                let reads =
                    [ ruleUuid |> ClassificationRuleId.fromGuid |> ClassificationRule.fetchById s.Context |> Result.map ignore
                      ruleName |> ClassificationRule.fetchByName s.Context |> Result.map ignore
                      ClassificationOrchestration.fetchRulesFiltered s.Context { noRuleFilter with nameLike = Some ruleName } None |> Result.map ignore ]
                let namesTheRule (read: Result<unit, IAppError>) =
                    match read with
                    | Error (AsError (ClassificationRuleInvalidClaimant(id, _, _))) -> id = ruleUuid
                    | _ -> false
                Assert.All(reads, fun read -> Assert.True(namesTheRule read))
            })

    // =========================================================================
    // REQ-CR-3.7, 3.8 — which rules a run uses, and what the classifier ignores
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-3.7 an account classification run reports NoMatch for a line matched only by an active payment-agreement-claimant rule, and a payment-agreement run reports NoMatch for a line matched only by an active account-claimant rule`` () =
        rolledBack fixture (fun s ->
            result {
                let paymentTag = newTag ()
                let! legId = s.leg ()
                let! _ = s.paymentAgreementRule paymentTag legId 100 paymentTag
                let! entry = s.entry $"Payment only {paymentTag}" "Ingested" false
                let! run = s.accountRun ()
                let outcome = (run.classificationResults |> resultFor (entry |> debitLineOf |> StageEntryLine.stageEntryLineId)).outcome
                Assert.Equal(ClassifierOutcome.NoMatch, outcome)
            })
        rolledBack fixture (fun s ->
            result {
                let accountTag = newTag ()
                let! _ = s.accountRule accountTag "F-1280" 100 accountTag
                let! entry = s.entry $"Account only {accountTag}" "Classified" true
                let! run = s.paymentAgreementRun ()
                let outcome = (run.classificationResults |> resultFor (entry |> debitLineOf |> StageEntryLine.stageEntryLineId)).outcome
                Assert.Equal(ClassifierOutcome.NoMatch, outcome)
            })

    [<Fact>]
    member _.``REQ-CR-3.7 when an account rule and a payment-agreement rule of equal priority both match a line, the account run reports OneMatch carrying the account rule and the payment-agreement run reports OneMatch carrying the payment-agreement rule`` () =
        let both (run: Scenario -> Result<ClassificationResult list, IAppError>) (expectAccountRule: bool) =
            rolledBack fixture (fun s ->
                result {
                    let tag = newTag ()
                    let! legId = s.leg ()
                    let! accountRule = s.accountRule tag "F-1280" 100 tag
                    let! paymentRule = s.paymentAgreementRule tag legId 100 tag
                    let! entry = s.entry $"Both {tag}" "Ingested" false
                    let! results = run s
                    let outcome = (results |> resultFor (entry |> debitLineOf |> StageEntryLine.stageEntryLineId)).outcome
                    let expected = if expectAccountRule then ruleIdOf accountRule else ruleIdOf paymentRule
                    Assert.Equal(Some expected, oneMatchRule outcome)
                })
        both (fun s -> s.accountRun () |> Result.map _.classificationResults) true
        both (fun s -> s.paymentAgreementRun () |> Result.map _.classificationResults) false

    [<Fact>]
    member _.``REQ-CR-3.8 a line that already has an account gets the same OneMatch, carrying the same account rule, as an otherwise identical line with no account`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! rule = s.accountRule tag "F-1280" 100 tag
                let! assigned = s.entry $"Assigned {tag}" "Classified" true
                let! unassigned = s.entry $"Assigned {tag}" "Ingested" false
                let assignedLine = assigned |> debitLineOf
                let unassignedLine = unassigned |> debitLineOf
                let! run =
                    [ candidateOf assigned assignedLine; candidateOf unassigned unassignedLine ]
                    |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context AccountClaimant
                let assignedOutcome = (run.results |> resultFor (assignedLine |> StageEntryLine.stageEntryLineId)).outcome
                let unassignedOutcome = (run.results |> resultFor (unassignedLine |> StageEntryLine.stageEntryLineId)).outcome
                Assert.True((assignedLine |> StageEntryLine.accountId).IsSome)
                Assert.Equal(Some(ruleIdOf rule), oneMatchRule assignedOutcome)
                Assert.Equal(unassignedOutcome, assignedOutcome)
            })

    [<Fact>]
    member _.``REQ-CR-3.8 a line that is already linked gets the same OneMatch, carrying the same payment-agreement rule, as an otherwise identical unlinked line`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! legId = s.leg ()
                let! rule = s.paymentAgreementRule tag legId 100 tag
                let! linked = s.entry $"Linked {tag}" "Classified" true
                let! unlinked = s.entry $"Linked {tag}" "Classified" true
                let linkedLine = linked |> debitLineOf
                let unlinkedLine = unlinked |> debitLineOf
                let! _ = CashFlowOps.constructNewAndPersist s.Context legId (linkedLine |> StageEntryLine.stageEntryLineId)
                let! links = linkedLine |> StageEntryLine.stageEntryLineId |> PaymentAgreementLink.fetchByStageEntryLineId s.Context
                let! run =
                    [ candidateOf linked linkedLine; candidateOf unlinked unlinkedLine ]
                    |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context PaymentAgreementClaimant
                let linkedOutcome = (run.results |> resultFor (linkedLine |> StageEntryLine.stageEntryLineId)).outcome
                let unlinkedOutcome = (run.results |> resultFor (unlinkedLine |> StageEntryLine.stageEntryLineId)).outcome
                Assert.Single(links) |> ignore
                Assert.Equal(Some(ruleIdOf rule), oneMatchRule linkedOutcome)
                Assert.Equal(unlinkedOutcome, linkedOutcome)
            })

    // =========================================================================
    // REQ-CR-8.1, 8.2, 8.3 — what a run records
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-8.1 two classification runs return distinct, non-empty run IDs, and each run's match rows carry the ID that run returned`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                // two rules tie, so the entry stays in Conflict and the second run evaluates it again
                let! _ = s.accountRule tag "F-1280" 100 tag
                let! _ = s.accountRule tag "F-2230" 100 tag
                let! entry = s.entry $"Runs {tag}" "Ingested" false
                let! first = s.accountRun ()
                let! second = s.accountRun ()
                let! firstRows = s.rowsOf first.runId
                let! secondRows = s.rowsOf second.runId
                let line = entry |> debitLineOf |> StageEntryLine.stageEntryLineId
                Assert.NotEqual(Guid.Empty, first.runId |> ClassificationRunId.value)
                Assert.NotEqual(Guid.Empty, second.runId |> ClassificationRunId.value)
                Assert.NotEqual(first.runId, second.runId)
                Assert.NotEmpty(firstRows |> rowsOn line)
                Assert.NotEmpty(secondRows |> rowsOn line)
                Assert.All(firstRows, fun r -> Assert.Equal(first.runId, r |> RuleMatch.runId))
                Assert.All(secondRows, fun r -> Assert.Equal(second.runId, r |> RuleMatch.runId))
            })

    [<Fact>]
    member _.``REQ-CR-8.2 a line matched by a winning rule and a losing rule records one match row per rule, each with a system-generated ID, the run ID, the staged line ID, the rule ID and the run's Instant`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! winner = s.accountRule tag "F-1280" 10 tag
                let! loser = s.accountRule tag "F-2230" 20 tag
                let! entry = s.entry $"Winner {tag}" "Ingested" false
                let! run = s.accountRun ()
                let! rows = s.rowsOf run.runId
                let line = entry |> debitLineOf |> StageEntryLine.stageEntryLineId
                let onLine = rows |> rowsOn line
                let initiation = s.Context |> Context.getInitiationInstant
                Assert.Equal<Set<ClassificationRuleId>>(set [ ruleIdOf winner; ruleIdOf loser ], onLine |> List.map RuleMatch.classificationRuleId |> Set.ofList)
                Assert.Equal(2, onLine.Length)
                Assert.Equal(2, onLine |> List.map RuleMatch.classificationMatchId |> List.distinct |> List.length)
                Assert.All(onLine, fun r ->
                    Assert.NotEqual(Guid.Empty, r |> RuleMatch.classificationMatchId |> ClassificationMatchId.value)
                    Assert.Equal(run.runId, r |> RuleMatch.runId)
                    Assert.Equal(line, r |> RuleMatch.stageEntryLineId)
                    Assert.Equal(initiation, r |> RuleMatch.createdAt))
            })

    [<Fact>]
    member _.``REQ-CR-8.2 a line on which two rules tie records exactly one match row per tied rule`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! first = s.accountRule tag "F-1280" 50 tag
                let! second = s.accountRule tag "F-2230" 50 tag
                let! entry = s.entry $"Tie {tag}" "Ingested" false
                let! run = s.accountRun ()
                let! rows = s.rowsOf run.runId
                let onLine = rows |> rowsOn (entry |> debitLineOf |> StageEntryLine.stageEntryLineId)
                Assert.Equal<ClassificationRuleId list>(
                    [ ruleIdOf first; ruleIdOf second ] |> List.sort,
                    onLine |> List.map RuleMatch.classificationRuleId |> List.sort)
            })

    [<Fact>]
    member _.``REQ-CR-8.2 a line matched by exactly one rule records exactly one match row`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! rule = s.accountRule tag "F-1280" 50 tag
                let! entry = s.entry $"One {tag}" "Ingested" false
                let! run = s.accountRun ()
                let! rows = s.rowsOf run.runId
                let onLine = rows |> rowsOn (entry |> debitLineOf |> StageEntryLine.stageEntryLineId)
                Assert.Equal<ClassificationRuleId list>([ ruleIdOf rule ], onLine |> List.map RuleMatch.classificationRuleId)
            })

    [<Fact>]
    member _.``REQ-CR-8.2 a line no rule matches records no match row`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let! _ = s.accountRule tag "F-1280" 50 tag
                let! matched = s.entry $"Matched {tag}" "Ingested" false
                let! unmatched = s.entry $"Unmatched {newTag ()}" "Ingested" false
                let! run = s.accountRun ()
                let! rows = s.rowsOf run.runId
                let unmatchedLine = unmatched |> debitLineOf |> StageEntryLine.stageEntryLineId
                Assert.Equal(ClassifierOutcome.NoMatch, (run.classificationResults |> resultFor unmatchedLine).outcome)
                Assert.NotEmpty(rows |> rowsOn (matched |> debitLineOf |> StageEntryLine.stageEntryLineId))
                Assert.Empty(rows |> rowsOn unmatchedLine)
            })

    [<Fact>]
    member _.``REQ-CR-8.2 a run over several candidate lines records match rows for each line's own matching rules and no others`` () =
        rolledBack fixture (fun s ->
            result {
                let first, second, third = newTag (), newTag (), newTag ()
                let! onlyFirst = s.accountRule "Several" "F-1280" 10 first
                let! secondOrThird = s.accountRule "Several" "F-2230" 20 $"{second}|{third}"
                let! onlyThird = s.accountRule "Several" "F-1290" 30 third
                let! e1 = s.entry $"Several {first}" "Ingested" false
                let! e2 = s.entry $"Several {second}" "Ingested" false
                let! e3 = s.entry $"Several {third}" "Ingested" false
                let! run = s.accountRun ()
                let! rows = s.rowsOf run.runId
                let rulesOn line = rows |> rowsOn line |> List.map RuleMatch.classificationRuleId |> List.sort
                let expect (entry: StageEntryOrchestration.StageEntry) (rules: ClassificationRule.ClassificationRule list) =
                    for line in entry |> lineIdsOf do
                        Assert.Equal<ClassificationRuleId list>(rules |> List.map ruleIdOf |> List.sort, rulesOn line)
                expect e1 [ onlyFirst ]
                expect e2 [ secondOrThird ]
                expect e3 [ secondOrThird; onlyThird ]
            })

    [<Fact>]
    member _.``REQ-CR-8.3 recording a match row for a (run, staged line, rule) combination already recorded leaves exactly one row for that combination`` () =
        withCommitted (fun committed rules entries ->
            result {
                let tag = newTag ()
                let! rule, entry, run =
                    committed.Run(fun s ->
                        result {
                            let! rule = s.accountRule tag "F-1280" 50 tag
                            let! entry = s.entry $"Duplicate row {tag}" "Ingested" false
                            let! run =
                                [ candidateOf entry (debitLineOf entry) ]
                                |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context AccountClaimant
                            return rule, entry, run
                        })
                rules.Add(ruleIdOf rule)
                entries.Add(entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                let line = entry |> debitLineOf |> StageEntryLine.stageEntryLineId
                let _second =
                    committed.Run(fun s ->
                        RuleMatch.create (ClassificationMatchId.create ()) run.runId line (ruleIdOf rule) (s.Context |> Context.getInitiationInstant)
                        |> RuleMatch.persist s.Context)
                let! rows = run.runId |> RuleMatch.fetchByRunId (fresh ())
                let forCombination = rows |> List.filter (fun r -> r |> RuleMatch.stageEntryLineId = line && r |> RuleMatch.classificationRuleId = ruleIdOf rule)
                Assert.Single(forCombination) |> ignore
            })

    [<Fact>]
    member _.``REQ-CR-8.3 a rule that matches a line through more than one of its rule groups records one match row for that line in that run`` () =
        rolledBack fixture (fun s ->
            result {
                let tag = newTag ()
                let bySource = FieldMatch.Source(pattern "TestCreditCardCo")
                let! rule =
                    s.rule "Two groups" (ClassificationClaimant.Account(s.accountIdOf "F-1280")) 50
                        (groups [ ("And", [ describedAs tag ], None); ("Or", [ bySource ], Some [ describedAs tag ]) ])
                let! entry = s.entry $"Two groups {tag}" "Ingested" false
                let! run = s.accountRun ()
                let! rows = s.rowsOf run.runId
                let line = entry |> debitLineOf |> StageEntryLine.stageEntryLineId
                Assert.Equal(Some(ruleIdOf rule), oneMatchRule (run.classificationResults |> resultFor line).outcome)
                Assert.Single(rows |> rowsOn line) |> ignore
            })

    // =========================================================================
    // REQ-CR-8.5 — reading a run's match rows back
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-8.5 fetching a run's match rows returns every row of that run and none of another run's, each with the rule's name, claimant and priority`` () =
        withCommitted (fun committed rules entries ->
            result {
                let tag = newTag ()
                let! rule, runs =
                    committed.Run(fun s ->
                        result {
                            let! rule = s.accountRule tag "F-1280" 40 tag
                            let! entry = s.entry $"Read back {tag}" "Ingested" false
                            entries.Add(entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                            let candidates = entry |> StageEntryOrchestration.seLines |> List.map (candidateOf entry)
                            let! first = candidates |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context AccountClaimant
                            let! second = candidates |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context AccountClaimant
                            return rule, (first, second)
                        })
                rules.Add(ruleIdOf rule)
                let first, second = runs
                Assert.NotEqual(first.runId, second.runId)
                // every (line, rule) the first run's classifier reported matching, winners, losers and ties alike
                let expectedPairs =
                    first.results
                    |> List.collect (fun r ->
                        let matched =
                            match r.outcome with
                            | ClassifierOutcome.NoMatch -> []
                            | ClassifierOutcome.OneMatch m -> [ m ]
                            | ClassifierOutcome.ManyMatchesClearWinner (winner, losers) -> winner :: losers
                            | ClassifierOutcome.ManyMatchesTied tied -> tied
                        matched
                        |> List.map (fun m ->
                            r.candidate.lineIdOfCandidate |> StageEntryLineId.value, m.ruleId |> ClassificationRuleId.value))
                    |> List.sort
                Assert.Equal(2, expectedPairs.Length)
                let! secondRows = second.runId |> RuleMatch.fetchByRunId (fresh ())
                let secondIds = secondRows |> List.map (RuleMatch.classificationMatchId >> ClassificationMatchId.value)
                Assert.Equal(2, secondIds.Length)
                let! returned = fetchRun (first.runId |> ClassificationRunId.value)
                Assert.Equal<(Guid * Guid) list>(
                    expectedPairs,
                    returned.matches |> List.map (fun m -> m.stageEntryLineId, m.classificationRuleId) |> List.sort)
                Assert.All(secondIds, fun id -> Assert.DoesNotContain(id, returned.matches |> List.map _.ruleMatchId))
                Assert.All(returned.matches, fun m ->
                    Assert.Equal<string>(rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value, m.classificationRuleName)
                    Assert.Equal(40, m.priority)
                    Assert.Equal(
                        Contracts.ClassificationClaimantReturn.Account { code = "F-1280"; accountName = (fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-1280") |> Account.accountName |> AccountName.value) },
                        m.claimantAtMatch))
            })

    [<Fact>]
    member _.``REQ-CR-8.5 fetching a run's match rows after the rule was renamed, re-pointed to another claimant and re-prioritised returns the rule's current name, claimant and priority, not those at run time`` () =
        withCommitted (fun committed rules entries ->
            result {
                let tag = newTag ()
                let! rule, run =
                    committed.Run(fun s ->
                        result {
                            let! rule = s.accountRule tag "F-1280" 40 tag
                            let! entry = s.entry $"Renamed {tag}" "Ingested" false
                            entries.Add(entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                            let! run =
                                [ candidateOf entry (debitLineOf entry) ]
                                |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context AccountClaimant
                            return rule, run
                        })
                rules.Add(ruleIdOf rule)
                let newName = $"Renamed rule {newTag ()}"
                let! _ =
                    committed.Run(fun s ->
                        result {
                            let! name = newName |> ClassificationRuleName.create
                            return!
                                ClassificationOrchestration.updateClassificationRule s.Context
                                    (FieldUpdate.SetTo name) (FieldUpdate.SetTo(ClassificationClaimant.Account(s.accountIdOf "F-1290")))
                                    (FieldUpdate.SetTo 7) FieldUpdate.NoChange FieldUpdate.NoChange (ruleIdOf rule)
                        })
                let! returned = fetchRun (run.runId |> ClassificationRunId.value)
                let only = Assert.Single(returned.matches)
                let f1290 = fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-1290")
                Assert.Equal<string>(newName, only.classificationRuleName)
                Assert.Equal(7, only.priority)
                Assert.Equal(
                    Contracts.ClassificationClaimantReturn.Account { code = "F-1290"; accountName = (f1290 |> Account.accountName |> AccountName.value) },
                    only.claimantAtMatch)
            })

    [<Fact>]
    member _.``REQ-CR-8.5 a run's match rows come back grouped by staged line, rows on the same line ordered by priority, and rows sharing line and priority ordered by rule name`` () =
        withCommitted (fun committed rules entries ->
            result {
                let tag = newTag ()
                let! made, run =
                    committed.Run(fun s ->
                        result {
                            // created in an order that is neither the priority order nor the name order
                            let! b = s.rule $"b {tag}" (ClassificationClaimant.Account(s.accountIdOf "F-1280")) 10 (groups [ ("And", [ describedAs tag ], None) ])
                            let! c = s.rule $"c {tag}" (ClassificationClaimant.Account(s.accountIdOf "F-1280")) 5 (groups [ ("And", [ describedAs tag ], None) ])
                            let! a = s.rule $"a {tag}" (ClassificationClaimant.Account(s.accountIdOf "F-1280")) 10 (groups [ ("And", [ describedAs tag ], None) ])
                            let! entry = s.entry $"Ordered {tag}" "Ingested" false
                            entries.Add(entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                            let! run =
                                entry |> StageEntryOrchestration.seLines |> List.map (candidateOf entry)
                                |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches s.Context AccountClaimant
                            return [ a; b; c ], run
                        })
                made |> List.iter (ruleIdOf >> rules.Add)
                let! returned = fetchRun (run.runId |> ClassificationRunId.value)
                // the order between lines is unspecified, so the rows are cut into runs of one line each: there must be
                // exactly one run per line, and each run carries c (5), then a and b (10, by name)
                let runsOfOneLine =
                    returned.matches
                    |> List.fold (fun acc m ->
                        match acc with
                        | (lineId, rows) :: rest when lineId = m.stageEntryLineId -> (lineId, rows @ [ m ]) :: rest
                        | _ -> (m.stageEntryLineId, [ m ]) :: acc) []
                    |> List.rev
                let expectedLines = run.results |> List.map (fun r -> r.candidate.lineIdOfCandidate |> StageEntryLineId.value)
                Assert.Equal<Set<Guid>>(expectedLines |> Set.ofList, runsOfOneLine |> List.map fst |> Set.ofList)
                Assert.Equal(expectedLines.Length, runsOfOneLine.Length)
                let nameOf (rule: ClassificationRule.ClassificationRule) =
                    rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value
                let a, b, c = made[0], made[1], made[2]
                Assert.All(runsOfOneLine, fun (_, rows) ->
                    Assert.Equal<(int * string) list>(
                        [ (5, c |> nameOf); (10, a |> nameOf); (10, b |> nameOf) ],
                        rows |> List.map (fun m -> m.priority, m.classificationRuleName)))
            })

    [<Fact>]
    member _.``REQ-CR-8.5 fetching the match rows of a run ID that has none returns an empty list`` () =
        let runId = Guid.NewGuid()
        let returned = fetchRun runId
        match returned with
        | Ok run ->
            Assert.Equal(runId, run.runId)
            Assert.Empty(run.matches)
        | Error e -> Assert.Fail(e.ToMessage())
