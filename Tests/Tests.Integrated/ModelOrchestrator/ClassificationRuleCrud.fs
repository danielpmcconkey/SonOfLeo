module Tests.Integrated.ModelOrchestrator.ClassificationRuleCrud

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion.Classification
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator
open ModelOrchestrator.FetchFilters
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.SadPath
open Utilities.AppError
open Utilities.ResultHelper
open Utilities.FieldUpdate
open Xunit


let private unwrap result =
    result |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

let private ruleNameOf s = s |> ClassificationRuleName.create |> unwrap
let private codeOf s = s |> AccountCode.create |> unwrap
let private patternOf s = s |> StringSearchPattern.create |> unwrap

let private chainOf matches = FieldMatchChain.create matches
let private groupOf matches = ClassificationRuleGroup.create And (chainOf matches) None

/// A filter that constrains nothing. activeOnly is false, so inactive rules come back too.
let private noFilter =
    { ruleId = None
      nameLike = None
      codeAtMatch = None
      sourceLike = None
      activeOnly = false }

let private nameOf (r: ClassificationRule.ClassificationRule) =
    r |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value

let private codeStrOf (r: ClassificationRule.ClassificationRule) =
    r |> ClassificationRule.codeAtMatch |> AccountCode.value

let private idOf (r: ClassificationRule.ClassificationRule) =
    r |> ClassificationRule.classificationRuleId

let private namesOf rules = rules |> List.map nameOf |> List.sort


