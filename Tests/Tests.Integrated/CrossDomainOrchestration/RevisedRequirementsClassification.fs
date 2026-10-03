module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsClassification

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.FieldUpdate
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
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowError
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

(* Plan item 30: the clauses of revised Classification Rule requirements that no earlier test reached.
   The rule tests go through the Classification routes, which commit. Each makes its own payment agreements (Outgo,
   one leg on F-2230 and F-1280) and rules, with a fresh tag in every name, and a finally deletes the rules and then the
   agreements. The field match and classifier tests need no database, apart from the REQ-CR-3.4 rule, which is made in
   a transaction that rolls back. *)

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private send (verb: string) (payload: 'a) = payload |> Json.toJson |> Result.bind (routeUiCommandForTesting "Classification" verb [])

let private describedAsGroups (pattern: string) : ClassificationRuleGroupContract list =
    [ { connector = "And"; chainOne = { chain = [ FieldMatchContract.Description pattern ] }; chainTwo = None } ]

let private newRuleInput (name: string) (claimant: ClassificationClaimantInput) (groups: ClassificationRuleGroupContract list) : NewClassificationRuleInput =
    { classificationRuleName = name; claimantAtMatch = claimant; priority = 100; ruleGroups = groups }

let private noFilter : ClassificationRuleFilterInput =
    { ruleId = None
      nameLike = None
      accountCodeAtMatch = None
      paymentAgreementNameAtMatch = None
      claimantType = None
      sourceLike = None
      activeOnly = false }

let private fetchFiltered (filter: ClassificationRuleFilterInput) =
    ({ filter = filter; sort = None } : FetchClassificationRuleFilteredInput)
    |> send "FetchClassificationRuleFiltered"
    |> Result.bind Json.fromJson<ClassificationRuleReturn list>

let private fetchRuleById (id: Guid) =
    ({ classificationRuleId = id } : FetchClassificationRuleByIdInput)
    |> send "FetchClassificationRuleById"
    |> Result.bind Json.fromJson<ClassificationRuleReturn>

let private ruleIds (rules: ClassificationRuleReturn list) = rules |> List.map (fun r -> r.classificationRuleId) |> Set.ofList

/// The claimant a rule holds, read from the database through the model (which refuses a row holding both or neither).
let private storedClaimant (id: Guid) =
    id |> ClassificationRuleId.fromGuid |> ClassificationRule.fetchById (Context.create NoTransaction FetchOnly)
    |> Result.map ClassificationRule.classificationClaimant

type private Committed(fixture: TestDataFixture) =
    let rules = ResizeArray<Guid>()
    let agreements = ResizeArray<Guid>()
    let idOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    member _.accountIdOf code = idOf code

    member _.accountName code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountName |> AccountName.value

    /// Commits an Outgo agreement with one leg named after the tag. Returns the leg's name and ID.
    member _.leg (tag: string) =
        runCommandRouteAndAutoCompleteTransaction ClassificationNewRule (fun context ->
            result {
                let name = $"Revised classification {tag} {Guid.NewGuid():N}"
                let! agreementName = name |> AgreementName.create
                let! first = 1 |> Cadence.DateInMonthNumber.fromInt
                let! counterparty = "Revised classification counterparty" |> Counterparty.create
                let! activityPeriod =
                    ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
                let legName = $"{name} leg"
                let! paName = legName |> PaymentAgreementName.create
                let! agreement =
                    AgreementOrchestration.constructNewAndPersist
                        context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first)) { nextInstance = LocalDate(2049, 3, 1) }
                        counterparty activityPeriod None
                        [ (paName, DebitAccount.create(idOf "F-2230"), CreditAccount.create(idOf "F-1280"), None, None, None) ]
                agreements.Add(agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID |> MasterAgreementId.value)
                let legId = agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
                return legName, legId
            })
        |> orFail

    /// Creates a rule through the route and records it for deletion.
    member _.rule (input: NewClassificationRuleInput) =
        input |> send "NewClassificationRule" |> Result.bind Json.fromJson<ClassificationRuleReturn>
        |> Result.map (fun r -> rules.Add r.classificationRuleId; r)

    member _.CleanUp () =
        [ for id in rules -> id |> ClassificationRuleId.fromGuid |> Some |> Cleanup.cleanUpClassificationRuleId
          for id in agreements -> Cleanup.cleanUpMasterAgreementTree (Some id) ]
        |> List.iter orFail

let private withCommitted (fixture: TestDataFixture) (test: Committed -> Result<unit, IAppError>) =
    let committed = Committed(fixture)
    try
        test committed |> railroadWrapper
    finally
        committed.CleanUp ()

