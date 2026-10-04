module Tests.Integrated.InterfaceBridge.SameValueUpdates

open System
open App.Session
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification.ClassificationComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open App.DataAccessLayer.ExecuteReader
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module CashFlowContracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts
module AccountContracts = Ui.InterfaceBridge.InterfaceContracts.AccountContracts
module JournalContracts = Ui.InterfaceBridge.InterfaceContracts.JournalContracts
module ClassificationContracts = Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts

(* REQ-SYS-6.1 rejects an update that names no field. A field re-sent at the value it already holds is named, so the
   update succeeds and is written: its modified-at moves to the update's instant. One case per updatable entity
   family, each through the route the operator uses. Every route here commits, so each test creates the entity it
   updates, sleeps past the creation instant, reads back from a fresh context, and deletes what it made in a finally. *)

let private fresh () = Context.create NoTransaction FetchOnly

/// Asserts the instant moved forward, naming both in the failure.
let private assertAdvanced (before: NodaTime.Instant) (after: NodaTime.Instant) =
    Assert.True(after > before, $"modified-at did not advance: before {before}, after {after}")

[<Collection("SharedTestData")>]
type SameValueUpdateTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-CF-14.2 an update that sets a Master Agreement's name to the name it already holds succeeds and advances its modified-at`` () =
        let name = $"SYS-6.1 agreement {Guid.NewGuid():N}"
        let storedNamed () =
            ({ agreementIds = None; activeAgreementsOnly = false } : AgreementFilter)
            |> AgreementOrchestration.fetchFiltered (fresh ()) AnyQuantityIsAcceptable
            |> Result.map (
                List.map AgreementOrchestration.masterAgreement
                >> List.filter (fun m -> m |> MasterAgreement.agreementName |> AgreementName.value = name))
        try
            result {
                let! createPayload =
                    ({ agreementName = name
                       direction = "Outgo"
                       cadence = { cadenceType = CashFlowContracts.Daily; nextInstance = Calendar.today () }
                       counterparty = "Same value update test counterparty"
                       activeBegin = Calendar.today().PlusDays(-30)
                       activeEnd = None
                       memo = None
                       paymentAgreements =
                         [ { paymentAgreementName = $"{name} leg"
                             debitAccountCode = "F-2230"
                             creditAccountCode = "F-1280"
                             expectedAmount = Some 100.00M
                             daysDueAfterInvoiceDate = Some 0
                             memo = None } ] } : CashFlowContracts.CreateAgreementInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "CashFlow" "CreateAgreement" [] createPayload
                let! before = storedNamed () |> Result.map List.exactlyOne
                System.Threading.Thread.Sleep(10)
                let! updatePayload =
                    ({ agreementName = name
                       agreementNameUpdate = SetTo name
                       directionUpdate = NoChange
                       cadenceUpdate = NoChange
                       counterpartyUpdate = NoChange
                       activeBeginUpdate = NoChange
                       activeEndUpdate = NoChange
                       memoUpdate = NoChange
                       paymentAgreementUpdates = []
                       newPaymentAgreements = [] } : CashFlowContracts.UpdateAgreementInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "CashFlow" "UpdateAgreement" [] updatePayload
                let! after = storedNamed () |> Result.map List.exactlyOne
                Assert.Equal<string>(name, after |> MasterAgreement.agreementName |> AgreementName.value)
                assertAdvanced (before |> MasterAgreement.modifiedAt) (after |> MasterAgreement.modifiedAt)
            }
            |> railroadWrapper
        finally
            match storedNamed () with
            | Ok stored ->
                for m in stored do
                    cleanUpMasterAgreementTree (Some(m |> MasterAgreement.agreementID |> MasterAgreementId.value)) |> ignore
            | Error _ -> ()

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-AC-4.8 a rename that sets an Account's name to the name it already holds succeeds, the name reads back unchanged and modified-at advances`` () =
        let code = "SYS-6.1-N"
        let mutable idToCleanUp = None
        try
            result {
                let! created, accountId = code |> createTestAccountFromCodeString (fresh ())
                idToCleanUp <- Some accountId
                let currentName = created |> Account.accountName |> AccountName.value
                System.Threading.Thread.Sleep(10)
                let! payload =
                    ({ code = code; newName = currentName } : AccountContracts.AccountUpdateNameInput) |> Json.toJson
                let! _ = routeUiCommandForTesting "Account" "UpdateName" [] payload
                let! after = accountId |> Account.fetchById (fresh ())
                Assert.Equal<string>(currentName, after |> Account.accountName |> AccountName.value)
                assertAdvanced (created |> Account.modifiedAt) (after |> Account.modifiedAt)
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId idToCleanUp with
            | Ok () -> ()
            | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-AC-4.9 an external reference update re-sending an Account's current external reference succeeds and modified-at advances`` () =
        let code = "SYS-6.1-R"
        let reference = "SYS-6.1 account reference"
        let mutable idToCleanUp = None
        try
            result {
                let! created, accountId =
                    createTestAccountFromPrimitives
                        (fresh ()) code GenericTestProperties.genericAccountNameString
                        GenericTestProperties.genericAccountTypeString GenericTestProperties.genericActiveBegin None None
                        None (Some reference)
                idToCleanUp <- Some accountId
                System.Threading.Thread.Sleep(10)
                let! payload =
                    ({ code = code; newReference = Some reference } : AccountContracts.AccountUpdateExternalReferenceInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload
                let! after = accountId |> Account.fetchById (fresh ())
                Assert.Equal(Some reference, after |> Account.externalReference |> Option.map AccountExternalReference.value)
                assertAdvanced (created |> Account.modifiedAt) (after |> Account.modifiedAt)
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId idToCleanUp with
            | Ok () -> ()
            | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-JE-5.3 a comment amendment re-sending the comment's current text succeeds and modified-at advances`` () =
        let text = "SYS-6.1 comment text"
        let mutable idToCleanUp = None
        try
            result {
                let! created, headerId =
                    createTestJournalEntryFromPrimitives
                        (fresh ()) "REQ-SYS-6.1 JE whose comment is re-sent" None (Calendar.today ())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        [ (None, text) ]
                idToCleanUp <- Some headerId
                let before = created |> comments |> List.exactlyOne
                System.Threading.Thread.Sleep(10)
                let! payload =
                    ({ id = before |> JournalEntryComment.journalEntryCommentId |> JournalEntryCommentId.value
                       secondaryJournalEntryId = NoChange
                       commentText = SetTo text } : JournalContracts.JournalEntryUpdateCommentInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "JournalEntry" "UpdateComment" [] payload
                let! refetched = headerId |> fetchById (fresh ())
                let after = refetched |> comments |> List.exactlyOne
                Assert.Equal<string>(text, after |> JournalEntryComment.commentText |> CommentText.value)
                assertAdvanced (before |> JournalEntryComment.modifiedAt) (after |> JournalEntryComment.modifiedAt)
            }
            |> railroadWrapper
        finally
            match idToCleanUp |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-JE-4.9 a journal entry reference update re-sending its current FI and value succeeds and modified-at advances`` () =
        let fi = "SYS61Bank"
        let referenceText = "SYS-6.1-REF"
        let mutable idToCleanUp = None
        try
            result {
                let! created, headerId =
                    createTestJournalEntryFromPrimitives
                        (fresh ()) "REQ-SYS-6.1 JE whose reference is re-sent" None (Calendar.today ())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [ (fi, referenceText) ]
                        []
                idToCleanUp <- Some headerId
                let before = created |> externalReferences |> List.exactlyOne
                System.Threading.Thread.Sleep(10)
                let! payload =
                    ({ id = before |> JournalEntryExternalReference.journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
                       fi = Some fi
                       reference = Some referenceText } : JournalContracts.JournalEntryUpdateExternalReferenceInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "JournalEntry" "UpdateExternalReference" [] payload
                let! refetched = headerId |> fetchById (fresh ())
                let after = refetched |> externalReferences |> List.exactlyOne
                Assert.Equal<string>(fi, after |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
                Assert.Equal<string>(referenceText, after |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
                assertAdvanced
                    (before |> JournalEntryExternalReference.modifiedAt) (after |> JournalEntryExternalReference.modifiedAt)
            }
            |> railroadWrapper
        finally
            match idToCleanUp |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-CR-6.2 a classification rule update setting the priority to the priority it already holds succeeds and modified-at advances`` () =
        let mutable idToCleanUp = None
        try
            result {
                let! createPayload =
                    ({ classificationRuleName = "SYS-6.1 same priority rule"
                       claimantAtMatch = ClassificationContracts.ClassificationClaimantInput.Account "F-5650"
                       priority = 37
                       ruleGroups =
                         [ { connector = "And"
                             chainOne =
                               ({ chain = [ ClassificationContracts.FieldMatchContract.Source "SYS-6.1SamePriority" ] }
                                : ClassificationContracts.FieldMatchChainContract)
                             chainTwo = None } ] } : ClassificationContracts.NewClassificationRuleInput)
                    |> Json.toJson
                let! createdPayload = routeUiCommandForTesting "Classification" "NewClassificationRule" [] createPayload
                let! created = Json.fromJson<ClassificationContracts.ClassificationRuleReturn> createdPayload
                idToCleanUp <- Some(created.classificationRuleId |> ClassificationRuleId.fromGuid)
                System.Threading.Thread.Sleep(10)
                let! updatePayload =
                    ({ classificationRuleId = created.classificationRuleId
                       classificationRuleNameUpdate = NoChange
                       claimantAtMatchUpdate = NoChange
                       priorityUpdate = SetTo 37
                       ruleGroupsUpdate = NoChange
                       isActiveUpdate = NoChange } : ClassificationContracts.UpdateClassificationRuleInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "Classification" "UpdateClassificationRule" [] updatePayload
                let! byIdPayload =
                    ({ classificationRuleId = created.classificationRuleId }
                     : ClassificationContracts.FetchClassificationRuleByIdInput)
                    |> Json.toJson
                let! refetchedPayload = routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [] byIdPayload
                let! refetched = Json.fromJson<ClassificationContracts.ClassificationRuleReturn> refetchedPayload
                Assert.Equal(37, refetched.priority)
                assertAdvanced created.modifiedAt refetched.modifiedAt
            }
            |> railroadWrapper
        finally
            cleanUpClassificationRuleId idToCleanUp |> ignore