[<Collection("SharedTestData")>]
type ClassificationRuleCrudTests(fixture: TestDataFixture) =

    // Fixture rule names, so expectations are derived from known fixture data rather than
    // from a second call to the thing under test.
    let fixtureRules () = fixture.Data.classificationRules

    // =========================================================================
    // Create
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-4.1 REQ-CR-4.5 create returns the new rule bearing an id, a created_at and modified_at that are populated and equal, and the name, code, priority, and rule groups it was given`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let groups = [ groupOf [ Source(patternOf "TestReturnShape") ] ]
                let! created =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf "CR-4.1 return shape")
                        (codeOf "F-5350")
                        777
                        groups
                Assert.Equal("CR-4.1 return shape", created |> nameOf)
                Assert.Equal("F-5350", created |> codeStrOf)
                Assert.Equal(777, created |> ClassificationRule.priority)
                Assert.Equal<ClassificationRuleGroup list>(groups, created |> ClassificationRule.ruleGroups)
                Assert.NotEqual(System.Guid.Empty, created |> idOf |> ClassificationRuleId.value)
                Assert.Equal(
                    created |> ClassificationRule.createdAt,
                    created |> ClassificationRule.modifiedAt)
                Assert.NotEqual(NodaTime.Instant.MinValue, created |> ClassificationRule.createdAt)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-4.5 a created rule fetched back from the database carries the same rule groups, field matches, and amount patterns it was created with`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                // Two groups, a chainTwo, and an Amount match: the round trip has to survive
                // JSONB serialisation and reconstitution, not just a flat string compare.
                let! amount = Money.fromDecimal 412.75M
                let groups =
                    [ ClassificationRuleGroup.create
                        And
                        (chainOf [ Source(patternOf "TestRoundTrip"); LineType(Model.Ledger.Journaling.JournalEntryComponent.JournalEntryLineType.Debit) ])
                        None
                      ClassificationRuleGroup.create
                        Or
                        (chainOf [ Description(patternOf "^ROUNDTRIP") ])
                        (Some(chainOf [ Amount({ numericSearchOperator = GreaterThanOrEqualTo; amount = amount }) ])) ]
                let! created =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf "CR-4.5 round trip")
                        (codeOf "F-5650")
                        778
                        groups
                let! fetched = created |> idOf |> ClassificationRule.fetchById context
                Assert.Equal<ClassificationRuleGroup list>(groups, fetched |> ClassificationRule.ruleGroups)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-4.4 a newly created rule is active, both in the value create returns and in the row fetched back`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let! created =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf "CR-4.4 always active")
                        (codeOf "F-5350")
                        779
                        [ groupOf [ Source(patternOf "TestAlwaysActive") ] ]
                Assert.True(created |> ClassificationRule.isActive)
                let! fetched = created |> idOf |> ClassificationRule.fetchById context
                Assert.True(fetched |> ClassificationRule.isActive)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-4.3 REQ-CR-1.5 create returns an account-not-found error when codeAtMatch doesn't match an account code in the ledger`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            // Rule groups are valid, because confirmRuleGroups runs first and would otherwise
            // return its own error before the code is ever looked at.
            ClassificationOrchestration.createNewClassificationRule
                context
                (ruleNameOf "CR-4.3 bogus code")
                (codeOf "F-9999")
                780
                [ groupOf [ Source(patternOf "TestBogusCode") ] ]
            |> fun r -> isCorrectError r AccountCodeDoesntMatchAccountId None)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-4.6 REQ-CR-1.7 create returns a validation error when the rule groups list is empty`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            ClassificationOrchestration.createNewClassificationRule
                context
                (ruleNameOf "CR-4.6 no groups")
                (codeOf "F-5350")
                781
                []
            |> fun r -> isCorrectErrorEmpty r IngestionClassificationRuleGroupsEmpty None)
        |> railroadWrapper

    // Position 0: the only group's chainOne. Position 1: the second group's chainOne — an
    // implementation checking only the head of the list passes this one. Position 2: a chainTwo,
    // which is optional and easy to skip entirely.
    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(1)>]
    [<InlineData(2)>]
    member _.``REQ-CR-4.7 REQ-CR-1.12 create returns a validation error when a field match chain is empty`` (emptyPosition: int) =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            let populated = chainOf [ Source(patternOf "TestEmptyChain") ]
            let empty = chainOf []
            let groups =
                match emptyPosition with
                | 0 -> [ ClassificationRuleGroup.create And empty None ]
                | 1 ->
                    [ ClassificationRuleGroup.create And populated None
                      ClassificationRuleGroup.create And empty None ]
                | _ -> [ ClassificationRuleGroup.create Or populated (Some empty) ]
            ClassificationOrchestration.createNewClassificationRule
                context
                (ruleNameOf $"CR-4.7 empty chain at {emptyPosition}")
                (codeOf "F-5350")
                782
                groups
            |> fun r -> isCorrectErrorEmpty r IngestionFieldMatchChainEmpty None)
        |> railroadWrapper

    // =========================================================================
    // Read
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-5.1 fetch by id returns the one rule bearing that id with its rule groups, field matches, and amount patterns intact`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let expected =
                    fixtureRules ()
                    |> List.find (fun r -> r |> nameOf = "TestArchiveBank two-group rule then 5650")
                let! fetched = expected |> idOf |> ClassificationRule.fetchById context
                Assert.Equal(expected |> idOf, fetched |> idOf)
                Assert.Equal(expected |> nameOf, fetched |> nameOf)
                Assert.Equal<ClassificationRuleGroup list>(
                    expected |> ClassificationRule.ruleGroups,
                    fetched |> ClassificationRule.ruleGroups)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.2 fetch by name returns the rule whose name matches exactly and not one whose name merely contains it`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let target =
                    fixtureRules ()
                    |> List.find (fun r -> r |> nameOf = "Source = TestBank then 5300")
                // A strict superstring of the target's name. An exact-match fetch must ignore it;
                // a LIKE would return two rows and the fetch would fail outright.
                let! _ =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf "Source = TestBank then 5300 EXTENDED")
                        (codeOf "F-5350")
                        783
                        [ groupOf [ Source(patternOf "TestSuperstring") ] ]
                let! fetched = ruleNameOf "Source = TestBank then 5300" |> ClassificationRule.fetchByName context
                Assert.Equal(target |> idOf, fetched |> idOf)
                Assert.Equal("Source = TestBank then 5300", fetched |> nameOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered by id returns exactly the one rule bearing that id`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let target = fixtureRules () |> List.find (fun r -> r |> nameOf = "Allstate Insurance to 5300")
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with ruleId = Some(target |> idOf) }
                        None
                Assert.Equal(1, found |> List.length)
                Assert.Equal(target |> idOf, found |> List.exactlyOne |> idOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered by name fragment returns every rule whose name contains the fragment and no others`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let fragment = "Allstate"
                let expected =
                    fixtureRules ()
                    |> List.filter (fun r -> (r |> nameOf).Contains fragment)
                    |> namesOf
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with nameLike = Some(ruleNameOf fragment) }
                        None
                // The fragment must select some rules and exclude some, or an implementation
                // that ignores the filter entirely would satisfy the comparison below.
                Assert.NotEmpty(expected)
                Assert.NotEqual<string list>(fixtureRules () |> namesOf, expected)
                Assert.Equal<string list>(expected, found |> namesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered by codeAtMatch returns every rule assigned that exact code and none assigned a code merely containing it`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let expected =
                    fixtureRules ()
                    |> List.filter (fun r -> r |> codeStrOf = "F-5650")
                    |> namesOf
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with codeAtMatch = Some(codeOf "F-5650") }
                        None
                Assert.NotEmpty(expected)
                Assert.NotEqual<string list>(fixtureRules () |> namesOf, expected)
                Assert.Equal<string list>(expected, found |> namesOf)
                Assert.DoesNotContain("F-5350", found |> List.map codeStrOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered by source pattern fragment returns the rules whose rule group bodies carry that pattern and no others`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with sourceLike = Some "TestSplitBank" }
                        None
                let expected =
                    [ "Source = TestSplitBank && Credit then 5650"
                      "Source = TestSplitBank && Debit then 5350" ]
                Assert.Equal<string list>(expected, found |> namesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered with activeOnly true omits the inactive rule that its other filters would otherwise have returned`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let inactiveName = "INACTIVE Source = TestSavings then 5300"
                let! withInactive =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with codeAtMatch = Some(codeOf "F-5300") }
                        None
                // The filter really does select it when activeOnly is off, so the omission below
                // is the active filter working rather than the code filter missing it.
                Assert.Contains(inactiveName, withInactive |> namesOf)
                let! activeOnly =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with codeAtMatch = Some(codeOf "F-5300"); activeOnly = true }
                        None
                Assert.DoesNotContain(inactiveName, activeOnly |> namesOf)
                Assert.Equal((withInactive |> List.length) - 1, activeOnly |> List.length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered with activeOnly false returns that inactive rule alongside the active ones its other filters match`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let expected =
                    fixtureRules ()
                    |> List.filter (fun r -> r |> codeStrOf = "F-5300")
                    |> namesOf
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with codeAtMatch = Some(codeOf "F-5300") }
                        None
                Assert.NotEmpty(expected)
                Assert.NotEqual<string list>(fixtureRules () |> namesOf, expected)
                Assert.Equal<string list>(expected, found |> namesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered given both a name fragment and a code returns only the rules satisfying both, not the union`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let fragment = "Allstate"
                let code = "F-5650"
                let both =
                    fixtureRules ()
                    |> List.filter (fun r -> (r |> nameOf).Contains fragment && r |> codeStrOf = code)
                    |> namesOf
                let union =
                    fixtureRules ()
                    |> List.filter (fun r -> (r |> nameOf).Contains fragment || r |> codeStrOf = code)
                    |> namesOf
                // The two sets must differ or the assertion below proves nothing.
                Assert.NotEqual<string list>(both, union)
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with nameLike = Some(ruleNameOf fragment); codeAtMatch = Some(codeOf code) }
                        None
                Assert.NotEmpty(both)
                Assert.Equal<string list>(both, found |> namesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered with every filter omitted returns every rule in the table`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let! found = ClassificationOrchestration.fetchRulesFiltered context noFilter None
                Assert.Equal<string list>(fixtureRules () |> namesOf, found |> namesOf)
            })
        |> railroadWrapper

    // =========================================================================
    // Update
    //
    // The target is the TestArchiveBank rule: it matches no staged data, so mutating it cannot
    // perturb classification even before the transaction rolls back. Each test asserts the one
    // field it set and the four it did not, so a SET clause that touched extra columns fails.
    // =========================================================================

    member private _.TwoGroupRule() =
        fixture.Data.classificationRules
        |> List.find (fun r -> r |> nameOf = "TestArchiveBank two-group rule then 5650")

    member private this.AssertOnlyChangedField
        (updated: ClassificationRule.ClassificationRule)
        (original: ClassificationRule.ClassificationRule)
        (changed: string)
        =
        if changed <> "name" then Assert.Equal(original |> nameOf, updated |> nameOf)
        if changed <> "code" then Assert.Equal(original |> codeStrOf, updated |> codeStrOf)
        if changed <> "priority" then
            Assert.Equal(original |> ClassificationRule.priority, updated |> ClassificationRule.priority)
        if changed <> "groups" then
            Assert.Equal<ClassificationRuleGroup list>(
                original |> ClassificationRule.ruleGroups,
                updated |> ClassificationRule.ruleGroups)
        if changed <> "isActive" then
            Assert.Equal(original |> ClassificationRule.isActive, updated |> ClassificationRule.isActive)

    [<Fact>]
    member this.``REQ-CR-6.1 updating name with SetTo changes the name and leaves code, priority, rule groups, and isActive as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = this.TwoGroupRule()
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        (SetTo(ruleNameOf "CR-6.1 renamed"))
                        NoChange NoChange NoChange NoChange
                        (original |> idOf)
                Assert.Equal("CR-6.1 renamed", updated |> nameOf)
                Assert.NotEqual<string>(original |> nameOf, updated |> nameOf)
                this.AssertOnlyChangedField updated original "name"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.1 updating codeAtMatch with SetTo changes the code and leaves name, priority, rule groups, and isActive as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = this.TwoGroupRule()
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        NoChange (SetTo(codeOf "F-5350")) NoChange NoChange NoChange
                        (original |> idOf)
                Assert.Equal("F-5350", updated |> codeStrOf)
                Assert.NotEqual<string>(original |> codeStrOf, updated |> codeStrOf)
                this.AssertOnlyChangedField updated original "code"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.1 updating priority with SetTo changes the priority and leaves name, code, rule groups, and isActive as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = this.TwoGroupRule()
                let newPriority = (original |> ClassificationRule.priority) + 11
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        NoChange NoChange (SetTo newPriority) NoChange NoChange
                        (original |> idOf)
                Assert.Equal(newPriority, updated |> ClassificationRule.priority)
                this.AssertOnlyChangedField updated original "priority"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.1 updating ruleGroups with SetTo leaves exactly the new rule groups with none of the old surviving, and leaves name, code, priority, and isActive as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = this.TwoGroupRule()
                let replacement = [ groupOf [ Source(patternOf "TestReplacedBody") ] ]
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        NoChange NoChange NoChange (SetTo replacement) NoChange
                        (original |> idOf)
                // Equality against the replacement is what forbids a merge or an append: the
                // original had two groups, so a body that kept them would not compare equal.
                Assert.Equal<ClassificationRuleGroup list>(replacement, updated |> ClassificationRule.ruleGroups)
                Assert.Equal(2, original |> ClassificationRule.ruleGroups |> List.length)
                Assert.Equal(1, updated |> ClassificationRule.ruleGroups |> List.length)
                this.AssertOnlyChangedField updated original "groups"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.1 updating isActive with SetTo false deactivates the rule and leaves name, code, priority, and rule groups as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = this.TwoGroupRule()
                Assert.True(original |> ClassificationRule.isActive)
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        NoChange NoChange NoChange NoChange (SetTo false)
                        (original |> idOf)
                Assert.False(updated |> ClassificationRule.isActive)
                this.AssertOnlyChangedField updated original "isActive"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.1 updating isActive with SetTo true reactivates the inactive rule and leaves name, code, priority, and rule groups as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original =
                    fixture.Data.classificationRules
                    |> List.find (fun r -> r |> nameOf = "INACTIVE Source = TestSavings then 5300")
                Assert.False(original |> ClassificationRule.isActive)
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        NoChange NoChange NoChange NoChange (SetTo true)
                        (original |> idOf)
                Assert.True(updated |> ClassificationRule.isActive)
                this.AssertOnlyChangedField updated original "isActive"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.2 an update with all five fields NoChange is rejected and leaves the stored rule, modified_at included, untouched``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let ruleId = this.TwoGroupRule() |> idOf
                let! before = ruleId |> ClassificationRule.fetchById context
                do!
                    ClassificationOrchestration.updateClassificationRule
                        context NoChange NoChange NoChange NoChange NoChange ruleId
                    |> fun r -> isCorrectErrorEmpty r IngestionClassificationRuleUpdateNoOp None
                let! after = ruleId |> ClassificationRule.fetchById context
                Assert.Equal(before |> nameOf, after |> nameOf)
                Assert.Equal(before |> codeStrOf, after |> codeStrOf)
                Assert.Equal(before |> ClassificationRule.priority, after |> ClassificationRule.priority)
                Assert.Equal(before |> ClassificationRule.isActive, after |> ClassificationRule.isActive)
                // The timestamp is the half a bare rejection would still get wrong.
                Assert.Equal(before |> ClassificationRule.modifiedAt, after |> ClassificationRule.modifiedAt)
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.3 REQ-CR-1.5 update returns an account-not-found error when the new codeAtMatch doesn't match an account code in the ledger``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            ClassificationOrchestration.updateClassificationRule
                context
                NoChange (SetTo(codeOf "F-9999")) NoChange NoChange NoChange
                (this.TwoGroupRule() |> idOf)
            |> fun r -> isCorrectError r AccountCodeDoesntMatchAccountId None)
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.4 update returns a validation error when the new rule groups list is empty``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            ClassificationOrchestration.updateClassificationRule
                context
                NoChange NoChange NoChange (SetTo []) NoChange
                (this.TwoGroupRule() |> idOf)
            |> fun r -> isCorrectErrorEmpty r IngestionClassificationRuleGroupsEmpty None)
        |> railroadWrapper

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(1)>]
    [<InlineData(2)>]
    member this.``REQ-CR-6.4 update returns a validation error when a chain within the new rule groups is empty``(emptyPosition: int) =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            let populated = chainOf [ Source(patternOf "TestEmptyChainOnUpdate") ]
            let empty = chainOf []
            let groups =
                match emptyPosition with
                | 0 -> [ ClassificationRuleGroup.create And empty None ]
                | 1 ->
                    [ ClassificationRuleGroup.create And populated None
                      ClassificationRuleGroup.create And empty None ]
                | _ -> [ ClassificationRuleGroup.create Or populated (Some empty) ]
            ClassificationOrchestration.updateClassificationRule
                context
                NoChange NoChange NoChange (SetTo groups) NoChange
                (this.TwoGroupRule() |> idOf)
            |> fun r -> isCorrectErrorEmpty r IngestionFieldMatchChainEmpty None)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-6.5 a successful update leaves modified_at later than the value it held before the update``() =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let! created =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf "CR-6.5 timestamp")
                        (codeOf "F-5350")
                        784
                        [ groupOf [ Source(patternOf "TestTimestamp") ] ]
                // modified_at is stamped from the context's initiation instant, not the wall
                // clock, so without a fresh instant the update writes the same value back and
                // the comparison below can never move.
                let laterContext = context |> Context.updateInitiationInstant
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        laterContext
                        (SetTo(ruleNameOf "CR-6.5 timestamp moved"))
                        NoChange NoChange NoChange NoChange
                        (created |> idOf)
                Assert.True(
                    (updated |> ClassificationRule.modifiedAt) > (created |> ClassificationRule.modifiedAt),
                    $"modified_at did not advance: {created |> ClassificationRule.modifiedAt} -> {updated |> ClassificationRule.modifiedAt}")
                Assert.Equal(created |> ClassificationRule.createdAt, updated |> ClassificationRule.createdAt)
            })
        |> railroadWrapper
