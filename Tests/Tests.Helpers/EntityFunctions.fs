module Tests.Helpers.EntityFunctions

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationRule
open Business.FinancialServices.Classification.ClassificationRuleGroup
open Business.FinancialServices.Classification.FieldMatch
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Tests.Helpers.GenericTestProperties

let createTestFiscalPeriodFromPrimitives context keyStr : Result<FiscalPeriod.FiscalPeriod, IAppError> =
    result {
        let! key = keyStr |> FiscalPeriodKey.fromString
        return! key |> FiscalPeriodCreation.constructNewAndPersist context
    }

let createTestAccountFromPrimitives
    context
    code
    name
    actType
    activeBegin
    activeEnd
    subtype
    parentId
    reference
    : Result<Account * AccountId, IAppError> =
    result {
        let! account =
            AccountCreation.constructNewAndPersist
                context
                (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(e.ToMessage())))
                (name |> AccountName.create |> Result.defaultWith(fun e -> failwith(e.ToMessage())))
                (actType |> AccountType.fromString |> Result.defaultWith(fun e -> failwith(e.ToMessage())))
                (ActivityPeriod.create activeBegin activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
                 |> Result.defaultWith(fun e -> failwith(e.ToMessage())))
                (subtype
                 |> convertOptionToDesiredTypeWithFallibleConverter AccountSubtype.fromString
                 |> Result.defaultWith(fun e -> failwith(e.ToMessage())))
                parentId
                (reference
                 |> convertOptionToDesiredTypeWithFallibleConverter AccountExternalReference.create
                 |> Result.defaultWith(fun e -> failwith(e.ToMessage())))
        return (account, account |> Account.accountId)
    }

let createTestAccountFromCodeString context codeToUse =
    createTestAccountFromPrimitives
        context
        codeToUse
        genericAccountNameString
        genericAccountTypeString
        genericActiveBegin
        genericActiveEnd
        genericAccountSubtype
        genericAccountParentId
        genericAccountReference

let createAccountInput codeToUse : AccountCreateInput =
    { code = codeToUse
      name = genericAccountNameString
      accountTypeSt = genericAccountTypeString
      activeBegin = genericActiveBegin
      activeEnd = genericActiveEnd
      subType = genericAccountSubtype
      parentCode = genericAccountParentCode
      reference = genericAccountReference }
let createTestJournalEntryFromPrimitives
    (context: Context.Context)
    (description: string)
    (source: string option)
    (entryDate: LocalDate)
    (lines: (AccountId * decimal * string * string option) list)
    (references: (string * string) list)
    (comments: (JournalEntryHeaderId option * string) list)
    : Result<JournalEntry * JournalEntryHeaderId, IAppError> =
    let convertLines
        (linesIn: (AccountId * decimal * string * string option) list)
        : Result<(AccountId * Money.Money * JournalEntryLineType * JournalEntryLineMemo option) list, IAppError> =
        linesIn
        |> List.map(fun l ->
            let id, amountDec, lineTypeSt, memoSt = l
            result {
                let! amount = amountDec |> Money.fromDecimal
                let! lineType = lineTypeSt |> JournalEntryLineType.fromString
                let! memo = memoSt |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
                return id, amount, lineType, memo
            })
        |> convertListOfResultsToResultsList
    let convertRefs
        (refsIn: (string * string) list)
        : Result<(JournalRefFinancialInstitution * JournalExternalReferenceText) list, IAppError> =
        refsIn
        |> List.map(fun r ->
            let fiSt, refSt = r
            result {
                let! fi = fiSt |> JournalRefFinancialInstitution.create
                let! ref = refSt |> JournalExternalReferenceText.create
                return fi, ref
            })
        |> convertListOfResultsToResultsList
    let convertComments
        (commentsIn: (JournalEntryHeaderId option * string) list)
        : Result<(JournalEntryHeaderId option * CommentText) list, IAppError> =
        commentsIn
        |> List.map(fun c ->
            let id, textSt = c
            result {
                let! text = textSt |> CommentText.create
                return id, text
            })
        |> convertListOfResultsToResultsList
    result {
        let! description = description |> JournalEntryDescription.create
        let! source = source |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
        let! entryDate = entryDate |> EntryDate.create context
        let! linesConverted = lines |> convertLines
        let! refsConverted = references |> convertRefs
        let! commentsConverted = comments |> convertComments
        let! journalEntry =
            JournalEntryOrchestration.constructNewAndPersist
                context
                description
                source
                entryDate
                linesConverted
                refsConverted
                commentsConverted
        let headerId = journalEntry |> JournalEntryOrchestration.header |> JournalEntryHeader.journalEntryHeaderId
        return (journalEntry, headerId)
    }
