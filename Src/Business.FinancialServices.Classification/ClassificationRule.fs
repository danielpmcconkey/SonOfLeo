module Business.FinancialServices.Classification.ClassificationRule

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.Json.Json
open App.Utility.FieldUpdate
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.CashFlow
open Business.FinancialServices.Classification.ClassificationError
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
    
/// StoredFieldMatch is how a field match is written to the database. Stored rule JSON outlives changes to the domain
/// types, so it is plain strings and numbers under fixed names, and every value goes back through its smart constructor
/// on read.
type StoredFieldMatch =
    { field: string
      pattern: string option
      lineType: string option
      numericOperator: string option
      amount: decimal option }

type StoredRuleGroup =
    { connector: string
      chainOne: StoredFieldMatch list
      chainTwo: StoredFieldMatch list option }

/// sourcePatternLikePredicate is true for a rule with a Source field match, in any chain of any group, whose pattern is
/// LIKE the given parameter.
let sourcePatternLikePredicate (parameterName: string) =
    $"""
    EXISTS (
        SELECT 1
        FROM jsonb_array_elements(cr.rule_groups) AS rg,
             jsonb_array_elements(
                (rg.value -> 'chainOne') || COALESCE(NULLIF(rg.value -> 'chainTwo', 'null'::jsonb), '[]'::jsonb)
             ) AS fm
        WHERE fm.value ->> 'field' = 'Source'
        AND fm.value ->> 'pattern' LIKE {parameterName}
    )
    """

let private toStoredFieldMatch (fieldMatch: FieldMatch.FieldMatch) : StoredFieldMatch =
    let blank = { field = ""; pattern = None; lineType = None; numericOperator = None; amount = None }
    match fieldMatch with
    | FieldMatch.Source pattern ->
        { blank with field = "Source"; pattern = Some(pattern |> StringSearchPattern.value) }
    | FieldMatch.Description pattern ->
        { blank with field = "Description"; pattern = Some(pattern |> StringSearchPattern.value) }
    | FieldMatch.LineType lineType ->
        { blank with field = "LineType"; lineType = Some(lineType |> JournalEntryLineType.toString) }
    | FieldMatch.Amount moneyPattern ->
        { blank with
            field = "Amount"
            numericOperator = Some(moneyPattern.numericSearchOperator |> NumericSearchOperator.toString)
            amount = Some(moneyPattern.amount |> Money.amount) }

let private toStoredRuleGroup (ruleGroup: ClassificationRuleGroup) : StoredRuleGroup =
    let storedChain chain = chain |> FieldMatchChain.chain |> List.map toStoredFieldMatch
    { connector = ruleGroup |> connector |> ClassificationGroupConnector.toString
      chainOne = ruleGroup |> chainOne |> storedChain
      chainTwo = ruleGroup |> chainTwo |> Option.map storedChain }

/// ruleGroupsToJson is the rule_groups column's value for the given groups.
let ruleGroupsToJson (ruleGroups: ClassificationRuleGroup list) : Result<string, IAppError> =
    ruleGroups |> List.map toStoredRuleGroup |> toJson<StoredRuleGroup list>

let private storedGroupsInvalid (ruleUuid: System.Guid) (e: IAppError) : IAppError =
    ClassificationRuleStoredGroupsInvalid(ruleUuid, e.ToMessage())

