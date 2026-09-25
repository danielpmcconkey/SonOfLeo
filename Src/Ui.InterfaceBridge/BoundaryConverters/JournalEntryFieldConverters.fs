module Ui.InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters

open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.AccountActivity
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.BoundaryConverters.SharedContractConverters
open Ui.InterfaceBridge.BoundaryConverters.MoneyFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

let ``convert JeDescriptionString Option to JeDescription Option``
    (stringOption: string option)
    : Result<JournalEntryDescription option, IAppError> =
    let fallibleConverter = (fun string -> string |> JournalEntryDescription.create)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert JeSourceString Option to JeSource Option``
    (stringOption: string option)
    : Result<JournalEntrySource option, IAppError> =
    let fallibleConverter = (fun string -> string |> JournalEntrySource.create)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert JournalEntryLineInput to JournalEntryLinePrimitives``
    (context: Context.Context)
    (input: JournalEntryLineInput)
    : Result<AccountId * Money.Money * JournalEntryLineType * JournalEntryLineMemo option, IAppError> =
    result {
        let! accountId = input.accountCode |> ``convert AccountCodeString to Id`` context
        let! amount = input.amount |> Money.fromDecimal
        let! lineType = input.lineType |> JournalEntryLineType.fromString
        let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        return (accountId, amount, lineType, memo)
    }

let ``convert [JournalEntryLineInput list] to [JournalEntryLinePrimitives list]``
    (context: Context.Context)
    (input: JournalEntryLineInput list)
    : Result<(AccountId * Money.Money * JournalEntryLineType * JournalEntryLineMemo option) list, IAppError> =
    input
    |> List.map(fun x -> x |> ``convert JournalEntryLineInput to JournalEntryLinePrimitives`` context)
    |> convertListOfResultsToResultsList

let ``convert JournalEntryLine to JournalEntryLineReturn``
    (context: Context.Context)
    (model: JournalEntryLine.JournalEntryLine)
    : Result<JournalEntryLineReturn, IAppError> =
    result {
        let! accountCode = model |> JournalEntryLine.accountId |> ``convert AccountId to AccountCodeString`` context
        let! nameString = model |> JournalEntryLine.accountId |> ``convert AccountId to AccountNameString`` context
        return
            { id = model |> JournalEntryLine.journalEntryLineId |> JournalEntryLineId.value
              accountCode = accountCode
              accountName = nameString
              amount = model |> JournalEntryLine.amount |> Money.amount
              lineType = model |> JournalEntryLine.lineType |> JournalEntryLineType.toString
              memo = model |> JournalEntryLine.memo |> Option.map(fun x -> x |> JournalEntryLineMemo.value)
              createdAt = model |> JournalEntryLine.createdAt
              modifiedAt = model |> JournalEntryLine.modifiedAt }
    }

let ``convert JournalEntryLine list to JournalEntryLineReturn list``
    (context: Context.Context)
    (input: JournalEntryLine.JournalEntryLine list)
    : Result<JournalEntryLineReturn list, IAppError> =
    input
    |> List.map(fun x -> x |> ``convert JournalEntryLine to JournalEntryLineReturn`` context)
    |> convertListOfResultsToResultsList

let ``convert [JournalEntryExternalReferenceInput] to [JournalEntryExternalReferencePrimitives]``
    (input: JournalEntryExternalReferenceInput)
    : Result<JournalRefFinancialInstitution * JournalExternalReferenceText, IAppError> =
    result {
        let! fi = input.financialInstitution |> JournalRefFinancialInstitution.create
        let! reference = input.referenceText |> JournalExternalReferenceText.create
        return (fi, reference)
    }

let ``convert [JournalEntryExternalReferenceInput list] to [JournalEntryExternalReferencePrimitives list]``
    (input: JournalEntryExternalReferenceInput list)
    : Result<(JournalRefFinancialInstitution * JournalExternalReferenceText) list, IAppError> =
    input
    |> List.map(fun x ->
        x |> ``convert [JournalEntryExternalReferenceInput] to [JournalEntryExternalReferencePrimitives]``)
    |> convertListOfResultsToResultsList

let ``convert [JournalEntryCommentInput] to [JournalEntryCommentPrimitives]``
    (input: JournalEntryCommentInput)
    : Result<JournalEntryHeaderId option * CommentText, IAppError> =
    result {
        let secondaryJournalEntryId = input.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.fromGuid
        let! commentText = input.commentText |> CommentText.create
        return secondaryJournalEntryId, commentText
    }

