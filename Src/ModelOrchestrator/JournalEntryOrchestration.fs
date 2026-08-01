namespace ModelOrchestrator.JournalEntries

open System
open Context.Context
open DataAccessLayer.ExecuteReader
open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.Accounts
open ModelOrchestrator
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters

type JournalEntry =
    private
        { header: JournalEntryHeader
          lines: JournalEntryLine list
          externalReferences: JournalEntryExternalReference list
          comments: JournalEntryComment list }

module JournalEntry =
    let header je = je.header
    let lines je = je.lines
    let externalReferences je = je.externalReferences
    let comments je = je.comments

    // =============================================================================
    // validating JE as a collection
    // =============================================================================

    let private validateAmountEquality (lines: JournalEntryLine list) : Result<unit, AppError> =
        result {
            let! totalDebits = lines |> JournalEntryLine.sumLinesByType Debit
            let! totalCredits = lines |> JournalEntryLine.sumLinesByType Credit
            return!
                if totalCredits = totalDebits then
                    Ok()
                else
                    Error(JournalEntryDebitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
        }

    let private validateLineCount (lines: JournalEntryLine list) : Result<unit, AppError> =
        if lines |> List.length < 2 then
            Error(JournalEntryInsufficientLines(lines |> List.length))
        else
            Ok()

    let validateLineList (lines: JournalEntryLine list) : Result<unit, AppError> =
        result {
            let! _ = validateLineCount lines
            let! _ = validateAmountEquality lines
            return ()
        }

    // =============================================================================
    // Create
    // =============================================================================

    let private createValidHeader
        (context: Context)
        (description: JournalEntryDescription)
        (source: JournalEntrySource option)
        (entryDate: EntryDate)
        : Result<JournalEntryHeader, AppError> =
        JournalEntryHeaderOrchestration.constructNewAndSaveToDb context description source entryDate

    let private confirmAccountIsActiveAtEntryDate
        (context: Context)
        (entryDate: EntryDate)
        (accountId: AccountId)
        : Result<unit, AppError> =
        result {
            let! account =
                match accountId |> Account.fetchById context with
                | Ok a -> Ok a
                | Error(DalResultantRowsDidntMatchExpectation _) ->
                    Error(JournalEntryLineAccountDoesntExist(accountId |> AccountId.value))
                | Error e -> Error e
            let referenceDate = entryDate |> EntryDate.entryDate
            let activityPeriod = account |> Account.activityPeriod
            return!
                match activityPeriod |> AccountActivityPeriod.isActive referenceDate with
                | true -> Ok()
                | false ->
                    let accountUuid = accountId |> AccountId.value
                    let entryDateLd = entryDate |> EntryDate.entryDate
                    let beginDate = activityPeriod |> AccountActivityPeriod.activeBegin
                    let endDate = activityPeriod |> AccountActivityPeriod.activeEnd
                    Error(JournalEntryLineAccountInactive(accountUuid, entryDateLd, beginDate, endDate))
        }

    let private createValidLines
        (context: Context)
        (journalEntryId: JournalEntryHeaderId)
        (entryDate: EntryDate)
        (lines: (AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list)
        : Result<JournalEntryLine list, AppError> =
        lines
        |> List.map(fun line ->
            let accountId, amount, lineType, memo = line
            result {
                do! accountId |> confirmAccountIsActiveAtEntryDate context entryDate
                return!
                    JournalEntryLineOrchestration.constructNewAndSaveToDb
                        context
                        journalEntryId
                        accountId
                        amount
                        lineType
                        memo
            })
        |> convertListOfResultsToResultsList

    let private createValidExternalReferences
        (context: Context)
        (journalEntryHeaderId: JournalEntryHeaderId)
        (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
        : Result<JournalEntryExternalReference list, AppError> =
        references
        |> List.map(fun reference ->
            let financialInstitution, referenceText = reference
            JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                context
                journalEntryHeaderId
                financialInstitution
                referenceText)
        |> convertListOfResultsToResultsList

    let private createValidComments
        (context: Context)
        (primaryJournalEntryId: JournalEntryHeaderId)
        (comments: (JournalEntryHeaderId option * CommentText) list)
        : Result<JournalEntryComment list, AppError> =
        comments
        |> List.map(fun comment ->
            let secondaryJournalEntryId, commentText = comment
            JournalEntryCommentOrchestration.constructNewAndSaveToDb
                context
                primaryJournalEntryId
                secondaryJournalEntryId
                commentText)
        |> convertListOfResultsToResultsList

    /// constructNewAndSaveToDb validates that the components work together to
    /// form a valid whole before adding it to the persistence layer. All new
    /// Journal Entry creation should route through here before being sent to the
    /// persistence layer. Internal model functions may construct through other
    /// means if they're operating on known good data.
    let constructNewAndSaveToDb
        (context: Context)
        (description: JournalEntryDescription)
        (source: JournalEntrySource option)
        (entryDate: EntryDate)
        (lines: (AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list)
        (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
        (comments: (JournalEntryHeaderId option * CommentText) list)
        : Result<JournalEntry, AppError> =
        result {
            let! validHeader = createValidHeader context description source entryDate
            let journalEntryHeaderId = validHeader |> JournalEntryHeader.journalEntryHeaderId
            let! validLines = createValidLines context journalEntryHeaderId entryDate lines
            let! validReferences = createValidExternalReferences context journalEntryHeaderId references
            let! validComments = createValidComments context journalEntryHeaderId comments
            do! validateLineList validLines
            return
                { header = validHeader
                  lines = validLines
                  externalReferences = validReferences
                  comments = validComments }
        }

    // =============================================================================
    // Read
    // =============================================================================

    let private composeFromFetchedLists
        (headers: JournalEntryHeader list)
        (lines: JournalEntryLine list)
        (references: JournalEntryExternalReference list)
        (comments: JournalEntryComment list)
        : JournalEntry list =
        headers
        |> List.map(fun header ->
            let headerId = header |> JournalEntryHeader.journalEntryHeaderId
            let linesForHeader =
                lines |> List.filter(fun x -> x |> JournalEntryLine.journalEntryHeaderId = headerId)
            let referencesForHeader =
                references
                |> List.filter(fun x -> x |> JournalEntryExternalReference.journalEntryHeaderId = headerId)
            let commentsForHeader =
                comments |> List.filter(fun x -> x |> JournalEntryComment.primaryJournalEntryId = headerId)
            { header = header
              lines = linesForHeader
              externalReferences = referencesForHeader
              comments = commentsForHeader })

    let private fetchHeadersFromFilter
        (context: Context)
        (filter: JournalEntryFetchFilter)
        (expectedRows: AcceptableExpectedRows)
        : Result<JournalEntryHeader list, AppError> =
        result {
            let! dateRange =
                match filter.temporalFilter with
                | None -> Ok None
                | Some(DateRange dr) -> Ok(Some(dr.beginDate, dr.endInclusive))
                | Some(FiscalPeriodIdentifier fpId) ->
                    fpId
                    |> FiscalPeriod.fetchById context
                    |> Result.map(fun fp -> Some(fp |> FiscalPeriod.startDate, fp |> FiscalPeriod.endDate))
            let voidClause =
                if filter.unVoidedOnly then
                    "and je.voided_at is null"
                else
                    ""
            let whereClausesAndParams =
                [ filter.journalEntryHeaderId
                  |> Option.map(fun x ->
                      ("and je.unique_id = @header_id",
                       { name = "@header_id"; value = UniqueId(x |> JournalEntryHeaderId.value) }))

                  dateRange
                  |> Option.map(fun (x, _) ->
                      ("and je.entry_date >= @begin_date", { name = "@begin_date"; value = DbLocalDate x }))

                  dateRange
                  |> Option.map(fun (_, x) ->
                      ("and je.entry_date <= @end_date", { name = "@end_date"; value = DbLocalDate x }))

                  filter.source
                  |> Option.map(fun x ->
                      ("and je.je_source = @je_source",
                       { name = "@je_source"; value = CharString(x |> JournalEntrySource.value) }))

                  filter.financialInstitution
                  |> Option.map(fun x ->
                      (let fiString = x |> JournalRefFinancialInstitution.value
                       "and jer.financial_institution = @financial_institution",
                       { name = "@financial_institution"; value = CharString fiString }))

                  filter.referenceText
                  |> Option.map(fun x ->
                      (let refString = x |> JournalExternalReferenceText.value
                       "and jer.reference = @reference", { name = "@reference"; value = CharString refString })) ]
                |> List.choose id
            let whereClauses = whereClausesAndParams |> List.map fst |> String.concat Environment.NewLine
            let parameters = whereClausesAndParams |> List.map snd
            let predicate =
                Some
                    $"""
                1 = 1
                {whereClauses}
                {voidClause}
                """
            let joins =
                [
                  // as of right now, there's no filter that compels us to
                  // join on lines or comments, so this options list is a bit
                  // overkill. However, I'm leaving the structure in so that
                  // it'd be easier to expand our filter in future.
                  if filter.referenceText = None && filter.financialInstitution = None then
                      None
                  else
                      Some "left join ledger.journal_entry_ext_reference jer on je.unique_id = jer.journal_entry_id" ]
                |> List.choose id
                |> String.concat Environment.NewLine
            let joinClause = if joins = "" then None else Some joins
            let sort = Some "je.entry_date asc"
            let! headersDuplicates =
                JournalEntryHeader.readRowsFromDb context joinClause predicate None sort parameters expectedRows
            return headersDuplicates |> List.distinctBy(fun h -> h |> JournalEntryHeader.journalEntryHeaderId)
        }

    let fetchFiltered
        (context: Context)
        (filter: JournalEntryFetchFilter)
        (expectedRows: AcceptableExpectedRows)
        : Result<JournalEntry list, AppError> =
        result {
            let! headers = fetchHeadersFromFilter context filter expectedRows
            let! innerRailroad =
                if headers |> List.length = 0 then
                    Ok []
                else
                    result {
                        let headerIds = headers |> List.map(fun x -> x |> JournalEntryHeader.journalEntryHeaderId)
                        let! lines = headerIds |> JournalEntryLine.fetchByJournalEntryHeaderIdList context
                        let! references =
                            headerIds |> JournalEntryExternalReference.fetchByJournalEntryHeaderIdList context
                        let! comments = headerIds |> JournalEntryComment.fetchByJournalEntryHeaderIdList context
                        return composeFromFetchedLists headers lines references comments
                    }
            return innerRailroad
        }

    let fetchById
        (context: Context)
        (journalEntryHeaderId: JournalEntryHeaderId)
        : Result<JournalEntry, AppError> =
        let filter =
            { journalEntryHeaderId = Some journalEntryHeaderId
              source = None
              financialInstitution = None
              referenceText = None
              temporalFilter = None
              unVoidedOnly = false }
        // Note: expected rows of exactly one works here only because we don't
        // have any other filter conditions that would join other tables. In
        // future, if we ever expand this filter or use this function as a
        // template for a new fetch function, know that the deduplication of
        // records happens *after* DAL checks the exactly one condition.
        let expectedRows = ExactlyOne
        match fetchFiltered context filter expectedRows with
        | Ok x -> x |> List.head |> Ok
        | Error (DalResultantRowsDidntMatchExpectation (expected, actual)) ->
            if actual = 0 then Error (JournalEntryHeaderIdDoesntExist (journalEntryHeaderId |> JournalEntryHeaderId.value))
            else Error (DalResultantRowsDidntMatchExpectation (expected, actual))
        | Error e -> Error e

    let fetchByPeriod
        (context: Context)
        (fiscalPeriod: FiscalPeriod)
        : Result<JournalEntry list, AppError> =
        let filter =
            { journalEntryHeaderId = None
              source = None
              financialInstitution = None
              referenceText = None
              temporalFilter =
                Some(fiscalPeriod |> FiscalPeriod.fiscalPeriodId |> TemporalFilter.FiscalPeriodIdentifier)
              unVoidedOnly = false }
        let expectedRows = AnyQuantityIsAcceptable
        fetchFiltered context filter expectedRows

    let fetchByDateRange
        (context: Context)
        (beginDate: LocalDate)
        (endDateInclusive: LocalDate)
        : Result<JournalEntry list, AppError> =
        if beginDate > endDateInclusive
        then
            Error (JournalEntryFetchByDateRangeBeginAfterEnd (beginDate, endDateInclusive))
        else
            let filter =
                { journalEntryHeaderId = None
                  source = None
                  financialInstitution = None
                  referenceText = None
                  temporalFilter =
                      TemporalFilter.DateRange { beginDate = beginDate; endInclusive = endDateInclusive }
                      |> Some
                  unVoidedOnly = false }
            let expectedRows = AnyQuantityIsAcceptable
            fetchFiltered context filter expectedRows

    let fetchByReference
        (context: Context)
        (financialInstitution: JournalRefFinancialInstitution option)
        (referenceText: JournalExternalReferenceText option)
        : Result<JournalEntry list, AppError> =
        result {
            do!
                if financialInstitution |> Option.isNone && referenceText |> Option.isNone then
                    Error(JournalEntryFetchByReferenceBothArgumentsNull)
                else
                    Ok()
            let filter =
                { journalEntryHeaderId = None
                  source = None
                  financialInstitution = financialInstitution
                  referenceText = referenceText
                  temporalFilter = None
                  unVoidedOnly = false }
            let expectedRows = AnyQuantityIsAcceptable
            return! fetchFiltered context filter expectedRows
        }