let createJournalRefFinancialInstitutionFromString fiString =
    fiString
    |> JournalRefFinancialInstitution.create
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let createJournalExternalReferenceTextFromString textString =
    textString
    |> JournalExternalReferenceText.create
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let createFiUpdateFromString fiString =
    fiString |> createJournalRefFinancialInstitutionFromString |> SetTo
let createReferenceTextUpdateFromString textString =
    textString |> createJournalExternalReferenceTextFromString |> SetTo
let sumJournalEntryLinesByAccountIdAndType tran unvoidedOnly accountId lineType lines =
    // this is expensive if unvoidedOnly is true
    let allLinesAtAccountAndType =
        lines
        |> List.filter(fun x ->
            x |> JournalEntryLine.accountId = accountId && x |> JournalEntryLine.lineType = lineType)
    let filteredFurther =
        if unvoidedOnly then
            allLinesAtAccountAndType
            |> List.filter(fun x ->
                x
                |> JournalEntryLine.journalEntryHeaderId
                |> JournalEntryHeader.fetchById tran
                |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
                |> JournalEntryHeader.voidedAt
                |> Option.isNone)
        else
            allLinesAtAccountAndType
    filteredFurther |> List.sumBy(fun x -> x |> JournalEntryLine.amount |> Money.amount)
    
let createClassificationRuleGroupListForTest
    (ruleGroupPrimitives: (string * FieldMatch list * FieldMatch list option) list)
    : Result<ClassificationRuleGroup list, IAppError> =
    let ruleGroups =
        ruleGroupPrimitives
        |> List.map(fun x -> 
        let connectorStr, fmChain1, fmChain2 = x
        result {
            let! connector = connectorStr |> ClassificationGroupConnector.fromString
            let chainOne = fmChain1 |> FieldMatchChain.create
            let chainTwo = fmChain2 |> Option.map FieldMatchChain.create
            return ClassificationRuleGroup.create connector chainOne chainTwo
        }) |> convertListOfResultsToResultsList
    ruleGroups    

let createClassificationRuleForTest
    (context: Context.Context)
    (classificationRuleNameStr: string)
    (codeAtMatchStr: string)
    (priority: int)
    (ruleGroupPrimitives: (string * FieldMatch list * FieldMatch list option) list)
    : Result<ClassificationRule, IAppError> =
    result {
        let! classificationRuleName = classificationRuleNameStr |> ClassificationRuleName.create
        let! accountAtMatch = codeAtMatchStr |> ``convert AccountCodeString to Id`` context
        let classificationClaimant = ClassificationClaimant.Account accountAtMatch
        let! ruleGroups = ruleGroupPrimitives |> createClassificationRuleGroupListForTest
        return!
            ClassificationOrchestration.createNewClassificationRule
                context
                classificationRuleName
                classificationClaimant
                priority
                ruleGroups
    }

let createIngestionSourceForTest
    (context: Context.Context)
    (nameStr: string)
    : Result<IngestionSource.IngestionSource, IAppError> =
    result {
        let instant = context |> Context.getInitiationInstant
        let uuid = IngestionSourceId.create()
        let! name = nameStr |> JournalRefFinancialInstitution.create
        let source = IngestionSource.create uuid name instant instant
        do! source |> IngestionSource.persist context
        return source
        }
    
let createStageEntryHeaderForTest 
    (context: Context.Context)
    (sourceFileStr: string)
    (descriptionStr: string)
    (fiReferenceStr: string)
    (ingestionSource: IngestionSource.IngestionSource)
    (entryDate: LocalDate)
    : Result<StageEntryHeader.StageEntryHeader, IAppError> =
    result {
        let! sourceFile = sourceFileStr |> SourceFile.create
        let stageEntryHeaderId = StageEntryHeaderId.create()
        let entryDate = entryDate
        let! description = descriptionStr |> JournalEntryDescription.create
        let! fiReference = fiReferenceStr |> JournalExternalReferenceText.create
        let header =
            StageEntryHeader.create sourceFile stageEntryHeaderId
                entryDate description ingestionSource fiReference None (Some Ingested)
        do! header |> StageEntryHeader.persist context Ingested StageIngestion
        return header
        }

