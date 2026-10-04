module Tests.Integrated.InterfaceBridge.AccountNamesInPayloads

open System
open System.Text.Json.Nodes
open Microsoft.FSharp.Reflection
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module AccountContracts = Ui.InterfaceBridge.InterfaceContracts.AccountContracts
module CashFlowContracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts
module ClassificationContracts = Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts
module JournalContracts = Ui.InterfaceBridge.InterfaceContracts.JournalContracts
module ReportsContracts = Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

(* REQ-NGUI-1.6, in two halves.

   Every return contract: each record of the interface contracts that is not an input, and that has a field named
   `code` or ending in `Code`, is found by reflection. Each such field must have its name field beside it (`name` or
   `accountName` beside `code`, otherwise the same stem ending in `Name`), and the set of such contracts must be the
   set this test drives a route for, so a new contract carrying a code fails here until a route for it is added.

   Every route: for each of those contracts a route that returns it is called, and every code anywhere in the payload
   it returns, in any role, must carry the name of the account that code identifies, as stored. Each route must return
   at least one code, with a value, in each role its contract has.

   Some payloads need data the fixture lacks: an account deactivated yesterday still holding money (the balance sheet
   integrity report), and an unclassified staged entry with a rule that claims it for an account (the account
   classification run). These are committed, because the routes read through their own connections, and deleted in a
   finally. A fixture-only account classification run classifies nothing, so the run here touches only this test's
   entry; the test checks that. *)

/// The contract types carrying codes, and the code fields each carries, that this test calls a route for.
let private driven : Map<string, string list> =
    Map.ofList
        [ "AccountClaimantReturn", [ "code" ]
          "AccountReturn", [ "code"; "parentCode" ]
          "AccountActivityReturn", [ "accountCode"; "accountParentCode" ]
          "AccountBalanceReturn", [ "accountCode" ]
          "PaymentAgreementReturn", [ "debitAccountCode"; "creditAccountCode" ]
          "ProjectedAccountReturn", [ "accountCode" ]
          "PrioritizedMatchReturn", [ "accountCode" ]
          "StageEntryLineReturn", [ "accountCode" ]
          "JournalEntryLineReturn", [ "accountCode" ]
          "TrialBalanceReturnRow", [ "accountCode" ]
          "DeactivatedAccountWithBalanceReturnRow", [ "accountCode" ]
          "ReconciliationReturnRow", [ "accountCode" ]
          "PeriodActivityAccountReturnRow", [ "accountCode" ]
          "PrePostingLineReturnRow", [ "accountCode" ] ]

let private isCodeField (name: string) = name = "code" || name.EndsWith "Code"

let private nameFieldsFor (codeField: string) =
    if codeField = "code" then [ "name"; "accountName" ] else [ codeField.Substring(0, codeField.Length - 4) + "Name" ]

/// Every record of the interface contracts that is not an input, with its code fields, and those of the code fields
/// with no name field beside them.
let private contractsCarryingCodes () =
    typeof<AccountContracts.AccountReturn>.Assembly.GetTypes()
    |> Array.filter (fun t ->
        not (isNull t.FullName)
        && t.FullName.StartsWith "Ui.InterfaceBridge.InterfaceContracts."
        && FSharpType.IsRecord(t, true)
        && not (t.Name.Contains "Input"))
    |> Array.choose (fun t ->
        let fields = FSharpType.GetRecordFields(t, true) |> Array.map (fun p -> p.Name)
        let codes = fields |> Array.filter isCodeField |> List.ofArray
        if codes.IsEmpty then None
        else
            let unnamed = codes |> List.filter (fun c -> nameFieldsFor c |> List.exists (fun n -> fields |> Array.contains n) |> not)
            Some(t.Name, codes, unnamed))
    |> List.ofArray

let private textOf (node: JsonNode) =
    match node with
    | null -> None
    | :? JsonValue as v -> Some(v.GetValue<string>())
    | other -> Some(other.ToJsonString())

/// Every code field anywhere in the payload: its field, its value, the name field found beside it, and that name.
let rec private codesIn (node: JsonNode) : (string * string option * string option * string option) list =
    match node with
    | :? JsonObject as o ->
        let props = o |> Seq.map (fun kv -> kv.Key, kv.Value) |> List.ofSeq
        let own =
            props
            |> List.filter (fun (key, _) -> isCodeField key)
            |> List.map (fun (key, value) ->
                let nameField = nameFieldsFor key |> List.tryFind (fun n -> o.ContainsKey n)
                key, textOf value, nameField, nameField |> Option.bind (fun n -> textOf o[n]))
        own @ (props |> List.collect (fun (_, value) -> if isNull value then [] else codesIn value))
    | :? JsonArray as a -> a |> Seq.filter (isNull >> not) |> Seq.collect codesIn |> List.ofSeq
    | _ -> []

let private toJsonOf<'a> (input: 'a) = input |> Json.toJson<'a>

