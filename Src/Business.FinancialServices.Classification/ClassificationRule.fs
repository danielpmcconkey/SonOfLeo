module Business.FinancialServices.Classification.ClassificationRule

open NodaTime
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Utility.AppError
open App.Utility.Json.Json
open App.Utility.Result
open App.Session
open Business.FinancialServices.CashFlow
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationRuleGroup

/// ClassificationRule: The top-level classification rule. All groups must resolve to true for the rule to resolve to
/// true.
type ClassificationRule =
    private {
        classificationRuleId: ClassificationRuleId
        classificationRuleName: ClassificationRuleName
        classificationClaimant: ClassificationClaimant 
        priority: int // lower number wins when multiple rules match
        ruleGroups: ClassificationRuleGroup list
        isActive: bool
        createdAt: Instant
        modifiedAt: Instant
    }
        
let classificationRuleId (a: ClassificationRule) = a.classificationRuleId
let classificationRuleName (a: ClassificationRule) = a.classificationRuleName
let classificationClaimant (a: ClassificationRule) = a.classificationClaimant
let priority (a: ClassificationRule) = a.priority
let ruleGroups (a: ClassificationRule) = a.ruleGroups
let isActive (a: ClassificationRule) = a.isActive
let createdAt (a: ClassificationRule) = a.createdAt
let modifiedAt (a: ClassificationRule) = a.modifiedAt

let create
    (classificationRuleId: ClassificationRuleId)
    (classificationRuleName: ClassificationRuleName)
    (classificationClaimant: ClassificationClaimant)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
    (isActive: bool)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : ClassificationRule = {
        classificationRuleId = classificationRuleId
        classificationRuleName = classificationRuleName
        classificationClaimant = classificationClaimant
        priority = priority
        ruleGroups = ruleGroups
        isActive = isActive
        createdAt = createdAt
        modifiedAt = modifiedAt
    }
    
let persist (context: Context.Context) (classificationRule: ClassificationRule) : Result<unit, AppError> =
    let queryStatement =
        """
        insert into classification.classification_rule(
	        unique_id, rule_name, account_at_match, payment_agreement_at_match, 
            priority, rule_groups, is_active, created_at, modified_at)
        values (
	        @unique_id, 
            @rule_name, 
            @account_at_match, 
            @payment_agreement_at_match, 
            @priority, 
            @rule_groups, 
            @is_active, 
            @created_at, 
            @modified_at);"""
    let uuid = classificationRule.classificationRuleId |> ClassificationRuleId.value
    let ruleName = classificationRule.classificationRuleName |> ClassificationRuleName.value
    let accountId, paymentAgreementId =
        match classificationRule.classificationClaimant with
        | Account accountId -> accountId |> AccountId.value |> Some, None
        | PaymentAgreement paymentAgreementId -> None, paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value |> Some
    let priority = classificationRule.priority
    let isActive = classificationRule.isActive
    let createdAt = classificationRule.createdAt
    let modifiedAt = classificationRule.modifiedAt
    result {
        let! ruleGroups = classificationRule.ruleGroups |> toJson<ClassificationRuleGroup list>
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@rule_name"; value = CharString(ruleName) }
              { name = "@account_at_match"; value = NullableUniqueId(accountId) }
              { name = "@payment_agreement_at_match"; value = NullableUniqueId(paymentAgreementId) }
              { name = "@priority"; value = Integer(priority) }
              { name = "@rule_groups"; value = Jsonb(ruleGroups) }
              { name = "@is_active"; value = Boolean(isActive) }
              { name = "@created_at"; value = DbInstant createdAt }
              { name = "@modified_at"; value = DbInstant modifiedAt }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }
    