let private requiredStoredValue (ruleUuid: System.Guid) (field: string) (valueName: string) (value: 'a option) =
    match value with
    | Some x -> Ok x
    | None -> Error(ClassificationRuleStoredGroupsInvalid(ruleUuid, $"a {field} match has no {valueName}.") :> IAppError)

let private fromStoredPattern (ruleUuid: System.Guid) (field: string) (pattern: string option) =
    result {
        let! patternStr = pattern |> requiredStoredValue ruleUuid field "pattern"
        return!
            patternStr
            |> StringSearchPattern.create
            |> Result.mapError (fun e ->
                let reason =
                    match e with
                    | AsError (ClassificationSearchPatternInvalidRegex(_, reason)) -> reason
                    | other -> other.ToMessage()
                ClassificationRuleStoredPatternInvalid(ruleUuid, patternStr, reason) :> IAppError)
    }

let private fromStoredFieldMatch (ruleUuid: System.Guid) (stored: StoredFieldMatch) =
    match stored.field with
    | "Source" -> stored.pattern |> fromStoredPattern ruleUuid stored.field |> Result.map FieldMatch.Source
    | "Description" -> stored.pattern |> fromStoredPattern ruleUuid stored.field |> Result.map FieldMatch.Description
    | "LineType" ->
        result {
            let! lineTypeStr = stored.lineType |> requiredStoredValue ruleUuid stored.field "line type"
            let! lineType =
                lineTypeStr |> JournalEntryLineType.fromString |> Result.mapError (storedGroupsInvalid ruleUuid)
            return FieldMatch.LineType lineType
        }
    | "Amount" ->
        result {
            let! operatorStr = stored.numericOperator |> requiredStoredValue ruleUuid stored.field "operator"
            let! amount = stored.amount |> requiredStoredValue ruleUuid stored.field "amount"
            let! numericOperator =
                operatorStr |> NumericSearchOperator.fromString |> Result.mapError (storedGroupsInvalid ruleUuid)
            let! money = amount |> Money.fromDecimal |> Result.mapError (storedGroupsInvalid ruleUuid)
            return FieldMatch.Amount { numericSearchOperator = numericOperator; amount = money }
        }
    | other ->
        Error(ClassificationRuleStoredGroupsInvalid(ruleUuid, $"\"{other}\" is not a field match target.") :> IAppError)

let private fromStoredChain (ruleUuid: System.Guid) (stored: StoredFieldMatch list) =
    if stored |> List.isEmpty then Error(storedGroupsInvalid ruleUuid ClassificationFieldMatchChainEmpty) else
    stored
    |> List.map (fromStoredFieldMatch ruleUuid)
    |> convertListOfResultsToResultsList
    |> Result.map FieldMatchChain.create

let private fromStoredRuleGroup (ruleUuid: System.Guid) (stored: StoredRuleGroup) =
    result {
        let! groupConnector =
            stored.connector |> ClassificationGroupConnector.fromString |> Result.mapError (storedGroupsInvalid ruleUuid)
        let! storedChainOne = stored.chainOne |> fromStoredChain ruleUuid
        let! storedChainTwo =
            match stored.chainTwo with
            | None -> Ok None
            | Some chain -> chain |> fromStoredChain ruleUuid |> Result.map Some
        return ClassificationRuleGroup.create groupConnector storedChainOne storedChainTwo
    }

let private ruleGroupsFromJson (ruleUuid: System.Guid) (json: string) =
    result {
        let! stored = json |> fromJson<StoredRuleGroup list> |> Result.mapError (storedGroupsInvalid ruleUuid)
        if stored |> List.isEmpty then return! Error(storedGroupsInvalid ruleUuid ClassificationRuleGroupsEmpty) else
        return!
            stored
            |> List.map (fromStoredRuleGroup ruleUuid)
            |> convertListOfResultsToResultsList
    }

let persist (context: Context.Context) (classificationRule: ClassificationRule) : Result<unit, IAppError> =
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
        | ClassificationClaimant.Account accountId -> accountId |> AccountId.value |> Some, None
        | ClassificationClaimant.PaymentAgreement paymentAgreementId -> None, paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value |> Some
    let priority = classificationRule.priority
    let isActive = classificationRule.isActive
    let createdAt = classificationRule.createdAt
    let modifiedAt = classificationRule.modifiedAt
    result {
        let! ruleGroups = classificationRule.ruleGroups |> ruleGroupsToJson
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
            | _ -> Error (ClassificationRuleInvalidClaimant(uuid, accountUuidOpt, paymentAgreementUuidOpt))
        let! ruleGroups = ruleGroupsStr |> ruleGroupsFromJson uuid
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
    : Result<ClassificationRule list, IAppError> =
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

let fetchById (context: Context.Context) (ruleId: ClassificationRuleId) : Result<ClassificationRule, IAppError> =
    let predicate = "cr.unique_id = @unique_id"
    let nameStr = ruleId |> ClassificationRuleId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId(nameStr) } ]
    query context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

