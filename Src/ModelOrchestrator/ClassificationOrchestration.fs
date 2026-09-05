module ModelOrchestrator.ClassificationOrchestration

open System

open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open Model
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryLine
open Model.Ledger.AccountComponent
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.Json
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open Utilities.FieldUpdate
open Model.DataIngestion.Classification.ClassificationRuleComponent
open Model.DataIngestion.Classification.ClassificationRuleGroup
open Model.DataIngestion.Classification.FieldMatchChain

let private confirmAccount
    (context: Context.Context)
    (accountId: AccountId)
    : Result<unit, AppError> =
    let uuid = accountId |> AccountId.value
    let confirmed = uuid |> LookupCache.accountIdToCode.fetch context // we don't need the code. we just want to know that the accountId exists
    match confirmed with
    | Ok _ -> Ok ()
    | Error (DalResultantRowsDidntMatchExpectation _) -> Error (AccountIdDoesntMatch uuid)
    | Error e -> Error e

let private confirmPaymentAgreement
    (context: Context.Context)
    (paymentAgreementId: CashFlowComponent.PaymentAgreementId)
    : Result<unit, AppError> =
    let confirmed = paymentAgreementId |> PaymentAgreement.fetchById context
    match confirmed with
    | Ok _ -> Ok ()
    | Error (DalResultantRowsDidntMatchExpectation _) ->
        let uuid = paymentAgreementId |> PaymentAgreementId.value
        Error (CashflowPaymentAgreementIdDoesntExist uuid)
    | Error e -> Error e

let private confirmClassificationClaimant
    (context: Context.Context)
    (classificationClaimant: ClassificationClaimant)
    : Result<unit, AppError> =
    match classificationClaimant with
    | Account accountId -> accountId |> confirmAccount context
    | PaymentAgreement paymentAgreementId -> paymentAgreementId |> confirmPaymentAgreement context

let private confirmFieldMatchChain
    (fieldMatchChain: FieldMatchChain)
    : Result<unit, AppError> =
    let chain = fieldMatchChain |> FieldMatchChain.chain
    if chain |> List.isEmpty then Error IngestionFieldMatchChainEmpty else Ok ()
    
let private confirmRuleGroup
    (ruleGroup: ClassificationRuleGroup)
    : Result<unit, AppError> = result {
        do! ruleGroup |> chainOne |> confirmFieldMatchChain
        do! match ruleGroup |> chainTwo with
            | None -> Ok ()
            | Some x -> x |> confirmFieldMatchChain
        return ()
    }
    
let private confirmRuleGroups
    (ruleGroups: ClassificationRuleGroup list)
    : Result<unit, AppError> = 
    if ruleGroups |> List.isEmpty then Error IngestionClassificationRuleGroupsEmpty
    else
        ruleGroups
        |> List.map(confirmRuleGroup)
        |> convertListOfResultsToResultsList
        |> Result.map ignore

let createNewClassificationRule
    (context: Context.Context)
    (classificationRuleName: ClassificationRuleName)
    (classificationClaimant: ClassificationClaimant)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
    : Result<ClassificationRule.ClassificationRule, AppError> = 
    let classificationRuleId = ClassificationRuleId.create()
    let instant = context |> Context.getInitiationInstant
    let newRule =
        ClassificationRule.create
            classificationRuleId
            classificationRuleName
            classificationClaimant
            priority
            ruleGroups
            true // no new rules that are already inactive
            instant
            instant
    result {
        do! ruleGroups |> confirmRuleGroups
        do! classificationClaimant |> confirmClassificationClaimant context
        do! newRule |> ClassificationRule.insertNewToDb context
        return newRule
    }

