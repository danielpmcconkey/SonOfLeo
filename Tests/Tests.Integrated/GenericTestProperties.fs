module Tests.Integrated.GenericTestProperties

open InterfaceBridge.InterfaceContracts.AccountContracts
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Utilities.FieldUpdate
open Context.Context

// todo: rename this module or move the helper functions out of it

// account
let genericAccountCodeString = "GenCode"
let genericAccountCode =
    genericAccountCodeString
    |> AccountCode.create
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountNameString = "Gen account name"
let genericAccountName =
    genericAccountNameString
    |> AccountName.create
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountTypeString = "Revenue"
let genericAccountType =
    AccountType.fromString genericAccountTypeString
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountActiveBegin = Calendar.today().PlusYears(-1)
let genericAccountActiveEnd = None
let genericAccountActivityPeriod =
    AccountActivityPeriod.create genericAccountActiveBegin genericAccountActiveEnd
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountSubtype = None
let genericAccountSubtypeString = "Cash"
let genericAccountSubtypeNonNull =
    AccountSubtype.fromString genericAccountSubtypeString
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountParentId = None
let genericAccountParentCode = None
let genericAccountReference = None
// fiscal period
let genericFiscalPeriodKeyString = "2050-01"
let genericFiscalPeriodKey =
    genericFiscalPeriodKeyString
    |> FiscalPeriodKey.fromString
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

let createTestFiscalPeriodFromPrimitives context keyStr : Result<FiscalPeriod, AppError> =
    result {
        let! key = keyStr |> FiscalPeriodKey.fromString
        return! key |> FiscalPeriodCreation.constructNewAndSaveToDb context
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
    : Result<(Account * AccountId), AppError> =
    result {
        let! account =
            AccountCreation.constructNewAndSaveToDb
                context
                (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                (name |> AccountName.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                (actType |> AccountType.fromString |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                (AccountActivityPeriod.create activeBegin activeEnd
                 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                (subtype
                 |> convertOptionToDesiredTypeWithFallibleConverter AccountSubtype.fromString
                 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                parentId
                (reference
                 |> convertOptionToDesiredTypeWithFallibleConverter AccountExternalReference.create
                 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
        return (account, account |> Account.accountId)
    }

let createTestAccountFromCodeString context codeToUse =
    createTestAccountFromPrimitives
        context
        codeToUse
        genericAccountNameString
        genericAccountTypeString
        genericAccountActiveBegin
        genericAccountActiveEnd
        genericAccountSubtype
        genericAccountParentId
        genericAccountReference
        
let createAccountInput codeToUse : AccountCreateInput =
    { code = codeToUse
      name = genericAccountNameString
      accountTypeSt = genericAccountTypeString
      activeBegin = genericAccountActiveBegin
      activeEnd = genericAccountActiveEnd
      subType = genericAccountSubtype
      parentCode = genericAccountParentCode
      reference = genericAccountReference }
let createTestJournalEntryFromPrimitives
    (context: Context)
    (description: string)
    (source: string option)
    (entryDate: LocalDate)
    (lines: (AccountId * decimal * string * string option) list)
    (references: (string * string) list)
    (comments: (JournalEntryHeaderId option * string) list)
    : Result<JournalEntry * JournalEntryHeaderId, AppError> =
    let convertLines
        (linesIn: (AccountId * decimal * string * string option) list)
        : Result<(AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list, AppError> =
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
        : Result<(JournalRefFinancialInstitution * JournalExternalReferenceText) list, AppError> =
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
        : Result<(JournalEntryHeaderId option * CommentText) list, AppError> =
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
            JournalEntry.constructNewAndSaveToDb
                context
                description
                source
                entryDate
                linesConverted
                refsConverted
                commentsConverted
        let headerId = journalEntry |> JournalEntry.header |> JournalEntryHeader.journalEntryHeaderId
        return (journalEntry, headerId)
    }
let createJournalRefFinancialInstitutionFromString fiString =
    fiString
    |> JournalRefFinancialInstitution.create
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let createJournalExternalReferenceTextFromString textString =
    textString
    |> JournalExternalReferenceText.create
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
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
                |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
                |> JournalEntryHeader.voidedAt
                |> Option.isNone)
        else
            allLinesAtAccountAndType
    filteredFurther |> List.sumBy(fun x -> x |> JournalEntryLine.amount |> Money.amount)