let createStageEntryLineForTest 
    (context: Context.Context)
    (stageEntryHeaderId: StageEntryHeaderId)
    (amountDec: decimal)
    (entryTypeStr : string)
    (accountCodeStr: string option)
    (memoStr: string option)
    (journalEntryLineId: JournalEntryLineId option)
    : Result<StageEntryLine.StageEntryLine, IAppError> =
    result {
        let uuid = StageEntryLineId.create()
        let! amount = amountDec |> Money.fromDecimal
        let! entryType = entryTypeStr |> JournalEntryLineType.fromString
        let! accountId = accountCodeStr |> ``convert AccountCodeString Option to AccountId Option`` context
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        let line =
            StageEntryLine.create uuid stageEntryHeaderId amount entryType accountId memo journalEntryLineId
        do! line |> StageEntryLine.persist context
        return line
        }

let createStageEntryLineListForTest
    (context: Context.Context)
    (stageEntryHeaderId: StageEntryHeaderId)
    (lines: (decimal * string * string option * string option * JournalEntryLineId option) list)
    : Result<StageEntryLine.StageEntryLine list, IAppError> =
    lines
    |> List.map (fun (amountDec, entryTypeStr, accountCodeStr, memoStr, journalEntryLineId) ->
        createStageEntryLineForTest context stageEntryHeaderId amountDec entryTypeStr
            accountCodeStr memoStr journalEntryLineId)
    |> convertListOfResultsToResultsList

let createStageEntryStatusTransitionForTest
    (context: Context.Context)
    (stageEntryHeaderId: StageEntryHeaderId)
    (fromStatusStr: string option)
    (toStatusStr: string)
    (instant: Instant)
    (stageStatusChangeMechanismStr: string)
    : Result<StageEntryStatusTransition.StageEntryStatusTransition, IAppError> =
    result {
        let uuid = StageEntryStatusTransitionId.create()
        let! fromStatus = fromStatusStr |> convertOptionToDesiredTypeWithFallibleConverter StagedEntryStatus.fromString
        let! toStatus = toStatusStr |> StagedEntryStatus.fromString
        let! stageStatusChangeMechanism = stageStatusChangeMechanismStr |> StageStatusChangeMechanism.fromString
        let transition = StageEntryStatusTransition.create uuid stageEntryHeaderId fromStatus
                             toStatus instant stageStatusChangeMechanism
        do! transition |> StageEntryHeader.persistStatusTransition context
        return transition
    }

let createStageEntryStatusTransitionListForTest
    (context: Context.Context)
    (stageEntryHeaderId: StageEntryHeaderId)
    (transitions: (string option * string * Instant * string) list)
    : Result<StageEntryStatusTransition.StageEntryStatusTransition list, IAppError> =
    transitions
    |> List.map (fun (fromStatusStr, toStatusStr, instant, stageStatusChangeMechanismStr) ->
        createStageEntryStatusTransitionForTest context stageEntryHeaderId fromStatusStr toStatusStr
            instant stageStatusChangeMechanismStr)
    |> convertListOfResultsToResultsList
    

let createStageEntryForTest 
    (context: Context.Context)
    (sourceFileStr: string)
    (descriptionStr: string)
    (fiReferenceStr: string)
    (ingestionSource: IngestionSource.IngestionSource)
    (entryDate: LocalDate)
    (linePrimitives: (decimal * string * string option * string option * JournalEntryLineId option) list)
    (transitionPrimitives: (string option * string * Instant * string) list)
    : Result<StageEntry, IAppError> =
    result {
        let! header = createStageEntryHeaderForTest context sourceFileStr descriptionStr fiReferenceStr ingestionSource entryDate
        let headerId = header |> StageEntryHeader.stageEntryHeaderId
        let! lines = linePrimitives |> createStageEntryLineListForTest context headerId
        let! transitions = transitionPrimitives |> createStageEntryStatusTransitionListForTest context headerId
        return! createStageEntry context header lines transitions
        }
    