let fetchRulesFiltered
    (context: Context.Context)
    (filter: ClassificationRuleFilter)
    (sort: FetchSortClassificationRule option)
    : Result<ClassificationRule.ClassificationRule list, AppError> =
    result {
        let sourcePredicate = """
            EXISTS (
                SELECT 1
                FROM jsonb_array_elements(cr.rule_groups) AS rg,
                     jsonb_array_elements(
                        CASE
                            WHEN rg.value -> 'chainTwo' IS NOT NULL
                                AND rg.value -> 'chainTwo' != 'null'::jsonb
                            THEN (rg.value -> 'chainOne' -> 'chain') || (rg.value -> 'chainTwo' -> 'chain')
                            ELSE rg.value -> 'chainOne' -> 'chain'
                        END
                ) AS fm
                WHERE fm.value ->> 'Case' = 'Source'
                AND fm.value -> 'Fields' ->> 0 LIKE @source_like
            )
            """
        
        let activeClause =
            match filter.activeOnly with
            | true -> "and cr.is_active = true"
            | false -> ""
            
        let orderBy =
            match sort with
            | None -> None
            | Some AccountCodeAsc -> Some "a.code asc"
            | Some AccountCodeDesc -> Some "a.code desc"
            | Some PriorityAsc -> Some "cr.priority asc"
            | Some PriorityDesc -> Some "cr.priority desc"
        
        // left joins: a rule claims either an account or a payment agreement, so the other column is always null
        let join =
            Some [ "left join ledger.account a on cr.account_at_match = a.unique_id"
                   "left join cashflow.payment_agreement pa on cr.payment_agreement_at_match = pa.unique_id" ]
            
        let whereClausesAndParams =
            [ filter.ruleId
              |> Option.map(fun x ->
                  ("cr.unique_id = @rule_id", { name = "@rule_id"; value = UniqueId(x |> ClassificationRuleId.value) }))
        
              filter.nameLike
              |> Option.map(fun x ->
                  let ruleName = x |> ClassificationRuleName.value
                  ("cr.rule_name like @rule_name",
                   { name = "@rule_name"; value = CharString $"%%{ruleName}%%"}))
        
              filter.accountAtMatch
              |> Option.map(fun x ->
                  ("cr.account_at_match = @account_at_match",
                   { name = "@account_at_match"; value = UniqueId(x |> AccountId.value) }))

              filter.paymentAgreementAtMatch
              |> Option.map(fun x ->
                  ("cr.payment_agreement_at_match = @payment_agreement_at_match",
                   { name = "@payment_agreement_at_match"; value = UniqueId(x |> PaymentAgreementId.value) }))

              filter.sourceLike
              |> Option.map(fun x ->
                  (sourcePredicate, { name = "@source_like"; value = CharString $"%%{x}%%" }))
            ]
            |> List.choose id
        let whereClauses = whereClausesAndParams |> List.map fst |> String.concat $" and {Environment.NewLine}"        
        let predicate = Some $"""
            {if whereClausesAndParams |> List.isEmpty then "1 = 1" else whereClauses}
            {activeClause}
        """
        let limit = None
        let parameters = whereClausesAndParams |> List.map snd
        return!
            ClassificationRule.readRowsFromDb context join predicate limit parameters orderBy AnyQuantityIsAcceptable
    }

let updateLineWithAccountMatch
    (context: Context.Context)
    (accountId: AccountId)
    (ruleId: ClassificationRuleId)
    (candidate: MatchCandidate)
    : Result<unit, AppError> =
    let idOption = Some accountId // we can trust this because the DB has a foreign key constraint between ingestion.classification_rule and ledger.account 
    let idUpdate = FieldUpdate.SetTo idOption
    let ruleId = Some ruleId
    let ruleUpdate = FieldUpdate.SetTo ruleId
    candidate.lineIdOfCandidate
    |> updateAccountAndRuleId context idUpdate ruleUpdate 
    |> Result.map ignore

let updateLineWithPaymentMatch
    (context: Context.Context)
    (paymentAgreementId: PaymentAgreementId)
    (ruleId: ClassificationRuleId)
    (candidate: MatchCandidate)
    : Result<unit, AppError> =
    let idOption = Some paymentAgreementId // we can trust this because the DB has a foreign key constraint between ingestion.classification_rule and ledger.account 
    let idUpdate = FieldUpdate.SetTo idOption
    let ruleId = Some ruleId
    let ruleUpdate = FieldUpdate.SetTo ruleId
    candidate.lineIdOfCandidate
    |> updatePaymentAgreementAndRuleId context idUpdate ruleUpdate 
    |> Result.map ignore

let updateLineWithMatch
    (context: Context.Context)
    (prioritizedMatch: PrioritizedMatch)
    (candidate: MatchCandidate)
    : Result<unit, AppError> =
    match prioritizedMatch.accountId, prioritizedMatch.paymentAgreementId with
    | Some x, None -> candidate |> updateLineWithAccountMatch context x prioritizedMatch.ruleId
    | None, Some x -> candidate |> updateLineWithPaymentMatch context x prioritizedMatch.ruleId
    | _ ->
        let ruleUuid = prioritizedMatch.ruleId |> ClassificationRuleId.value
        let accountUuid = prioritizedMatch.accountId |> Option.map AccountId.value
        let paymentAgreementUuid = prioritizedMatch.paymentAgreementId |> Option.map PaymentAgreementId.value
        Error (IngestionClassificationRuleInvalidClaimant(ruleUuid, accountUuid, paymentAgreementUuid))