[<Collection("SharedTestData")>]
type AccountNamesInPayloadsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-NGUI-1.6 for each route whose return payload carries an account code in any role (the account itself, its parent, an agreement's debit or credit account), the payload carries beside each code the name of the account that code identifies`` () =
        // every return contract carrying a code has a name beside each code, and is one this test drives
        let carrying = contractsCarryingCodes ()
        Assert.Equal<(string * string list) list>([], carrying |> List.filter (fun (_, _, unnamed) -> not unnamed.IsEmpty) |> List.map (fun (t, _, u) -> t, u))
        Assert.Equal<Map<string, string list>>(driven, carrying |> List.map (fun (t, codes, _) -> t, codes) |> Map.ofList)

        let today = Calendar.today ()
        let tag = Guid.NewGuid().ToString("N")
        let code = $"N16-{tag.Substring(0, 6)}"
        let accountIdOf c =
            fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = c) |> Account.accountId
        let testBank =
            fixture.Data.ingestionSources
            |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
        let mutable accountId = None
        let mutable journalEntryId = None
        let mutable headerId = None
        let mutable ruleId = None
        let committed body = runCommandRouteAndAutoCompleteTransaction FetchOnly body
        try
            result {
                // an expense account active until yesterday, holding 25.00 from an entry dated yesterday
                let! _, newAccountId =
                    committed (fun context ->
                        createTestAccountFromPrimitives
                            context code "NGUI-1.6 deactivated expense" "Expense" (today.PlusDays(-30)) (Some(today.PlusDays(-1)))
                            (Some "OperatingExpense") (Some(accountIdOf "F-5000")) None)
                accountId <- Some newAccountId
                let! _, newEntryId =
                    committed (fun context ->
                        createTestJournalEntryFromPrimitives
                            context $"NGUI-1.6 {tag}" None (today.PlusDays(-1))
                            [ (newAccountId, 25.00M, "Debit", None); (accountIdOf "F-1280", 25.00M, "Credit", None) ] [] [])
                journalEntryId <- Some newEntryId
                // an Ingested staged entry with no accounts, and an account rule claiming it
                let! staged =
                    committed (fun context ->
                        createStageEntryForTest
                            context "/tmp/ngui-1-6-test.dat" $"NGUI-1.6 {tag}" (Guid.NewGuid().ToString()) testBank today
                            [ (40.00M, "Debit", None, None, None); (40.00M, "Credit", None, None, None) ]
                            [ (None, "Ingested", Clock.now (), "StageIngestion") ])
                headerId <- Some(staged |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                let stagedLineIds =
                    staged |> StageEntryOrchestration.seLines |> List.map (StageEntryLine.stageEntryLineId >> StageEntryLineId.value) |> Set.ofList
                let! rulePayload =
                    ({ classificationRuleName = $"NGUI-1.6 rule {tag}"
                       claimantAtMatch = ClassificationContracts.ClassificationClaimantInput.Account "F-5350"
                       priority = 37
                       ruleGroups =
                         [ { connector = "And"
                             chainOne =
                               ({ chain = [ ClassificationContracts.FieldMatchContract.Description tag ] }
                                : ClassificationContracts.FieldMatchChainContract)
                             chainTwo = None } ] } : ClassificationContracts.NewClassificationRuleInput)
                    |> toJsonOf
                    |> Result.bind (routeUiCommandForTesting "Classification" "NewClassificationRule" [])
                let! rule = Json.fromJson<ClassificationContracts.ClassificationRuleReturn> rulePayload
                ruleId <- Some(rule.classificationRuleId |> Business.FinancialServices.Classification.ClassificationComponent.ClassificationRuleId.fromGuid)

                // the account classification run: it must have classified only this test's entry
                let! classifyPayload = routeUiCommandForTesting "Classification" "ClassifyAccounts" [] ""
                let! classified = Json.fromJson<ClassificationContracts.AccountClassificationResultReturn> classifyPayload
                Assert.Equal<Set<Guid>>(
                    stagedLineIds,
                    classified.classificationResults |> List.map (fun r -> r.candidate.stageEntryLineId) |> Set.ofList)

                let report name (input: string) = routeReportingCommandForTesting name [] input
                let! payloads =
                    [ "AccountReturn",
                      ({ code = "F-5311" } : AccountContracts.AccountFetchByCodeInput) |> toJsonOf
                      |> Result.bind (routeUiCommandForTesting "Account" "FetchByCode" [])
                      "AccountActivityReturn",
                      ({ filter =
                           { accountCode = Some code; temporalFilter = None; source = None; accountType = None
                             accountSubtype = None; accountParentCode = None; journalEntryId = None; amount = None
                             description = None; unVoidedOnly = true }
                         sort = None } : AccountContracts.AccountActivityFetchInput) |> toJsonOf
                      |> Result.bind (routeUiCommandForTesting "Account" "FetchActivity" [])
                      "AccountBalanceReturn",
                      ({ codes = [ "F-1280"; code ]; asOf = None } : AccountContracts.AccountBalanceFetchByAccountListInput) |> toJsonOf
                      |> Result.bind (routeUiCommandForTesting "Account" "FetchBalances" [])
                      "AccountClaimantReturn",
                      ({ classificationRuleId = rule.classificationRuleId } : ClassificationContracts.FetchClassificationRuleByIdInput)
                      |> toJsonOf |> Result.bind (routeUiCommandForTesting "Classification" "FetchClassificationRuleById" [])
                      "PaymentAgreementReturn",
                      ({ agreementName = "Fixture agreement A" } : CashFlowContracts.FetchAgreementSummaryInput) |> toJsonOf
                      |> Result.bind (routeUiCommandForTesting "CashFlow" "FetchAgreementSummary" [])
                      "ProjectedAccountReturn",
                      ({ projectionHorizonInDays = 60 } : CashFlowContracts.ProjectCashFlowInput) |> toJsonOf
                      |> Result.bind (routeUiCommandForTesting "CashFlow" "ProjectCashFlow" [])
                      "PrioritizedMatchReturn", Ok classifyPayload
                      "StageEntryLineReturn", Ok classifyPayload
                      "JournalEntryLineReturn",
                      ({ accountCode = code; nonVoidedOnly = true } : JournalContracts.JournalEntryFetchLinesByAccountInput) |> toJsonOf
                      |> Result.bind (routeUiCommandForTesting "JournalEntry" "FetchLinesByAccount" [])
                      "TrialBalanceReturnRow",
                      ({ asOf = { asOf = today }; reportOutput = ReportsContracts.DataOnly } : ReportsContracts.TrialBalanceReportInput)
                      |> toJsonOf |> Result.bind (report "TrialBalance")
                      "DeactivatedAccountWithBalanceReturnRow",
                      ({ asOf = { asOf = today }; reportOutput = ReportsContracts.DataOnly } : ReportsContracts.BalanceSheetIntegrityInput)
                      |> toJsonOf |> Result.bind (report "BalanceSheetIntegrity")
                      "ReconciliationReturnRow",
                      ({ rows = [ { accountCode = "F-1280"; externalBalance = 0.00M; asOf = today } ] } : ReportsContracts.ReconciliationInput)
                      |> toJsonOf |> Result.bind (report "Reconciliation")
                      "PeriodActivityAccountReturnRow",
                      ({ beginDate = today.PlusDays(-30); endDate = today; reportOutput = ReportsContracts.DataOnly } : ReportsContracts.PeriodActivityInput)
                      |> toJsonOf |> Result.bind (report "PeriodActivity")
                      "PrePostingLineReturnRow",
                      ({ reportOutput = ReportsContracts.DataOnly } : ReportsContracts.PrePostingReviewInput)
                      |> toJsonOf |> Result.bind (report "PrePostingReview") ]
                    |> List.map (fun (contract, payload) -> payload |> Result.map (fun p -> contract, p))
                    |> convertListOfResultsToResultsList
                let! accounts = Account.fetchAll (Context.create NoTransaction FetchOnly) false
                let nameOf c =
                    accounts |> List.tryFind (fun a -> a |> Account.code |> AccountCode.value = c) |> Option.map (Account.accountName >> AccountName.value)

                // every code in every payload carries the stored name of its account
                let wrong =
                    payloads
                    |> List.collect (fun (contract, payload) ->
                        codesIn (JsonNode.Parse payload)
                        |> List.choose (fun (field, codeValue, nameField, nameValue) ->
                            let expected = codeValue |> Option.bind nameOf
                            match codeValue, nameField with
                            | _, None -> Some $"{contract}: {field} {codeValue} has no name beside it"
                            | Some c, Some _ when expected.IsNone -> Some $"{contract}: {field} {c} names no stored account"
                            | _, Some n when nameValue <> expected -> Some $"{contract}: {field} {codeValue} carries {n} {nameValue}, not {expected}"
                            | _ -> None))
                Assert.Equal<string list>([], wrong)

                // each route returned at least one code, with a value, in each role its contract has
                let missing =
                    payloads
                    |> List.collect (fun (contract, payload) ->
                        let found =
                            codesIn (JsonNode.Parse payload)
                            |> List.filter (fun (_, codeValue, _, _) -> codeValue.IsSome)
                            |> List.map (fun (field, _, _, _) -> field)
                            |> Set.ofList
                        driven[contract] |> List.filter (fun field -> found.Contains field |> not) |> List.map (fun field -> $"{contract}.{field}"))
                Assert.Equal<string list>([], missing)
            }
            |> railroadWrapper
        finally
            [ Cleanup.cleanUpRuleMatchesOfRuleId ruleId
              Cleanup.cleanUpClassificationRuleId ruleId
              Cleanup.cleanUpStageEntryHeaderId headerId
              Cleanup.cleanUpJournalEntryId journalEntryId
              Cleanup.cleanUpAccountId accountId ]
            |> List.iter railroadWrapper
