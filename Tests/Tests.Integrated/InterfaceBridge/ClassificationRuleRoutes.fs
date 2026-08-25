module Tests.Integrated.InterfaceBridge.ClassificationRuleRoutes

open System
open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model.DataIngestion.Classification
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Utilities.FieldUpdate
open Utilities.Json.Json
open Utilities.ResultHelper
open Xunit


(* Every route below reaches the same orchestrator functions ClassificationRuleCrud.fs
   already covers. What only exists here is the boundary work: JSON in and out, the account
   code the caller speaks resolved to the AccountId the model speaks, and the rule-group
   contract converted in both directions. That layer is what these tests are for. *)
[<Collection("SharedTestData")>]
type ClassificationRuleRouteTests(fixture: TestDataFixture) =

    static let accountCodeForNewRules = "F-5650"

    let accountByCode code =
        fixture.Data.accounts
        |> List.find(fun a -> a |> Account.code |> AccountCode.value = code)

    let ruleNameOf (r: ClassificationRule.ClassificationRule) =
        r |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value

    let namesOf (returns: ClassificationRuleReturn list) =
        returns |> List.map(fun r -> r.classificationRuleName) |> List.sort

    /// One group matching on source, which is the smallest shape the contract converter has
    /// to carry in both directions.
    static let groupMatchingSource pattern : ClassificationRuleGroupContract =
        (* Annotated because `open Model.DataIngestion.Classification` brings the domain
           ClassificationRuleGroup into scope with the same field names. *)
        { connector = "And"
          chainOne = ({ chain = [ FieldMatchContract.Source pattern ] }: FieldMatchChainContract)
          chainTwo = None }

    let createThroughRoute (name: string) (code: string) (priority: int) (groups: ClassificationRuleGroupContract list) =
        result {
            let! payload =
                { classificationRuleName = name
                  codeAtMatch = code
                  priority = priority
                  ruleGroups = groups }
                |> toJson<NewClassificationRuleInput>
            let! returnPayload = routeUiCommandForTesting "Ingestion" "NewClassificationRule" [] payload
            return! fromJson<ClassificationRuleReturn> returnPayload
        }

    // =========================================================================
    // REQ-CR-4.1 — create
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-4.1 the new classification rule route stores the name, account at match, priority, rule groups, and isActive it was given and returns all five, with the account code it was handed resolved to that account``() =
        let mutable idToCleanUp = None
        let expectedAccount = accountByCode accountCodeForNewRules
        let ruleName = "CR-4.1 route create"
        let groups = [ groupMatchingSource "CR-4.1RouteCreate" ]
        try
            result {
                let! created = createThroughRoute ruleName accountCodeForNewRules 42 groups
                idToCleanUp <- Some(created.classificationRuleId |> ClassificationRuleId.fromGuid)
                Assert.Equal(ruleName, created.classificationRuleName)
                Assert.Equal(42, created.priority)
                Assert.Equal<ClassificationRuleGroupContract list>(groups, created.ruleGroups)
                Assert.True(created.isActive)
                (* The caller sent a code and never an id. Both of these come back only if the
                   route resolved that code against the chart of accounts. *)
                Assert.Equal(accountCodeForNewRules, created.codeAtMatch)
                Assert.Equal(
                    expectedAccount |> Account.accountName |> AccountName.value,
                    created.accountNameAtMatch)
                (* Read it back through a second route call so the assertion is about what the
                   route committed, not about what it happened to return. *)
                let! byIdPayload =
                    { FetchClassificationRuleByIdInput.classificationRuleId = created.classificationRuleId }
                    |> toJson<FetchClassificationRuleByIdInput>
                let! refetchedPayload =
                    routeUiCommandForTesting "Ingestion" "FetchClassificationRuleById" [] byIdPayload
                let! refetched = fromJson<ClassificationRuleReturn> refetchedPayload
                Assert.Equal(ruleName, refetched.classificationRuleName)
                Assert.Equal(42, refetched.priority)
                Assert.Equal<ClassificationRuleGroupContract list>(groups, refetched.ruleGroups)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    // =========================================================================
    // REQ-CR-6.1 — update
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-6.1 the update classification rule route applies name, account at match, priority, rule groups, and isActive in one call and returns the rule carrying all five new values``() =
        let mutable idToCleanUp = None
        (* The route commits, so a fixture rule cannot be the subject — this test creates the
           rule it is about to update and deletes it in the finally. *)
        let startingCode = accountCodeForNewRules
        let endingCode = "F-5350"
        let endingAccount = accountByCode endingCode
        let startingGroups = [ groupMatchingSource "CR-6.1RouteBefore" ]
        let endingGroups = [ groupMatchingSource "CR-6.1RouteAfter" ]
        try
            result {
                let! created = createThroughRoute "CR-6.1 route update before" startingCode 7 startingGroups
                idToCleanUp <- Some(created.classificationRuleId |> ClassificationRuleId.fromGuid)
                let! payload =
                    { classificationRuleId = created.classificationRuleId
                      classificationRuleNameUpdate = SetTo "CR-6.1 route update after"
                      codeAtMatchUpdate = SetTo endingCode
                      priorityUpdate = SetTo 99
                      ruleGroupsUpdate = SetTo endingGroups
                      isActiveUpdate = SetTo false }
                    |> toJson<UpdateClassificationRuleInput>
                let! updatedPayload =
                    routeUiCommandForTesting "Ingestion" "UpdateClassificationRule" [] payload
                let! updated = fromJson<ClassificationRuleReturn> updatedPayload
                Assert.Equal(created.classificationRuleId, updated.classificationRuleId)
                Assert.Equal("CR-6.1 route update after", updated.classificationRuleName)
                Assert.Equal(endingCode, updated.codeAtMatch)
                Assert.Equal(
                    endingAccount |> Account.accountName |> AccountName.value,
                    updated.accountNameAtMatch)
                Assert.Equal(99, updated.priority)
                Assert.Equal<ClassificationRuleGroupContract list>(endingGroups, updated.ruleGroups)
                Assert.False(updated.isActive)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    // =========================================================================
    // REQ-CR-5.1 REQ-CR-5.2 — single-rule fetches
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-5.1 the fetch by id route returns the rule bearing that id — its name, account code, and priority — and not a sibling rule stored alongside it``() =
        let expected = fixture.Data.classificationRules |> List.head
        let expectedAccount =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = (expected |> ClassificationRule.accountIdAtMatch))
        result {
            let! payload =
                { FetchClassificationRuleByIdInput.classificationRuleId =
                    expected |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value }
                |> toJson<FetchClassificationRuleByIdInput>
            let! returnPayload = routeUiCommandForTesting "Ingestion" "FetchClassificationRuleById" [] payload
            let! returned = fromJson<ClassificationRuleReturn> returnPayload
            Assert.Equal(expected |> ruleNameOf, returned.classificationRuleName)
            Assert.Equal(expectedAccount |> Account.code |> AccountCode.value, returned.codeAtMatch)
            Assert.Equal(expected |> ClassificationRule.priority, returned.priority)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.2 the fetch by name route returns the rule bearing that exact name and not another rule sharing its opening words``() =
        (* Two fixture rules start "Allstate Insurance to ", so an implementation that matched
           on a prefix would have two candidates and could return either. *)
        let candidates =
            fixture.Data.classificationRules
            |> List.filter(fun r -> (r |> ruleNameOf).StartsWith "Allstate Insurance to "
        )
        let expected = candidates |> List.find(fun r -> (r |> ruleNameOf).EndsWith "5650")
        result {
            Assert.Equal(2, candidates |> List.length)
            let! payload =
                { FetchClassificationRuleByNameInput.classificationRuleName = expected |> ruleNameOf }
                |> toJson<FetchClassificationRuleByNameInput>
            let! returnPayload = routeUiCommandForTesting "Ingestion" "FetchClassificationRuleByName" [] payload
            let! returned = fromJson<ClassificationRuleReturn> returnPayload
            Assert.Equal(expected |> ruleNameOf, returned.classificationRuleName)
            Assert.Equal(
                expected |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value,
                returned.classificationRuleId)
        }
        |> railroadWrapper

    // =========================================================================
    // REQ-CR-5.3 — filtered fetch
    // =========================================================================

    member private _.FetchFiltered(filter: ClassificationRuleFilterInput) =
        result {
            let! payload =
                { FetchClassificationRuleFilteredInput.filter = filter; sort = None }
                |> toJson<FetchClassificationRuleFilteredInput>
            let! returnPayload =
                routeUiCommandForTesting "Ingestion" "FetchClassificationRuleFiltered" [] payload
            return! fromJson<ClassificationRuleReturn list> returnPayload
        }

    [<Fact>]
    member this.``REQ-CR-5.3 the filtered fetch route returns every rule whose name contains the fragment and no rule that does not``() =
        let fragment = "Allstate"
        let expected =
            fixture.Data.classificationRules
            |> List.filter(fun r -> (r |> ruleNameOf).Contains fragment)
        result {
            Assert.NotEmpty expected
            let! returned =
                this.FetchFiltered
                    { ruleId = None
                      nameLike = Some fragment
                      accountCodeAtMatch = None
                      sourceLike = None
                      activeOnly = false }
            Assert.Equal<string list>(expected |> List.map ruleNameOf |> List.sort, returned |> namesOf)
            Assert.All(returned, fun r -> Assert.Contains(fragment, r.classificationRuleName))
        }
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-CR-5.3 the filtered fetch route resolves the account code at match and returns every rule pointing at that account and no rule pointing elsewhere``() =
        let code = "F-5650"
        let account = accountByCode code
        let expected =
            fixture.Data.classificationRules
            |> List.filter(fun r -> (r |> ClassificationRule.accountIdAtMatch) = (account |> Account.accountId))
        result {
            Assert.NotEmpty expected
            (* The caller never sends an AccountId. If the route stopped resolving the code,
               the filter would either fail or match nothing. *)
            let! returned =
                this.FetchFiltered
                    { ruleId = None
                      nameLike = None
                      accountCodeAtMatch = Some code
                      sourceLike = None
                      activeOnly = false }
            Assert.Equal<string list>(expected |> List.map ruleNameOf |> List.sort, returned |> namesOf)
            Assert.All(returned, fun r -> Assert.Equal(code, r.codeAtMatch))
        }
        |> railroadWrapper