let fetchByIdList
    (context: Context.Context)
    (ruleIds: ClassificationRuleId list)
    : Result<ClassificationRule list, IAppError> =
    if ruleIds |> List.isEmpty then Error ClassificationRuleIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. ruleIds.Length ] ruleIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@classificationRuleId{ordinal}"
            name, { name = name; value = UniqueId(id |> ClassificationRuleId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"cr.unique_id in ({names})"
    query context None (Some predicate) None parameters None AnyQuantityIsAcceptable

let fetchByName (context: Context.Context) (name: ClassificationRuleName) : Result<ClassificationRule, IAppError> =
    let predicate = "cr.rule_name = @rule_name"
    let nameStr = name |> ClassificationRuleName.value
    let parameters = [ { name = "@rule_name"; value = CharString(nameStr) } ]
    query context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

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
            | ClassificationClaimant.Account accountId ->
                accountId |> AccountId.value |> Some, None
            | ClassificationClaimant.PaymentAgreement paymentAgreementId ->
                None, paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value |> Some
        Some ("account_at_match = @account_at_match",
                { name = "@account_at_match"; value = NullableUniqueId(accountUuid) }),
        Some ("payment_agreement_at_match = @payment_agreement_at_match",
                { name = "@payment_agreement_at_match"; value = NullableUniqueId(paymentAgreementUuid) })

/// update writes the changed fields of one rule, and refuses a call that changes nothing.
let update
    (context: Context.Context)
    (classificationRuleId: ClassificationRuleId)
    (classificationRuleNameUpdate: FieldUpdate<ClassificationRuleName>)
    (classificationClaimantUpdate: FieldUpdate<ClassificationClaimant>)
    (priorityUpdate: FieldUpdate<int>)
    (ruleGroupsUpdate: FieldUpdate<ClassificationRuleGroup list>)
    (isActiveUpdate: FieldUpdate<bool>)
    : Result<unit, IAppError> =
    result {
        let! groupStr =
            match ruleGroupsUpdate with
            | NoChange -> Ok ""
            | SetTo x -> x |> ruleGroupsToJson
        let accountAtMatchUpdate, paymentAtMatchUpdate =
            classificationClaimantUpdate |> classificationClaimantToJointUpdates
        let updates =
            [ classificationRuleNameUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  ("rule_name = @rule_name",
                   { name = "@rule_name"; value = CharString(n |> ClassificationRuleName.value) }))

              priorityUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  ("priority = @priority", { name = "@priority"; value = Integer(n) }))

              ruleGroupsUpdate
              |> mapNoChangeToOptionWithConversion(fun _ ->
                  ("rule_groups = @rule_groups", { name = "@rule_groups"; value = Jsonb(groupStr) }))

              isActiveUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  ("is_active = @is_active", { name = "@is_active"; value = Boolean(n) }))

              accountAtMatchUpdate
              paymentAtMatchUpdate ]
            |> List.choose id
        do! if updates.IsEmpty then Error(ClassificationRuleUpdateNoOp) else Ok()
        let setClauses = updates |> List.map fst |> String.concat ", "
        let parameters =
            [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
              { name = "@unique_id"; value = UniqueId(classificationRuleId |> ClassificationRuleId.value) } ]
            @ (updates |> List.map snd)
        let queryStatement =
            $"""
            UPDATE classification.classification_rule
            set
                {setClauses},
                modified_at = @modified
            WHERE unique_id = @unique_id;
        """
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }
