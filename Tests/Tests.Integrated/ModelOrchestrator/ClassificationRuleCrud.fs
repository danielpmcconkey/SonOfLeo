module Tests.Integrated.ModelOrchestrator.ClassificationRuleCrud

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open DataAccessLayer.DbTransaction
open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion.Classification
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator
open ModelOrchestrator.FetchFilters
open Tests.Helpers
open Tests.Helpers.Cleanup
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
      accountAtMatch = None
      sourceLike = None
      activeOnly = false }

let private nameOf (r: ClassificationRule.ClassificationRule) =
    r |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value

let private codeStrOf
    (context: Context.Context)
    (r: ClassificationRule.ClassificationRule)
    : Result<string, AppError> =
    r
    |> ClassificationRule.accountIdAtMatch
    |> ``convert AccountId to AccountCodeString`` context

let private idOf (r: ClassificationRule.ClassificationRule) =
    r |> ClassificationRule.classificationRuleId

let private namesOf rules = rules |> List.map nameOf |> List.sort


[<Collection("SharedTestData")>]
type ClassificationRuleCrudTests(fixture: TestDataFixture) =

    // Fixture rule names, so expectations are derived from known fixture data rather than
    // from a second call to the thing under test.
    let fixtureRules () = fixture.Data.classificationRules

    (* The fixture carries exactly one inactive rule. exactlyOne rather than find so that a
       second one added later fails here loudly instead of silently changing what these
       tests are about. *)
    let inactiveFixtureRule () =
        fixtureRules () |> List.filter (fun r -> r |> ClassificationRule.isActive |> not) |> List.exactlyOne

    // =========================================================================
    // Create
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-4.1 REQ-CR-4.5 create returns the new rule bearing an id, a created_at and modified_at that are populated and equal, and the name, account, priority, and rule groups it was given`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let groups = [ groupOf [ Source(patternOf "TestReturnShape") ] ]
                let! created =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf "CR-4.1 return shape")
                        fixture.Data.food5350Id
                        777
                        groups
                Assert.Equal("CR-4.1 return shape", created |> nameOf)
                Assert.Equal(fixture.Data.food5350Id, created |> ClassificationRule.accountIdAtMatch )
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
                        fixture.Data.entertainment5650Id
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
                        fixture.Data.food5350Id
                        779
                        [ groupOf [ Source(patternOf "TestAlwaysActive") ] ]
                Assert.True(created |> ClassificationRule.isActive)
                let! fetched = created |> idOf |> ClassificationRule.fetchById context
                Assert.True(fetched |> ClassificationRule.isActive)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-4.3 REQ-CR-1.5 create returns an account-not-found error when the account at match doesn't exist in the ledger`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            // Rule groups are valid, because confirmRuleGroups runs first and would otherwise
            // return its own error before the code is ever looked at.
            ClassificationOrchestration.createNewClassificationRule
                context
                (ruleNameOf "CR-4.3 bogus code")
                (AccountId.create())
                780
                [ groupOf [ Source(patternOf "TestBogusCode") ] ]
            |> fun r -> isCorrectError r AccountIdDoesntMatch None)
        |> railroadWrapper

    (* Both REQ-CR-1.22 tests run on a NoTransaction context rather than inside the usual
       rollback. A failed statement aborts an open transaction, so nothing can be read back
       afterwards to see what the refusal left behind — which is the whole point of these two.
       Each cleans up in a finally, and each only ever adds a row: the incumbent is a fixture
       rule and no code path here rewrites it. *)

    [<Fact>]
    member _.``REQ-CR-1.22 create refuses a rule bearing a name an existing rule already holds and leaves that rule the only holder of it`` () =
        let mutable idToCleanUp = None
        let incumbent = fixtureRules () |> List.head
        let takenName = incumbent |> nameOf
        let context = Context.create NoTransaction IngestNewClassificationRule
        try
            result {
                do!
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf takenName)
                        fixture.Data.food5350Id
                        783
                        [ groupOf [ Source(patternOf "CR122Duplicate") ] ]
                    |> fun r ->
                        (match r with
                         | Ok created -> idToCleanUp <- Some(created |> idOf)
                         | Error _ -> ())
                        isCorrectError r DalErrorDuringNonQueryExecution None
                (* The error alone would also be satisfied by an implementation that resolved
                   the collision by overwriting the rule already holding the name. *)
                let! holders =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with nameLike = Some(ruleNameOf takenName) }
                        None
                let exact = holders |> List.filter (fun r -> r |> nameOf = takenName)
                Assert.Equal(1, exact |> List.length)
                let survivor = exact |> List.exactlyOne
                Assert.Equal(incumbent |> idOf, survivor |> idOf)
                Assert.Equal(incumbent |> ClassificationRule.priority, survivor |> ClassificationRule.priority)
                Assert.Equal(
                    incumbent |> ClassificationRule.accountIdAtMatch,
                    survivor |> ClassificationRule.accountIdAtMatch)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    [<Fact>]
    member _.``REQ-CR-1.22 update refuses to rename a rule onto a name another rule already holds and leaves both rules with the names they had`` () =
        let mutable idToCleanUp = None
        let incumbent = fixtureRules () |> List.head
        let takenName = incumbent |> nameOf
        let subjectName = "CR-1.22 rename subject"
        let context = Context.create NoTransaction IngestUpdateClassificationRule
        try
            result {
                (* The subject has to be committed for the rename to be attempted against real
                   state, so the test creates it and deletes it again. A fixture rule cannot be
                   the subject: the update path commits, and a wrongly-successful rename would
                   leave fixture data renamed for every later test. *)
                let! subject =
                    ClassificationOrchestration.createNewClassificationRule
                        context
                        (ruleNameOf subjectName)
                        fixture.Data.food5350Id
                        784
                        [ groupOf [ Source(patternOf "CR122Rename") ] ]
                idToCleanUp <- Some(subject |> idOf)
                do!
                    ClassificationOrchestration.updateClassificationRule
                        context
                        (SetTo(ruleNameOf takenName))
                        NoChange NoChange NoChange NoChange
                        (subject |> idOf)
                    |> fun r -> isCorrectError r DalErrorDuringNonQueryExecution None
                let! holders =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with nameLike = Some(ruleNameOf takenName) }
                        None
                let exact = holders |> List.filter (fun r -> r |> nameOf = takenName)
                Assert.Equal(1, exact |> List.length)
                Assert.Equal(incumbent |> idOf, exact |> List.exactlyOne |> idOf)
                let! subjectAfter = subject |> idOf |> ClassificationRule.fetchById context
                Assert.Equal(subjectName, subjectAfter |> nameOf)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    [<Fact>]
    member _.``REQ-CR-4.6 REQ-CR-1.7 create returns a validation error when the rule groups list is empty`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            ClassificationOrchestration.createNewClassificationRule
                context
                (ruleNameOf "CR-4.6 no groups")
                fixture.Data.food5350Id
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
                fixture.Data.food5350Id
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
                        fixture.Data.food5350Id
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
    member _.``REQ-CR-5.3 fetchRulesFiltered by account at match returns every rule assigned that account and no rule assigned a different one`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let expected =
                    fixtureRules ()
                    |> List.filter (fun r -> r |> ClassificationRule.accountIdAtMatch = fixture.Data.entertainment5650Id)
                    |> namesOf
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with accountAtMatch = Some(fixture.Data.entertainment5650Id) }
                        None
                Assert.NotEmpty(expected)
                Assert.NotEqual<string list>(fixtureRules () |> namesOf, expected)
                Assert.Equal<string list>(expected, found |> namesOf)
                Assert.DoesNotContain(fixture.Data.food5350Id, found |> List.map ClassificationRule.accountIdAtMatch)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered by source pattern fragment returns the rules whose rule group bodies carry that pattern and no others`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let sourceFragment = "TestSplitBank"
                (* The filter searches the rule group bodies; the expectation is derived from
                   rule names, which the filter never reads. The two agree only if the filter
                   found the right rules for the right reason. *)
                let expected =
                    fixtureRules ()
                    |> List.filter (fun r -> (r |> nameOf).Contains sourceFragment)
                    |> namesOf
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with sourceLike = Some sourceFragment }
                        None
                Assert.NotEmpty(expected)
                Assert.NotEqual<string list>(fixtureRules () |> namesOf, expected)
                Assert.Equal<string list>(expected, found |> namesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered with activeOnly true omits the inactive rule that its other filters would otherwise have returned`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let inactiveName = inactiveFixtureRule () |> nameOf
                let! withInactive =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with accountAtMatch = Some(fixture.Data.personalExpenses5300Id) }
                        None
                // The filter really does select it when activeOnly is off, so the omission below
                // is the active filter working rather than the code filter missing it.
                Assert.Contains(inactiveName, withInactive |> namesOf)
                let! activeOnly =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with accountAtMatch = Some(fixture.Data.personalExpenses5300Id); activeOnly = true }
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
                    |> List.filter (fun r -> r |> ClassificationRule.accountIdAtMatch = fixture.Data.personalExpenses5300Id)
                    |> namesOf
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with accountAtMatch = Some(fixture.Data.personalExpenses5300Id) }
                        None
                Assert.NotEmpty(expected)
                Assert.NotEqual<string list>(fixtureRules () |> namesOf, expected)
                Assert.Equal<string list>(expected, found |> namesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.3 fetchRulesFiltered given both a name fragment and an account returns only the rules satisfying both, not the union`` () =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let fragment = "Allstate"
                let code = "F-5650"
                let both =
                    fixtureRules ()
                    |> List.filter (fun r -> (r |> nameOf).Contains fragment && r |> ClassificationRule.accountIdAtMatch = fixture.Data.entertainment5650Id)
                    |> namesOf
                let union =
                    fixtureRules ()
                    |> List.filter (fun r -> (r |> nameOf).Contains fragment || r |> ClassificationRule.accountIdAtMatch = fixture.Data.entertainment5650Id)
                    |> namesOf
                // The two sets must differ or the assertion below proves nothing.
                Assert.NotEqual<string list>(both, union)
                let! found =
                    ClassificationOrchestration.fetchRulesFiltered
                        context
                        { noFilter with nameLike = Some(ruleNameOf fragment); accountAtMatch = Some(fixture.Data.entertainment5650Id) }
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

    // Ascending and descending are asserted against each other rather than against a
    // hand-written order: the reverse of one must be the other, which a query that ignored the
    // direction could not satisfy.
    [<Theory>]
    [<InlineData("code")>]
    [<InlineData("priority")>]
    member _.``REQ-CR-5.4 fetchRulesFiltered sorted ascending returns rules in increasing order of the named key, and sorted descending returns the exact reverse``(key: string) =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let asc, desc =
                    match key with
                    | "code" -> AccountCodeAsc, AccountCodeDesc
                    | _ -> PriorityAsc, PriorityDesc
                let! ascending = ClassificationOrchestration.fetchRulesFiltered context noFilter (Some asc)
                let! descending = ClassificationOrchestration.fetchRulesFiltered context noFilter (Some desc)
                // Both directions must still return the whole table, or an ordering assertion
                // over a truncated list would pass on nothing.
                Assert.Equal(fixtureRules () |> List.length, ascending |> List.length)
                Assert.Equal(fixtureRules () |> List.length, descending |> List.length)
                // Priority is compared as int and code as string: stringifying priority would
                // sort 1000 before 500 and the test would be asserting the wrong order.
                match key with
                | "code" ->
                    let! ascKeys = ascending |> List.map (codeStrOf context) |> convertListOfResultsToResultsList
                    let! descKeys = descending |> List.map (codeStrOf context) |> convertListOfResultsToResultsList
                    Assert.Equal<string list>(ascKeys |> List.sort, ascKeys)
                    Assert.Equal<string list>(ascKeys |> List.rev, descKeys)
                | _ ->
                    let ascKeys = ascending |> List.map ClassificationRule.priority
                    Assert.Equal<int list>(ascKeys |> List.sort, ascKeys)
                    Assert.Equal<int list>(ascKeys |> List.rev, descending |> List.map ClassificationRule.priority)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.4 fetchRulesFiltered sorted by priority ascending places no rule before one of lower priority, and places rules tied at the same priority adjacent to each other``() =
        runCommandRouteAndAutoRollback IngestNewClassificationRule (fun context ->
            result {
                let! sorted = ClassificationOrchestration.fetchRulesFiltered context noFilter (Some PriorityAsc)
                let priorities = sorted |> List.map ClassificationRule.priority
                Assert.Equal<int list>(priorities |> List.sort, priorities)
                // A tie exists in the fixture, so the adjacency claim below is about real data.
                let tied =
                    priorities
                    |> List.countBy id
                    |> List.filter (fun (_, n) -> n > 1)
                Assert.NotEmpty(tied)
                // Every priority occupies one unbroken run: the number of distinct values equals
                // the number of runs. A stable sort that scattered a tie would break this.
                let runs =
                    priorities
                    |> List.fold (fun acc p ->
                        match acc with
                        | (prev: int) :: _ when prev = p -> acc
                        | _ -> p :: acc) []
                    |> List.length
                Assert.Equal(priorities |> List.distinct |> List.length, runs)
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
        if changed <> "account" then Assert.Equal(
            original |> ClassificationRule.accountIdAtMatch, updated |> ClassificationRule.accountIdAtMatch)
        if changed <> "priority" then
            Assert.Equal(original |> ClassificationRule.priority, updated |> ClassificationRule.priority)
        if changed <> "groups" then
            Assert.Equal<ClassificationRuleGroup list>(
                original |> ClassificationRule.ruleGroups,
                updated |> ClassificationRule.ruleGroups)
        if changed <> "isActive" then
            Assert.Equal(original |> ClassificationRule.isActive, updated |> ClassificationRule.isActive)

    [<Fact>]
    member this.``REQ-CR-6.1 updating name with SetTo changes the name and leaves account, priority, rule groups, and isActive as they were``() =
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
    member this.``REQ-CR-6.1 updating the account at match with SetTo changes the account and leaves name, priority, rule groups, and isActive as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = this.TwoGroupRule()
                let! updated =
                    ClassificationOrchestration.updateClassificationRule
                        context
                        NoChange (SetTo(fixture.Data.food5350Id)) NoChange NoChange NoChange
                        (original |> idOf)
                Assert.Equal(fixture.Data.food5350Id, updated |> ClassificationRule.accountIdAtMatch)
                Assert.NotEqual<AccountId>(
                    original |> ClassificationRule.accountIdAtMatch, updated |> ClassificationRule.accountIdAtMatch)
                this.AssertOnlyChangedField updated original "account"
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.1 updating priority with SetTo changes the priority and leaves name, account, rule groups, and isActive as they were``() =
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
    member this.``REQ-CR-6.1 updating ruleGroups with SetTo leaves exactly the new rule groups with none of the old surviving, and leaves name, account, priority, and isActive as they were``() =
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
    member this.``REQ-CR-6.1 updating isActive with SetTo false deactivates the rule and leaves name, account, priority, and rule groups as they were``() =
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
    member this.``REQ-CR-6.1 updating isActive with SetTo true reactivates the inactive rule and leaves name, account, priority, and rule groups as they were``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            result {
                let original = inactiveFixtureRule ()
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
                Assert.Equal(before |> ClassificationRule.accountIdAtMatch, after |> ClassificationRule.accountIdAtMatch)
                Assert.Equal(before |> ClassificationRule.priority, after |> ClassificationRule.priority)
                Assert.Equal(before |> ClassificationRule.isActive, after |> ClassificationRule.isActive)
                // The timestamp is the half a bare rejection would still get wrong.
                Assert.Equal(before |> ClassificationRule.modifiedAt, after |> ClassificationRule.modifiedAt)
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-6.3 REQ-CR-1.5 update returns an account-not-found error when the new account at match doesn't exist in the ledger``() =
        runCommandRouteAndAutoRollback IngestUpdateClassificationRule (fun context ->
            ClassificationOrchestration.updateClassificationRule
                context
                NoChange (SetTo(AccountId.create())) NoChange NoChange NoChange
                (this.TwoGroupRule() |> idOf)
            |> fun r -> isCorrectError r AccountIdDoesntMatch None)
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
                        fixture.Data.food5350Id
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
