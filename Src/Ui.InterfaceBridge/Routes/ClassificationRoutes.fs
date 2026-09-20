module Ui.InterfaceBridge.Routes.ClassificationRoutes

open App.Utility.FieldUpdate
open App.Utility.Json
open App.Utility.Result
open App.DataAccessLayer.DbTransaction
open App.Operation.Audit
open App.Session
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.ClassificationOrchestration
open Ui.InterfaceBridge.BoundaryConverters.ClassificationFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts

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
    ]