/// A candidate from TestCreditCardCo with the description and memo, for evaluating field matches directly.
let private candidate (source: string) (description: string) (memo: string) : MatchCandidate =
    { headerIdOfCandidate = StageEntryHeaderId.fromGuid (Guid.NewGuid())
      lineIdOfCandidate = StageEntryLineId.fromGuid (Guid.NewGuid())
      ingestionSource = source |> JournalRefFinancialInstitution.create |> orFail
      description = description |> JournalEntryDescription.create |> orFail
      amount = 100.00M |> Money.fromDecimal |> orFail
      lineType = Debit
      memo = memo |> JournalEntryLineMemo.create |> orFail |> Some }

let private fieldMatch (field: string) (pattern: string) =
    let p = pattern |> StringSearchPattern.create |> orFail
    match field with
    | "Source" -> FieldMatch.Source p
    | _ -> FieldMatch.Description p

/// A candidate whose one field under test holds the value; the other two hold text no pattern here matches.
let private candidateWith (field: string) (value: string) =
    match field with
    | "Source" -> candidate value "unrelated description" "unrelated memo"
    | _ -> candidate "UnrelatedSource" value "unrelated memo"

[<Collection("SharedTestData")>]
type RevisedRequirementsClassificationTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CR-1.5, 4.3 — a payment agreement claimant given by name
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-1.5 REQ-CR-4.3 creating a classification rule whose claimant is a payment agreement given by name stores the rule claiming that payment agreement and no account`` () =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let legName, legId = c.leg tag
                let! created = c.rule (newRuleInput $"PA claimant {tag}" (ClassificationClaimantInput.PaymentAgreement legName) (describedAsGroups tag))
                Assert.Equal(ClassificationClaimantReturn.PaymentAgreement legName, created.claimantAtMatch)
                let! stored = storedClaimant created.classificationRuleId
                Assert.Equal(ClassificationClaimant.PaymentAgreement legId, stored)
            })

    [<Fact>]
    member _.``REQ-CR-1.5 REQ-CR-4.3 creating a classification rule whose payment agreement claimant name matches no payment agreement fails with a typed error naming the name, and no rule is stored`` () =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let missing = $"No such payment agreement {tag}"
                let attempt = c.rule (newRuleInput $"Missing PA {tag}" (ClassificationClaimantInput.PaymentAgreement missing) (describedAsGroups tag))
                let refused =
                    match attempt with
                    | Error (AsError (CashflowPaymentAgreementNameDoesntMatchId name)) -> name = missing
                    | _ -> false
                Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                let! named = fetchFiltered { noFilter with nameLike = Some tag }
                Assert.Empty(named)
            })

    // =========================================================================
    // REQ-CR-1.14 — case-sensitive, unanchored matching
    // =========================================================================

    [<Theory>]
    [<InlineData("Source")>]
    [<InlineData("Description")>]
    member _.``REQ-CR-1.14 for each of the Source and Description field matches, a pattern matches the same text in its own case and does not match it in the other case`` (field: string) =
        let pattern = fieldMatch field "CaseText"
        Assert.True(FieldMatch.doesMatch (candidateWith field "CaseText") pattern)
        Assert.False(FieldMatch.doesMatch (candidateWith field "casetext") pattern)
        Assert.False(FieldMatch.doesMatch (candidateWith field "CASETEXT") pattern)

    [<Theory>]
    [<InlineData("Source")>]
    [<InlineData("Description")>]
    member _.``REQ-CR-1.14 for each of the Source and Description field matches, a pattern is satisfied by a value containing it mid-string, and the same pattern anchored with ^ and $ is not`` (field: string) =
        let value = "Before Needle After"
        Assert.True(FieldMatch.doesMatch (candidateWith field value) (fieldMatch field "Needle"))
        Assert.False(FieldMatch.doesMatch (candidateWith field value) (fieldMatch field "^Needle$"))

    // =========================================================================
    // REQ-CR-3.4 — OneMatch carries a payment agreement claimant
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-3.4 when exactly one active rule matches and its claimant is a payment agreement, the outcome is OneMatch carrying that payment agreement's ID, the rule's ID and its priority`` () =
        runCommandRouteAndAutoRollback ClassifyAccounts (fun context ->
            result {
                let tag = newTag ()
                let accountIdOf code =
                    fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
                let! agreementName = $"OneMatch {tag}" |> AgreementName.create
                let! first = 1 |> Cadence.DateInMonthNumber.fromInt
                let! counterparty = "OneMatch counterparty" |> Counterparty.create
                let! activityPeriod =
                    ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
                let! paName = $"OneMatch {tag} leg" |> PaymentAgreementName.create
                let! agreement =
                    AgreementOrchestration.constructNewAndPersist
                        context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first)) { nextInstance = LocalDate(2049, 3, 1) }
                        counterparty activityPeriod None
                        [ (paName, DebitAccount.create(accountIdOf "F-2230"), CreditAccount.create(accountIdOf "F-1280"), None, None, None) ]
                let legId = agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
                let groupsFor (text: string) =
                    [ ("And", [ fieldMatch "Description" text ], None) ] |> createClassificationRuleGroupListForTest |> orFail
                let! ruleName = $"OneMatch rule {tag}" |> ClassificationRuleName.create
                let! rule =
                    ClassificationOrchestration.constructNewAndPersist context ruleName (ClassificationClaimant.PaymentAgreement legId) 37 (groupsFor tag)
                let! otherName = $"Unmatched rule {tag}" |> ClassificationRuleName.create
                let! other =
                    ClassificationOrchestration.constructNewAndPersist context otherName (ClassificationClaimant.Account(accountIdOf "F-2230")) 1 (groupsFor $"elsewhere {tag}")
                let! outcome = Classifier.classifyCandidate [ rule; other ] (candidateWith "Description" $"Paid {tag}")
                let expectedMatch : PrioritizedMatch =
                    { accountId = None
                      paymentAgreementId = Some legId
                      ruleId = rule |> ClassificationRule.classificationRuleId
                      priority = 37 }
                let expected = ClassifierOutcome.OneMatch expectedMatch
                Assert.Equal(expected, outcome.outcome)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CR-5.3 — filters
    // =========================================================================

    [<Theory>]
    [<InlineData("payment agreement claimant")>]
    [<InlineData("claimant type AccountClaimant")>]
    [<InlineData("claimant type PaymentAgreementClaimant")>]
    member _.``REQ-CR-5.3 for each of the payment agreement claimant filter and the claimant type filter (account, payment agreement), fetching rules returns every rule that meets it and no rule that doesn't`` (filterCase: string) =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let legXName, _ = c.leg $"{tag} X"
                let legYName, _ = c.leg $"{tag} Y"
                let! onX = c.rule (newRuleInput $"On X {tag}" (ClassificationClaimantInput.PaymentAgreement legXName) (describedAsGroups tag))
                let! onY = c.rule (newRuleInput $"On Y {tag}" (ClassificationClaimantInput.PaymentAgreement legYName) (describedAsGroups tag))
                let! onAccount = c.rule (newRuleInput $"On account {tag}" (ClassificationClaimantInput.Account "F-1280") (describedAsGroups tag))
                let mine = set [ onX.classificationRuleId; onY.classificationRuleId; onAccount.classificationRuleId ]
                match filterCase with
                | "payment agreement claimant" ->
                    let! found = fetchFiltered { noFilter with paymentAgreementNameAtMatch = Some legXName }
                    Assert.Equal<Set<Guid>>(set [ onX.classificationRuleId ], ruleIds found)
                | "claimant type AccountClaimant" ->
                    let! found = fetchFiltered { noFilter with claimantType = Some "AccountClaimant" }
                    Assert.Equal<Set<Guid>>(set [ onAccount.classificationRuleId ], Set.intersect mine (ruleIds found))
                    Assert.All(found, fun r -> Assert.True(r.claimantAtMatch.IsAccount, $"{r.classificationRuleName}"))
                | _ ->
                    let! found = fetchFiltered { noFilter with claimantType = Some "PaymentAgreementClaimant" }
                    Assert.Equal<Set<Guid>>(set [ onX.classificationRuleId; onY.classificationRuleId ], Set.intersect mine (ruleIds found))
                    Assert.All(found, fun r -> Assert.True(r.claimantAtMatch.IsPaymentAgreement, $"{r.classificationRuleName}"))
            })

    [<Theory>]
    [<InlineData("part of the name")>]
    [<InlineData("the name in the wrong case")>]
    member _.``REQ-CR-5.6 the payment agreement claimant filter given part of an agreement's name, or the name in the wrong case, fails with a typed error`` (given: string) =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let legName, _ = c.leg tag
                let! _ = c.rule (newRuleInput $"Exact {tag}" (ClassificationClaimantInput.PaymentAgreement legName) (describedAsGroups tag))
                (* The full name resolves, so only the change to it can make the filter fail. *)
                let! exact = fetchFiltered { noFilter with paymentAgreementNameAtMatch = Some legName }
                Assert.NotEmpty(exact)
                let value = if given = "part of the name" then legName.Substring(0, legName.Length - 4) else legName.ToUpperInvariant()
                let attempt = fetchFiltered { noFilter with paymentAgreementNameAtMatch = Some value }
                let namesIt =
                    match attempt with
                    | Error (AsError (CashflowPaymentAgreementNameDoesntMatchId name)) -> name = value
                    | _ -> false
                Assert.True(namesIt, $"%A{attempt |> Result.map List.length |> Result.mapError (fun e -> e.ToMessage())}")
            })

    [<Theory>]
    [<InlineData("name")>]
    [<InlineData("source pattern")>]
    member _.``REQ-CR-5.3 for each of the name filter and the source pattern filter, a partial value in the wrong case matches no rule and the same partial value in the right case matches the rule`` (filterCase: string) =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let marker = $"MiXeD{tag}"
                let groups : ClassificationRuleGroupContract list =
                    [ { connector = "And"
                        chainOne = { chain = [ FieldMatchContract.Source "TestCreditCardCo"; FieldMatchContract.Source $"Lead {marker} Tail" ] }
                        chainTwo = None } ]
                let! rule = c.rule (newRuleInput $"Rule {marker} named" (ClassificationClaimantInput.Account "F-1280") groups)
                let filterWith (value: string) =
                    if filterCase = "name" then { noFilter with nameLike = Some value } else { noFilter with sourceLike = Some value }
                let! wrongCase = fetchFiltered (filterWith (marker.ToLowerInvariant()))
                let! rightCase = fetchFiltered (filterWith marker)
                Assert.Empty(wrongCase)
                Assert.Equal<Set<Guid>>(set [ rule.classificationRuleId ], ruleIds rightCase)
            })

    // =========================================================================
    // REQ-CR-5.5 — claimants in human-readable form
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-5.5 a fetched rule identifies an account claimant by the account's code and name, and a payment agreement claimant by the payment agreement's name`` () =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let legName, _ = c.leg tag
                let! accountRule = c.rule (newRuleInput $"Account readable {tag}" (ClassificationClaimantInput.Account "F-1280") (describedAsGroups tag))
                let! paRule = c.rule (newRuleInput $"PA readable {tag}" (ClassificationClaimantInput.PaymentAgreement legName) (describedAsGroups tag))
                let! accountRead = fetchRuleById accountRule.classificationRuleId
                let! paRead = fetchRuleById paRule.classificationRuleId
                Assert.Equal(ClassificationClaimantReturn.Account { code = "F-1280"; accountName = c.accountName "F-1280" }, accountRead.claimantAtMatch)
                Assert.Equal(ClassificationClaimantReturn.PaymentAgreement legName, paRead.claimantAtMatch)
            })

    // =========================================================================
    // REQ-CR-6.1, 6.3 — updating the claimant
    // =========================================================================

    [<Theory>]
    [<InlineData("account to payment agreement")>]
    [<InlineData("payment agreement to account")>]
    member _.``REQ-CR-6.1 for each claimant switch (account to payment agreement, payment agreement to account), updating a rule's claimant stores the new claimant and clears the old one, leaving exactly one`` (switch: string) =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let legName, legId = c.leg tag
                let fromClaimant, toClaimant, expected =
                    if switch = "account to payment agreement" then
                        ClassificationClaimantInput.Account "F-1280", ClassificationClaimantInput.PaymentAgreement legName, ClassificationClaimant.PaymentAgreement legId
                    else
                        ClassificationClaimantInput.PaymentAgreement legName, ClassificationClaimantInput.Account "F-1280", ClassificationClaimant.Account(c.accountIdOf "F-1280")
                let! rule = c.rule (newRuleInput $"Switch {tag}" fromClaimant (describedAsGroups tag))
                let! _ =
                    ({ classificationRuleId = rule.classificationRuleId
                       classificationRuleNameUpdate = NoChange
                       claimantAtMatchUpdate = SetTo toClaimant
                       priorityUpdate = NoChange
                       ruleGroupsUpdate = NoChange
                       isActiveUpdate = NoChange } : UpdateClassificationRuleInput)
                    |> send "UpdateClassificationRule"
                let! stored = storedClaimant rule.classificationRuleId
                Assert.Equal(expected, stored)
            })

    [<Fact>]
    member _.``REQ-CR-6.3 updating a rule's claimant to a payment agreement name that matches no payment agreement fails with a typed error naming the name, and the rule keeps its claimant`` () =
        withCommitted fixture (fun c ->
            result {
                let tag = newTag ()
                let missing = $"No such payment agreement {tag}"
                let! rule = c.rule (newRuleInput $"Keeps claimant {tag}" (ClassificationClaimantInput.Account "F-1280") (describedAsGroups tag))
                let attempt =
                    ({ classificationRuleId = rule.classificationRuleId
                       classificationRuleNameUpdate = NoChange
                       claimantAtMatchUpdate = SetTo(ClassificationClaimantInput.PaymentAgreement missing)
                       priorityUpdate = NoChange
                       ruleGroupsUpdate = NoChange
                       isActiveUpdate = NoChange } : UpdateClassificationRuleInput)
                    |> send "UpdateClassificationRule"
                let refused =
                    match attempt with
                    | Error (AsError (CashflowPaymentAgreementNameDoesntMatchId name)) -> name = missing
                    | _ -> false
                Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                let! stored = storedClaimant rule.classificationRuleId
                Assert.Equal(ClassificationClaimant.Account(c.accountIdOf "F-1280"), stored)
            })