let ``convert [JournalEntryCommentInput list] to [JournalEntryCommentPrimitives list]``
    (input: JournalEntryCommentInput list)
    : Result<(JournalEntryHeaderId option * CommentText) list, IAppError> =
    input
    |> List.map(fun x -> x |> ``convert [JournalEntryCommentInput] to [JournalEntryCommentPrimitives]``)
    |> convertListOfResultsToResultsList

let ``convert JournalEntryHeader to JournalEntryHeaderReturn`` (model: JournalEntryHeader.JournalEntryHeader) : JournalEntryHeaderReturn =
    { id = model |> JournalEntryHeader.journalEntryHeaderId |> JournalEntryHeaderId.value
      description = model |> JournalEntryHeader.description |> JournalEntryDescription.value
      source = model |> JournalEntryHeader.source |> Option.map(fun x -> x |> JournalEntrySource.value)
      entryDate = model |> JournalEntryHeader.entryDate |> EntryDate.entryDate
      voidedAt = model |> JournalEntryHeader.voidedAt
      createdAt = model |> JournalEntryHeader.createdAt
      modifiedAt = model |> JournalEntryHeader.modifiedAt }

let ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn``
    (model: JournalEntryExternalReference.JournalEntryExternalReference)
    : JournalEntryExternalReferenceReturn =
    { id =
        model
        |> JournalEntryExternalReference.journalEntryExternalReferenceId
        |> JournalEntryExternalReferenceId.value
      financialInstitution =
        model |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
      referenceText = model |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
      createdAt = model |> JournalEntryExternalReference.createdAt
      modifiedAt = model |> JournalEntryExternalReference.modifiedAt }

let ``convert JournalEntryExternalReference list to JournalEntryExternalReferenceReturn list``
    (model: JournalEntryExternalReference.JournalEntryExternalReference list)
    : JournalEntryExternalReferenceReturn list =
    model
    |> List.map(fun x -> x |> ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn``)

let ``convert JournalEntryComment to JournalEntryCommentReturn``
    (model: JournalEntryComment.JournalEntryComment)
    : JournalEntryCommentReturn =
    { id = model |> JournalEntryComment.journalEntryCommentId |> JournalEntryCommentId.value
      primaryJournalEntryId = model |> JournalEntryComment.primaryJournalEntryId |> JournalEntryHeaderId.value
      secondaryJournalEntryId =
        model |> JournalEntryComment.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.value
      commentText = model |> JournalEntryComment.commentText |> CommentText.value
      createdAt = model |> JournalEntryComment.createdAt
      modifiedAt = model |> JournalEntryComment.modifiedAt }

let ``convert JournalEntryComment list to JournalEntryCommentReturn list``
    (model: JournalEntryComment.JournalEntryComment list)
    : JournalEntryCommentReturn list =
    model |> List.map(fun x -> x |> ``convert JournalEntryComment to JournalEntryCommentReturn``)

let ``convert JournalEntry to JournalEntryReturn``
    (context: Context.Context)
    (journalEntry: JournalEntry)
    : Result<JournalEntryReturn, IAppError> =
    result {
        let! lines =
            journalEntry
            |> JournalEntryOrchestration.jeLines
            |> ``convert JournalEntryLine list to JournalEntryLineReturn list`` context
        return
            { header = journalEntry |> JournalEntryOrchestration.header |> ``convert JournalEntryHeader to JournalEntryHeaderReturn``
              lines = lines
              externalReferences =
                journalEntry
                |> JournalEntryOrchestration.externalReferences
                |> ``convert JournalEntryExternalReference list to JournalEntryExternalReferenceReturn list``
              comments =
                journalEntry
                |> JournalEntryOrchestration.comments
                |> ``convert JournalEntryComment list to JournalEntryCommentReturn list`` }
    }

let ``convert JournalEntry list to JournalEntryReturn list``
    (context: Context.Context)
    (journalEntries: JournalEntry list)
    : Result<JournalEntryReturn list, IAppError> =
    journalEntries
    |> List.map(fun x -> x |> ``convert JournalEntry to JournalEntryReturn`` context)
    |> convertListOfResultsToResultsList