let private reconstitute raw =
    result {
        let (uuid,
             nameStr,
             accountUuidOpt,
             paymentAgreementUuidOpt,
             priority,
             ruleGroupsStr,
             isActive,
             createdAt,
             modifiedAt) =
            raw
        let classificationRuleId = uuid |> ClassificationRuleId.fromGuid
        let! name = nameStr |> ClassificationRuleName.create
        let! classificationClaimant =
            match accountUuidOpt, paymentAgreementUuidOpt with
            | Some accountUuid, None -> Ok (ClassificationClaimant.Account (accountUuid |> AccountId.fromGuid))
            | None, Some paymentAgreementUuid ->
                let pmtId:CashFlowComponent.PaymentAgreementId =
                    paymentAgreementUuid |> CashFlowComponent.PaymentAgreementId.fromGuid
                Ok (ClassificationClaimant.PaymentAgreement pmtId)
            | _ -> Error (IngestionClassificationRuleInvalidClaimant(uuid, accountUuidOpt, paymentAgreementUuidOpt))
        let! ruleGroups = ruleGroupsStr |> fromJson<ClassificationRuleGroup list>
        return
            create
                classificationRuleId
                name
                classificationClaimant
                priority
                ruleGroups
                isActive
                createdAt
                modifiedAt
    }
    
let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "rule_name"),
    (row |> RowReader.getUuidOption "account_at_match"),
    (row |> RowReader.getUuidOption "payment_agreement_at_match"),
    (row |> RowReader.getInt "priority"),
    (row |> RowReader.getString "rule_groups"),
    (row |> RowReader.getBool "is_active"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let query
    (context: Context.Context)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (orderBy: string option)
    (expectedRows: AcceptableExpectedRows)
    : Result<ClassificationRule list, AppError> =
    let select =
        """
        cr.unique_id, cr.rule_name, cr.account_at_match, cr.payment_agreement_at_match, cr.priority,
        cr.rule_groups, cr.is_active, cr.created_at, cr.modified_at
        """
    let from = "classification.classification_rule cr"
    let queryStatement = buildReadQuery None select from joinList predicate limit None orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context.Context) (ruleId: ClassificationRuleId) : Result<ClassificationRule, AppError> =
    let predicate = "cr.unique_id = @unique_id"
    let nameStr = ruleId |> ClassificationRuleId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId(nameStr) } ]
    query context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

let fetchByIdList
    (context: Context.Context)
    (ruleIds: ClassificationRuleId list)
    : Result<ClassificationRule list, AppError> =
    if ruleIds |> List.isEmpty then Error IngestionClassificationRuleIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. ruleIds.Length ] ruleIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@classificationRuleId{ordinal}"
            name, { name = name; value = UniqueId(id |> ClassificationRuleId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"cr.unique_id in ({names})"
    query context None (Some predicate) None parameters None AnyQuantityIsAcceptable

let fetchByName (context: Context.Context) (name: ClassificationRuleName) : Result<ClassificationRule, AppError> =
    let predicate = "cr.rule_name = @rule_name"
    let nameStr = name |> ClassificationRuleName.value
    let parameters = [ { name = "@rule_name"; value = CharString(nameStr) } ]
    query context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

/// constrainsLineType is true when a line type is pinned anywhere in the rule, not on every path through it. An Or
/// group with one unconstrained chain still reads as true and can let both lines of an entry through.
let constrainsLineType (classificationRule: ClassificationRule) : bool =
    let chainHasLineType (fieldMatchChain: FieldMatchChain.FieldMatchChain) =
        fieldMatchChain
        |> FieldMatchChain.chain
        |> List.exists (fun fieldMatch ->
            match fieldMatch with
            | FieldMatch.LineType _ -> true
            | _ -> false)
    classificationRule.ruleGroups
    |> List.exists (fun ruleGroup ->
        let chains = (ruleGroup |> chainOne) :: (ruleGroup |> chainTwo |> Option.toList)
        chains |> List.exists chainHasLineType)

let doesMatch
    (candidate: MatchCandidate)
    (classificationRule: ClassificationRule)
    : bool =
    // empty lists would match everything. we have validation at construction. the empty check is a backstop
    if classificationRule.ruleGroups |> List.isEmpty then false
    else
        classificationRule.ruleGroups
        |> List.forall(fun ruleGroup ->
                ruleGroup |> doesMatch candidate)
        