let updateDbLinesFromResultsList
    (context: Context.Context)
    (results: ClassificationResult list)
    : Result<unit, AppError> =
    result {
        // first update the line
        let! _ =
            results
            |> List.map (fun result ->
                    let candidate = result.candidate
                    match result.outcome with
                    | NoMatch -> Ok ()
                    | OneMatch prioritizedMatch -> candidate |> updateLineWithMatch context prioritizedMatch
                    | ManyMatchesClearWinner (winner, _) -> candidate |> updateLineWithMatch context winner
                    | ManyMatchesTied _ -> Ok () // no line update today
                )
            |> convertListOfResultsToResultsList
        // now update the header and status
        return ()
        }
    
/// classifyMatchCandidatesAndUpdateLines only updates the lines. StageEntryOrchestration owns making sure that status
/// transitions are viable. This runs the risk of an "orphan" line update if subsequent updates to the entry or audit
/// table fail. But this should all be under one transaction. Caveat emptor if you use individual transactions for this.
let classifyMatchCandidatesAndUpdateLines
    (context: Context.Context)
    (candidates: MatchCandidate list)
    : Result<ClassificationResult list, AppError> =
    result {
        let ruleFilter =  {
            ruleId = None
            nameLike = None
            accountAtMatch = None
            paymentAgreementAtMatch = None
            sourceLike = None
            activeOnly = true }
        let! rules = fetchRulesFiltered context ruleFilter None
        let classificationResults = Classifier.classify rules candidates
        do! classificationResults |> updateDbLinesFromResultsList context
        return classificationResults
    }
    
// both claimant columns are written on every change so any update must write a value to both and one must always be
// null
let private classificationClaimantToJointUpdates
    (classificationClaimantUpdate: FieldUpdate<ClassificationClaimant>)
    : (string * QueryParameter) option * (string * QueryParameter) option =
    match classificationClaimantUpdate with
    | NoChange -> None, None
    | SetTo claimant ->
        let accountUuid, paymentAgreementUuid =
            match claimant with
            | Account accountId ->
                accountId |> AccountId.value |> Some, None
            | PaymentAgreement paymentAgreementId ->
                None, paymentAgreementId |> PaymentAgreementId.value |> Some
        Some ("account_at_match = @account_at_match",
                { name = "@account_at_match"; value = NullableUniqueId(accountUuid) }),
        Some ("payment_agreement_at_match = @payment_agreement_at_match",
                { name = "@payment_agreement_at_match"; value = NullableUniqueId(paymentAgreementUuid) })

let updateClassificationRule
    (context: Context.Context)
    (classificationRuleNameUpdate: FieldUpdate<ClassificationRuleName>)
    (classificationClaimantUpdate: FieldUpdate<ClassificationClaimant>)
    (priorityUpdate: FieldUpdate<int>)
    (ruleGroupsUpdate: FieldUpdate<ClassificationRuleGroup list>)
    (isActiveUpdate: FieldUpdate<bool>)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule.ClassificationRule, AppError> =
    let uuid = classificationRuleId |> ClassificationRuleId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    result {
        do! match classificationClaimantUpdate with
            | NoChange -> Ok ()
            | SetTo x -> x |> confirmClassificationClaimant context
        do! match ruleGroupsUpdate with
            | NoChange -> Ok ()
            | SetTo x -> x |> confirmRuleGroups
        let! groupStr = // do this up here because it's a pain in the ass to do it down in the updates block
            match ruleGroupsUpdate with
            | NoChange -> Ok ""
            | SetTo x -> x |> Json.toJson<ClassificationRuleGroup list> 
        let accountAtMatchUpdate, paymentAtMatchUpdate =
            classificationClaimantUpdate |> classificationClaimantToJointUpdates
        let updates =
            [
                  classificationRuleNameUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("rule_name = @rule_name",
                       { name = "@rule_name"; value = CharString(n |> ClassificationRuleName.value) }))

                  priorityUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("priority = @priority",
                       { name = "@priority"; value = Integer(n) }))

                  ruleGroupsUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun _ ->
                      ("rule_groups = @rule_groups",
                       { name = "@rule_groups"; value = Jsonb(groupStr) }))

                  isActiveUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("is_active = @is_active",
                       { name = "@is_active"; value = Boolean(n) }))
                  
                  accountAtMatchUpdate
                  paymentAtMatchUpdate
            ]
            |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ", "
        let parameters = baseParams @ (updates |> List.map snd)
        let query =
            $"""
            UPDATE ingestion.classification_rule
            set
                {setClauses},
                modified_at = @modified
            WHERE unique_id = @unique_id;
        """
        do! if updates.IsEmpty then Error(IngestionClassificationRuleUpdateNoOp) else Ok()
        let! () = executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! classificationRuleId |> ClassificationRule.fetchById context
    }