// account activity converters are here because activity is a JE domain
let ``convert AccountActivityFilterInput to AccountActivityFilter``
    (context: Context.Context)
    (input: AccountActivityFilterInput)
    : Result<AccountActivityFilter, IAppError> =
    result {
        let! accountId =
            input.accountCode |> ``convert AccountCodeString Option to AccountId Option`` context
        let! accountParentId =
            match input.accountParentCode |> ``convert AccountCodeString Option to AccountId Option`` context with
            | Ok x -> Ok x
            | Error e ->
                if e.DomainName = nameof LedgerError && e.CaseName = nameof LedgerError.AccountCodeIsEmpty
                then Error(LedgerError.AccountParentCodeIsEmpty (input.accountParentCode |> Option.get))
                elif e.DomainName = nameof LedgerError && e.CaseName = nameof LedgerError.AccountCodeTooLong
                then
                    let codeString = input.accountParentCode |> Option.get
                    let max = AccountCode.maxLength
                    Error(LedgerError.AccountParentCodeTooLong (codeString, max))
                elif e.DomainName = nameof LedgerError && e.CaseName = nameof LedgerError.AccountCodeDoesntMatchAccountId
                then Error(LedgerError.AccountParentCodeInvalid (input.accountParentCode |> Option.get))
                else Error e
        let! accountType = input.accountType |> ``convert AccountTypeString Option to AccountType Option``
        let! accountSubtype = input.accountSubtype |> ``convert AccountSubtypeString Option to AccountSubtype Option``
        let! amount = input.amount |> ``convert Decimal Option to Money Option``
        let! description = input.description |> ``convert JeDescriptionString Option to JeDescription Option``
        let! source = input.source |> ``convert JeSourceString Option to JeSource Option``
        let! temporalFilter =
            input.temporalFilter |> ``convert TemporalFilterInput Option To TemporalFilter Option`` context
        return
            { accountId = accountId
              temporalFilter = temporalFilter
              source = source
              accountType = accountType
              accountSubtype = accountSubtype
              accountParentId = accountParentId
              journalEntryId = input.journalEntryId |> Option.map(JournalEntryHeaderId.fromGuid)
              amount = amount
              description = description
              unVoidedOnly = input.unVoidedOnly }
    }

let ``convert AccountActivityDetail to AccountActivityDetailReturn``
    (input: AccountActivityDetail)
    : AccountActivityDetailReturn =
    { lineId = input.lineId |> JournalEntryLineId.value
      amount = input.amount |> Money.amount
      lineType = input.lineType |> JournalEntryLineType.toString
      lineMemo = input.lineMemo |> Option.map(JournalEntryLineMemo.value)
      lineCreatedAt = input.lineCreatedAt
      lineModifiedAt = input.lineModifiedAt
      journalEntryId = input.journalEntryHeaderId |> JournalEntryHeaderId.value
      entryDate = input.entryDate
      journalEntryDescription = input.journalEntryDescription |> JournalEntryDescription.value
      journalEntrySource = input.journalEntrySource |> Option.map(JournalEntrySource.value)
      journalEntryVoidedAt = input.journalEntryVoidedAt }

let ``convert AccountActivity to AccountActivityReturn``
    (context: Context.Context)
    (input: AccountActivity)
    : Result<AccountActivityReturn, IAppError> =
    result {
        let! parentCodeOptionId = input.accountParentId |> ``convert AccountId Option to AccountCode Option`` context
        let parentCodeOptionString = parentCodeOptionId |> Option.map(AccountCode.value)
        let detail =
            input.activityDetail |> Option.map(``convert AccountActivityDetail to AccountActivityDetailReturn``)
        return
            { accountCode = input.accountCode |> AccountCode.value
              accountName = input.accountName |> AccountName.value
              accountType = input.accountType |> AccountType.toString
              accountSubtype = input.accountSubtype |> Option.map(AccountSubtype.toString)
              accountParentCode = parentCodeOptionString
              accountExternalRef = input.accountExternalRef |> Option.map(AccountExternalReference.value)
              activityDetail = detail }
    }

let ``convert AccountActivity List to AccountActivityReturn List``
    (context: Context.Context)
    (input: AccountActivity list)
    : Result<AccountActivityReturn list, IAppError> =
    input
    |> List.map(fun x -> x |> ``convert AccountActivity to AccountActivityReturn`` context)
    |> convertListOfResultsToResultsList
