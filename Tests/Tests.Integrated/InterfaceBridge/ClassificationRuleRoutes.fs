module Tests.Integrated.InterfaceBridge.ClassificationRuleRoutes

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.InterfaceContracts.IngestionContracts
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts
open Business.FinancialServices.Classification
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open App.Utility.FieldUpdate
open App.Utility.Json.Json
open App.Utility.Result
open Xunit
open Business.FinancialServices.Classification.ClassificationComponent
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Business.FinancialServices.Classification.ClassificationError


(* Every route below reaches the same orchestrator functions ClassificationRuleCrud.fs
   already covers. What only exists here is the boundary work: JSON in and out, the account
   code the caller speaks resolved to the AccountId the model speaks, and the rule-group
   contract converted in both directions. That layer is what these tests are for. *)
(* The invalid pattern is an unclosed group. It reaches the route as a plain string in the payload, which is the
   only way a caller can hand the system one. *)
let private invalidPattern = "CR-1.26 (unclosed"

(* An account code is at most 10 characters, and none in the fixture or any test starts with ZZ. *)
let private unknownAccountCode = "ZZ-CR-4.3"

let private groupMatching (field: string) (pattern: string) : ClassificationRuleGroupContract =
    let fieldMatch =
        match field with
        | "Source" -> FieldMatchContract.Source pattern
        | "Description" -> FieldMatchContract.Description pattern
        | other -> failwith $"no string field {other}"
    { connector = "And"
      chainOne = ({ chain = [ fieldMatch ] }: FieldMatchChainContract)
      chainTwo = None }

