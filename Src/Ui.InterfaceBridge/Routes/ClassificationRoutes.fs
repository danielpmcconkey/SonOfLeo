module Ui.InterfaceBridge.Routes.ClassificationRoutes

open App.Utility.FieldUpdate
open App.Utility.Json
open App.Utility.Result
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.ClassificationOrchestration
open Ui.InterfaceBridge.BoundaryConverters.CashFlowLookupConverters
open Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts
open Ui.InterfaceBridge.BoundaryConverters.ClassificationFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.SharedContracts

let private newClassificationRule payload _ =
    let context = Context.create NoTransaction ClassificationNewRule
    result {
        let! input = Json.fromJson<NewClassificationRuleInput> payload
        let! name = input.classificationRuleName |> ClassificationRuleName.create
        let! claimantAtMatch =
            input.claimantAtMatch |> ``convert [ClassificationClaimantInput] to [ClassificationClaimant]`` context
        let priority = input.priority
        let! ruleGroups = input.ruleGroups |> ``convert [ClassificationRuleGroupContract list] to [ClassificationRuleGroup list]``
        let! model =
            createNewClassificationRule
                context
                name
                claimantAtMatch
                priority
                ruleGroups
        let! returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]`` context
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private updateClassificationRule payload _ =
    let context = Context.create NoTransaction ClassificationUpdateRule
    result {
        let! input = Json.fromJson<UpdateClassificationRuleInput> payload
        let classificationRuleId = input.classificationRuleId |> ClassificationRuleId.fromGuid
        let! classificationRuleNameUpdate =
            input.classificationRuleNameUpdate |> convertFieldUpdateToNewTypeFallible ClassificationRuleName.create
        let! claimantAtMatchUpdate =
            input.claimantAtMatchUpdate
            |> convertFieldUpdateToNewTypeFallible
                (``convert [ClassificationClaimantInput] to [ClassificationClaimant]`` context)
        let priorityUpdate = input.priorityUpdate
        let! ruleGroupsUpdate =
            input.ruleGroupsUpdate
            |> convertFieldUpdateToNewTypeFallible
                ``convert [ClassificationRuleGroupContract list] to [ClassificationRuleGroup list]``
        let isActiveUpdate = input.isActiveUpdate
        let! model =
            classificationRuleId
            |> updateClassificationRule context classificationRuleNameUpdate claimantAtMatchUpdate
                   priorityUpdate ruleGroupsUpdate isActiveUpdate
        let! returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]`` context
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private fetchClassificationRuleById payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRuleByIdInput> payload
        let classificationRuleId = input.classificationRuleId |> ClassificationRuleId.fromGuid
        let! model = classificationRuleId |> ClassificationRule.fetchById context
        let! returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]`` context
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private fetchClassificationRuleByName payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRuleByNameInput> payload
        let! name = input.classificationRuleName |> ClassificationRuleName.create
        let! model = name |> ClassificationRule.fetchByName context
        let! returnVal = model |> ``convert [ClassificationRule] to [ClassificationRuleReturn]`` context
        return! Json.toJson<ClassificationRuleReturn> returnVal
    }

let private fetchClassificationRuleFiltered payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRuleFilteredInput> payload
        let! filter = input.filter |> ``convert [ClassificationRuleFilterInput] to [ClassificationRuleFilter]`` context
        let! model = fetchRulesFiltered context filter input.sort
        let! returnVal = model |> ``convert [ClassificationRule list] to [ClassificationRuleReturn list]`` context
        return! Json.toJson<ClassificationRuleReturn list> returnVal
    }

let private fetchClassificationRun payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchClassificationRunInput> payload
        let runId = input.runId |> ClassificationRunId.fromGuid
        let! matchesWithRules = runId |> fetchRunMatchesWithRules context
        let! matches =
            matchesWithRules
            |> List.map (fun (ruleMatch, rule) ->
                ruleMatch |> ``convert [RuleMatch] to [RuleMatchReturn]`` context rule)
            |> convertListOfResultsToResultsList
        let sorted =
            matches
            |> List.sortBy (fun ruleMatch ->
                ruleMatch.stageEntryLineId, ruleMatch.priority, ruleMatch.classificationRuleName)
        let returnVal : ClassificationRunReturn = { runId = input.runId; matches = sorted }
        return! Json.toJson<ClassificationRunReturn> returnVal
    }

let private classifyAccounts _ _ =
    runCommandRouteAndAutoCompleteTransaction ClassifyAccounts (fun context ->
        result {
            let! classificationResult = StageEntryOrchestration.classifyAccounts context
            let! converted =
                classificationResult
                |> ``convert [AccountClassificationResult] to [AccountClassificationResultReturn]`` context
            return! Json.toJson<AccountClassificationResultReturn> converted })

let private createPaymentAgreementLink payload _ =
    runCommandRouteAndAutoCompleteTransaction CreatePaymentAgreementLink (fun context ->
        result {
            let! input = Json.fromJson<CreatePaymentAgreementLinkInput> payload
            let! paymentAgreementId =
                input.paymentAgreementName |> ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context
            let stageEntryLineId =
                input.stageEntryLineId |> StageEntryLineId.fromGuid
            let! link =
                stageEntryLineId |> CashFlowOps.constructNewPaymentAgreementLinkAndPersist context paymentAgreementId
            let! converted = link |> ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]`` context
            return! Json.toJson<PaymentAgreementLinkReturn> converted
        })

let private updatePaymentAgreementLink payload _ =
    runCommandRouteAndAutoCompleteTransaction UpdatePaymentAgreementLink (fun context ->
        result {
            let! input = Json.fromJson<UpdatePaymentAgreementLinkInput> payload
            let! fieldUpdates =
                input |> ``convert [UpdatePaymentAgreementLinkInput] to [PaymentAgreementLinkFieldUpdates]`` context
            let! link = fieldUpdates |> PaymentAgreementLink.update context
            let! converted = link |> ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]`` context
            return! Json.toJson<PaymentAgreementLinkReturn> converted
        })

let private deletePaymentAgreementLink payload _ =
    runCommandRouteAndAutoCompleteTransaction DeletePaymentAgreementLink (fun context ->
        result {
            let! input = Json.fromJson<DeletePaymentAgreementLinkInput> payload
            let linkId = input.paymentAgreementLinkId |> PaymentAgreementLinkId.fromGuid
            let! link = linkId |> PaymentAgreementLink.fetchById context
            do! linkId |> PaymentAgreementLink.delete context
            let! converted = link |> ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]`` context
            return! Json.toJson<PaymentAgreementLinkReturn> converted
        })

let private classifyPaymentAgreements _ _ =
    runCommandRouteAndAutoCompleteTransaction ClassifyPaymentAgreements (fun context ->
        result {
            let! classificationResult = CashFlowOps.classifyPaymentAgreements context
            let! converted =
                classificationResult
                |> ``convert [PaymentAgreementClassificationResult] to [PaymentAgreementClassificationResultReturn]`` context
            return! Json.toJson<PaymentAgreementClassificationResultReturn> converted
        })

let classificationDomainCommandRoutes: CommandRoute list =
    [
      { domain = "Classification"
        verb = "NewClassificationRule"
        description = "Create a new ClassificationRule."
        inputContract = typeof<NewClassificationRuleInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = newClassificationRule }

      { domain = "Classification"
        verb = "FetchClassificationRuleById"
        description = "Fetch a specific ClassificationRule by providing its Id."
        inputContract = typeof<FetchClassificationRuleByIdInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = fetchClassificationRuleById }

      { domain = "Classification"
        verb = "FetchClassificationRuleByName"
        description = "Fetch a specific ClassificationRule by providing its name."
        inputContract = typeof<FetchClassificationRuleByNameInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = fetchClassificationRuleByName }

      { domain = "Classification"
        verb = "FetchClassificationRuleFiltered"
        description = "Fetch a whichever rules match a specific combination of filter inputs. This is more computationally expensive than the more basic FetchByX."
        inputContract = typeof<FetchClassificationRuleFilteredInput>.Name
        outputContract = typeof<ClassificationRuleReturn list>.Name
        handler = fetchClassificationRuleFiltered }

      { domain = "Classification"
        verb = "UpdateClassificationRule"
        description = "Update any of a ClassificationRule's fields."
        inputContract = typeof<UpdateClassificationRuleInput>.Name
        outputContract = typeof<ClassificationRuleReturn>.Name
        handler = updateClassificationRule }

      { domain = "Classification"
        verb = "FetchClassificationRun"
        description = "Read one classification run's matches with each claimed entity resolved to an account code or a payment agreement name, and each rule to its name and current priority. Serves review of both the account and the payment agreement classification steps."
        inputContract = typeof<FetchClassificationRunInput>.Name
        outputContract = typeof<ClassificationRunReturn>.Name
        handler = fetchClassificationRun }

      { domain = "Classification"
        verb = "ClassifyAccounts"
        description = "Run the account classification rules over every unresolved staged entry line, write the account where a rule wins outright, and update each entry's status. Returns the run, its results, and the entries an operator may still need to act on."
        inputContract = typeof<Ui.InterfaceBridge.InterfaceContracts.SharedContracts.NoInput>.Name
        outputContract = typeof<AccountClassificationResultReturn>.Name
        handler = classifyAccounts }

      { domain = "Classification"
        verb = "CreatePaymentAgreementLink"
        description = "Link a staged entry line to the payment agreement it satisfies, where classification left the decision to the operator."
        inputContract = typeof<CreatePaymentAgreementLinkInput>.Name
        outputContract = typeof<PaymentAgreementLinkReturn>.Name
        handler = createPaymentAgreementLink }

      { domain = "Classification"
        verb = "UpdatePaymentAgreementLink"
        description = "Repoint an existing linkage at a different payment agreement."
        inputContract = typeof<UpdatePaymentAgreementLinkInput>.Name
        outputContract = typeof<PaymentAgreementLinkReturn>.Name
        handler = updatePaymentAgreementLink }

      { domain = "Classification"
        verb = "DeletePaymentAgreementLink"
        description = "Remove a linkage outright, for a staged entry line that is not an obligation at all. Returns the row as it stood before deletion. This is a hard delete, not a void."
        inputContract = typeof<DeletePaymentAgreementLinkInput>.Name
        outputContract = typeof<PaymentAgreementLinkReturn>.Name
        handler = deletePaymentAgreementLink }

      { domain = "Classification"
        verb = "ClassifyPaymentAgreements"
        description = "Match staged entry lines to payment agreements, record the linkage for each uncontested match, create the payments that follow from it, and return both decision logs alongside the open instances."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<PaymentAgreementClassificationResultReturn>.Name
        handler = classifyPaymentAgreements }
    ]