[<Collection("SharedTestData")>]
type ClassificationRuleRouteTests(fixture: TestDataFixture) =

    static let accountCodeForNewRules = "F-5650"

    let accountByCode code =
        fixture.Data.accounts
        |> List.find(fun a -> a |> Account.code |> AccountCode.value = code)

    let ruleNameOf (r: ClassificationRule.ClassificationRule) =
        r |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value

    /// The account code a returned rule claims at match, if it claims an account at all.
    static let claimantCodeOf (claimant: ClassificationClaimantReturn) =
        match claimant with
        | ClassificationClaimantReturn.Account a -> Some a.code
        | ClassificationClaimantReturn.PaymentAgreement _ -> None

    /// The account name a returned rule claims at match, if it claims an account at all.
    static let claimantAccountNameOf (claimant: ClassificationClaimantReturn) =
        match claimant with
        | ClassificationClaimantReturn.Account a -> Some a.accountName
        | ClassificationClaimantReturn.PaymentAgreement _ -> None

    let namesOf (returns: ClassificationRuleReturn list) =
        returns |> List.map(fun r -> r.classificationRuleName) |> List.sort

    /// One group matching on source, which is the smallest shape the contract converter has
    /// to carry in both directions.
    static let groupMatchingSource pattern : ClassificationRuleGroupContract =
        (* Annotated because `open Business.FinancialServices.DataIngestion.Classification` brings the domain
           ClassificationRuleGroup into scope with the same field names. *)
        { connector = "And"
          chainOne = ({ chain = [ FieldMatchContract.Source pattern ] }: FieldMatchChainContract)
          chainTwo = None }

    let createThroughRoute (name: string) (code: string) (priority: int) (groups: ClassificationRuleGroupContract list) =
        result {
            let! payload =
                { classificationRuleName = name
                  claimantAtMatch = ClassificationClaimantInput.Account code
                  priority = priority
                  ruleGroups = groups }
                |> toJson<NewClassificationRuleInput>
            let! returnPayload = routeUiCommandForTesting "Classification" "NewClassificationRule" [] payload
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
                Assert.Equal(Some(accountCodeForNewRules), created.claimantAtMatch |> claimantCodeOf)
                Assert.Equal(
                    Some(expectedAccount |> Account.accountName |> AccountName.value),
                    created.claimantAtMatch |> claimantAccountNameOf)
                (* Read it back through a second route call so the assertion is about what the
                   route committed, not about what it happened to return. *)
                let! byIdPayload =
                    { FetchClassificationRuleByIdInput.classificationRuleId = created.classificationRuleId }
                    |> toJson<FetchClassificationRuleByIdInput>
                let! refetchedPayload =
                    routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [] byIdPayload
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
                      claimantAtMatchUpdate = SetTo (ClassificationClaimantInput.Account endingCode)
                      priorityUpdate = SetTo 99
                      ruleGroupsUpdate = SetTo endingGroups
                      isActiveUpdate = SetTo false }
                    |> toJson<UpdateClassificationRuleInput>
                let! updatedPayload =
                    routeUiCommandForTesting "Classification" "UpdateClassificationRule" [] payload
                let! updated = fromJson<ClassificationRuleReturn> updatedPayload
                Assert.Equal(created.classificationRuleId, updated.classificationRuleId)
                Assert.Equal("CR-6.1 route update after", updated.classificationRuleName)
                Assert.Equal(Some(endingCode), updated.claimantAtMatch |> claimantCodeOf)
                Assert.Equal(
                    Some(endingAccount |> Account.accountName |> AccountName.value),
                    updated.claimantAtMatch |> claimantAccountNameOf)
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
            |> List.find(fun a -> ClassificationClaimant.Account(a |> Account.accountId) = (expected |> ClassificationRule.classificationClaimant))
        result {
            let! payload =
                { FetchClassificationRuleByIdInput.classificationRuleId =
                    expected |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value }
                |> toJson<FetchClassificationRuleByIdInput>
            let! returnPayload = routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [] payload
            let! returned = fromJson<ClassificationRuleReturn> returnPayload
            Assert.Equal(expected |> ruleNameOf, returned.classificationRuleName)
            Assert.Equal(Some(expectedAccount |> Account.code |> AccountCode.value), returned.claimantAtMatch |> claimantCodeOf)
            Assert.Equal(expected |> ClassificationRule.priority, returned.priority)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CR-5.2 the fetch by name route returns the rule bearing that exact name and not another rule sharing its opening words``() =
        (* Two fixture rules start "Acme Insurance to ", so an implementation that matched
           on a prefix would have two candidates and could return either. *)
        let candidates =
            fixture.Data.classificationRules
            |> List.filter(fun r -> (r |> ruleNameOf).StartsWith "Acme Insurance to "
        )
        let expected = candidates |> List.find(fun r -> (r |> ruleNameOf).EndsWith "5650")
        result {
            Assert.Equal(2, candidates |> List.length)
            let! payload =
                { FetchClassificationRuleByNameInput.classificationRuleName = expected |> ruleNameOf }
                |> toJson<FetchClassificationRuleByNameInput>
            let! returnPayload = routeUiCommandForTesting "Classification" "FetchClassificationRuleByName" [] payload
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
                routeUiCommandForTesting "Classification" "FetchClassificationRuleFiltered" [] payload
            return! fromJson<ClassificationRuleReturn list> returnPayload
        }

    [<Fact>]
    member this.``REQ-CR-5.3 the filtered fetch route returns every rule whose name contains the fragment and no rule that does not``() =
        let fragment = "Acme Insurance"
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
                      paymentAgreementNameAtMatch = None
                      claimantType = None
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
            |> List.filter(fun r -> (r |> ClassificationRule.classificationClaimant) = ClassificationClaimant.Account(account |> Account.accountId))
        result {
            Assert.NotEmpty expected
            (* The caller never sends an AccountId. If the route stopped resolving the code,
               the filter would either fail or match nothing. *)
            let! returned =
                this.FetchFiltered
                    { ruleId = None
                      nameLike = None
                      accountCodeAtMatch = Some code
                      paymentAgreementNameAtMatch = None
                      claimantType = None
                      sourceLike = None
                      activeOnly = false }
            Assert.Equal<string list>(expected |> List.map ruleNameOf |> List.sort, returned |> namesOf)
            Assert.All(returned, fun r -> Assert.Equal(Some(code), r.claimantAtMatch |> claimantCodeOf))
        }
        |> railroadWrapper


    // =========================================================================
    // REQ-CR-5.5 — resolved account name on the returned rule
    // =========================================================================

    [<Fact>]
    member _.``REQ-CR-5.5 the fetch by id route resolves each of the two Acme Insurance rules to the name of its own account at match, not to a shared or first-found account name``() =
        (* Two fixture rules whose names differ only in their last four characters and which
           point at two different accounts. A resolver returning a constant, or the first
           account it found, or anything derived from the rule name satisfies one of these and
           fails the other. One rule could not tell those apart. *)
        let acmeInsuranceRules =
            fixture.Data.classificationRules
            |> List.filter(fun r -> (r |> ruleNameOf).StartsWith "Acme Insurance to ")
        let expectedNameFor (rule: ClassificationRule.ClassificationRule) =
            fixture.Data.accounts
            |> List.find(fun a -> ClassificationClaimant.Account(a |> Account.accountId) = (rule |> ClassificationRule.classificationClaimant))
            |> Account.accountName
            |> AccountName.value
        result {
            Assert.Equal(2, acmeInsuranceRules |> List.length)
            (* If the fixture ever pointed both rules at one account the assertions below would
               still pass while proving nothing. *)
            Assert.Equal(2, acmeInsuranceRules |> List.map expectedNameFor |> List.distinct |> List.length)
            let! fetched =
                acmeInsuranceRules
                |> List.map(fun rule ->
                    result {
                        let! payload =
                            { FetchClassificationRuleByIdInput.classificationRuleId =
                                rule |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value }
                            |> toJson<FetchClassificationRuleByIdInput>
                        let! returnPayload =
                            routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [] payload
                        let! returned = fromJson<ClassificationRuleReturn> returnPayload
                        return (rule, returned)
                    })
                |> convertListOfResultsToResultsList
            fetched
            |> List.iter(fun (rule, returned) ->
                Assert.Equal(Some(expectedNameFor rule), returned.claimantAtMatch |> claimantAccountNameOf))
        }
        |> railroadWrapper

    // =========================================================================
    // REQ-CR-1.26 — a pattern must be a valid regular expression
    // =========================================================================

    [<Theory>]
    [<InlineData("Source")>]
    [<InlineData("Description")>]
    member this.``REQ-CR-1.26 creating a rule whose Source or Description pattern is not a valid regular expression is rejected with a typed error naming the pattern, and no rule is written`` (field: string) =
        let ruleName = $"CR-1.26 create invalid {field}"
        let mutable idToCleanUp = None
        try
            result {
                let created = createThroughRoute ruleName accountCodeForNewRules 780 [ groupMatching field invalidPattern ]
                let () =
                    match created with
                    | Error (AsError (Business.FinancialServices.Classification.ClassificationError.ClassificationSearchPatternInvalidRegex(pattern, _))) ->
                        Assert.Equal(invalidPattern, pattern)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok rule ->
                        idToCleanUp <- Some(rule.classificationRuleId |> ClassificationRuleId.fromGuid)
                        Assert.Fail "Expected the invalid pattern to be rejected; the rule was created"
                // a rule written with the bad pattern would fail this read rather than pass it
                let! named =
                    this.FetchFiltered
                        { ruleId = None
                          nameLike = Some ruleName
                          accountCodeAtMatch = None
                          paymentAgreementNameAtMatch = None
                          claimantType = None
                          sourceLike = None
                          activeOnly = false }
                Assert.Empty(named)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    [<Theory>]
    [<InlineData("Source")>]
    [<InlineData("Description")>]
    member this.``REQ-CR-1.26 updating a rule's Source or Description pattern to one that is not a valid regular expression is rejected with a typed error naming the pattern, and the stored rule keeps its old pattern`` (field: string) =
        let mutable idToCleanUp = None
        let validGroups = [ groupMatching field $"CR-1.26 update valid {field}" ]
        try
            result {
                let! created = createThroughRoute $"CR-1.26 update {field}" accountCodeForNewRules 781 validGroups
                idToCleanUp <- Some(created.classificationRuleId |> ClassificationRuleId.fromGuid)
                let! payload =
                    { classificationRuleId = created.classificationRuleId
                      classificationRuleNameUpdate = NoChange
                      claimantAtMatchUpdate = NoChange
                      priorityUpdate = NoChange
                      ruleGroupsUpdate = SetTo [ groupMatching field invalidPattern ]
                      isActiveUpdate = NoChange }
                    |> toJson<UpdateClassificationRuleInput>
                let () =
                    match routeUiCommandForTesting "Classification" "UpdateClassificationRule" [] payload with
                    | Error (AsError (Business.FinancialServices.Classification.ClassificationError.ClassificationSearchPatternInvalidRegex(pattern, _))) ->
                        Assert.Equal(invalidPattern, pattern)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected the invalid pattern to be rejected; the update succeeded"
                let! byIdPayload =
                    { FetchClassificationRuleByIdInput.classificationRuleId = created.classificationRuleId }
                    |> toJson<FetchClassificationRuleByIdInput>
                let! refetchedPayload = routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [] byIdPayload
                let! refetched = fromJson<ClassificationRuleReturn> refetchedPayload
                Assert.Equal<ClassificationRuleGroupContract list>(validGroups, refetched.ruleGroups)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    // =========================================================================
    // REQ-CR-4.3, 6.3 — an account claimant is named by an account code the caller supplies
    // =========================================================================

    [<Fact>]
    member this.``REQ-CR-4.3 the new classification rule route given an account code that names no account fails with the account-code error carrying that code, and no rule is written``() =
        let ruleName = "CR-4.3 unknown account code"
        let mutable idToCleanUp = None
        try
            result {
                let! () =
                    match createThroughRoute ruleName unknownAccountCode 782 [ groupMatchingSource "CR-4.3 unknown" ] with
                    | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId code)) ->
                        Assert.Equal(unknownAccountCode, code)
                        Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}" :> IAppError)
                    | Ok rule ->
                        idToCleanUp <- Some(rule.classificationRuleId |> ClassificationRuleId.fromGuid)
                        Error (TestingError "Expected the unknown account code to be rejected; the rule was created" :> IAppError)
                let! named =
                    this.FetchFiltered
                        { ruleId = None
                          nameLike = Some ruleName
                          accountCodeAtMatch = None
                          paymentAgreementNameAtMatch = None
                          claimantType = None
                          sourceLike = None
                          activeOnly = false }
                Assert.Empty(named)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore

    [<Fact>]
    member _.``REQ-CR-6.3 the update classification rule route given an account code that names no account fails with the account-code error carrying that code, and the stored rule is unchanged``() =
        let mutable idToCleanUp = None
        try
            result {
                let! created = createThroughRoute "CR-6.3 unknown account code" accountCodeForNewRules 783 [ groupMatchingSource "CR-6.3 unknown" ]
                idToCleanUp <- Some(created.classificationRuleId |> ClassificationRuleId.fromGuid)
                let! payload =
                    { classificationRuleId = created.classificationRuleId
                      classificationRuleNameUpdate = SetTo "CR-6.3 renamed alongside the unknown code"
                      claimantAtMatchUpdate = SetTo (ClassificationClaimantInput.Account unknownAccountCode)
                      priorityUpdate = NoChange
                      ruleGroupsUpdate = NoChange
                      isActiveUpdate = NoChange }
                    |> toJson<UpdateClassificationRuleInput>
                let! () =
                    match routeUiCommandForTesting "Classification" "UpdateClassificationRule" [] payload with
                    | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId code)) ->
                        Assert.Equal(unknownAccountCode, code)
                        Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}" :> IAppError)
                    | Ok _ -> Error (TestingError "Expected the unknown account code to be rejected; the update succeeded" :> IAppError)
                let! byIdPayload =
                    { FetchClassificationRuleByIdInput.classificationRuleId = created.classificationRuleId }
                    |> toJson<FetchClassificationRuleByIdInput>
                let! refetchedPayload = routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [] byIdPayload
                let! refetched = fromJson<ClassificationRuleReturn> refetchedPayload
                Assert.Equal(created, refetched)
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore
