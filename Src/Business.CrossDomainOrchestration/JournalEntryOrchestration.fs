namespace Business.CrossDomainOrchestration.JournalEntryOrchestration

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters

type JournalEntry =
    private
        { header: JournalEntryHeader.JournalEntryHeader
          jeLines: JournalEntryLine.JournalEntryLine list
          externalReferences: JournalEntryExternalReference.JournalEntryExternalReference list
          comments: JournalEntryComment.JournalEntryComment list }

module JournalEntryOrchestration =
    let header je = je.header
    let jeLines je = je.jeLines
    let externalReferences je = je.externalReferences
    let comments je = je.comments

    // =============================================================================
    // validating JE as a collection
    // =============================================================================

    let confirmLineList (lines: JournalEntryLine.JournalEntryLine list) : Result<unit, IAppError> =
        lines
        |> List.map (fun line -> (line |> JournalEntryLine.lineType), (line |> JournalEntryLine.amount))
        |> BalancedLines.confirm
            (fun count -> JournalEntryInsufficientLines count)
            (fun (debits, credits) -> JournalEntryDebitCreditMismatch(debits, credits))

    // =============================================================================
    // Create
    // =============================================================================

    let private createValidHeader
        (context: Context.Context)
        (description: JournalEntryDescription)
        (source: JournalEntrySource option)
        (entryDate: EntryDate)
        : Result<JournalEntryHeader.JournalEntryHeader, IAppError> =
        JournalEntryHeaderOrchestration.constructNewAndPersist context description source entryDate

    let private confirmAccountIsActiveAtEntryDate
        (context: Context.Context)
        (entryDate: EntryDate)
        (accountId: AccountId)
        : Result<unit, IAppError> =
        result {
            let! account =
                accountId |> Account.fetchById context
                |> whenNoRows (JournalEntryLineAccountDoesntExist(accountId |> AccountId.value))
            let referenceDate = entryDate |> EntryDate.entryDate
            let activityPeriod = account |> Account.activityPeriod
            return!
                match activityPeriod |> ActivityPeriod.isActive referenceDate with
                | true -> Ok()
                | false ->
                    let accountUuid = accountId |> AccountId.value
                    let entryDateLd = entryDate |> EntryDate.entryDate
                    let beginDate = activityPeriod |> ActivityPeriod.activeBegin
                    let endDate = activityPeriod |> ActivityPeriod.activeEnd
                    Error(JournalEntryLineAccountInactive(accountUuid, entryDateLd, beginDate, endDate))
        }

    let private createValidLines
        (context: Context.Context)
        (journalEntryId: JournalEntryHeaderId)
        (entryDate: EntryDate)
        (lines: (AccountId * Money.Money * JournalEntryLineType * JournalEntryLineMemo option) list)
        : Result<JournalEntryLine.JournalEntryLine list, IAppError> =
        lines
        |> List.map(fun line ->
            let accountId, amount, lineType, memo = line
            result {
                do! accountId |> confirmAccountIsActiveAtEntryDate context entryDate
                return!
                    JournalEntryLineOrchestration.constructNewAndPersist
                        context
                        journalEntryId
                        accountId
                        amount
                        lineType
                        memo
            })
        |> convertListOfResultsToResultsList

    let private createValidExternalReferences
        (context: Context.Context)
        (journalEntryHeaderId: JournalEntryHeaderId)
        (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
        : Result<JournalEntryExternalReference.JournalEntryExternalReference list, IAppError> =
        references
        |> List.map(fun reference ->
            let financialInstitution, referenceText = reference
            JournalEntryExternalReferenceOrchestration.constructNewAndPersist
                context
                journalEntryHeaderId
                financialInstitution
                referenceText)
        |> convertListOfResultsToResultsList

    let private createValidComments
        (context: Context.Context)
        (primaryJournalEntryId: JournalEntryHeaderId)
        (comments: (JournalEntryHeaderId option * CommentText) list)
        : Result<JournalEntryComment.JournalEntryComment list, IAppError> =
        comments
        |> List.map(fun comment ->
            let secondaryJournalEntryId, commentText = comment
            JournalEntryCommentOrchestration.constructNewAndPersist
                context
                primaryJournalEntryId
                secondaryJournalEntryId
                commentText)
        |> convertListOfResultsToResultsList

    let constructNewAndPersist
        (context: Context.Context)
        (description: JournalEntryDescription)
        (source: JournalEntrySource option)
        (entryDate: EntryDate)
        (lines: (AccountId * Money.Money * JournalEntryLineType * JournalEntryLineMemo option) list)
        (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
        (comments: (JournalEntryHeaderId option * CommentText) list)
        : Result<JournalEntry, IAppError> =
        result {
            let! validHeader = createValidHeader context description source entryDate
            let journalEntryHeaderId = validHeader |> JournalEntryHeader.journalEntryHeaderId
            let! validLines = createValidLines context journalEntryHeaderId entryDate lines
            let! validReferences = createValidExternalReferences context journalEntryHeaderId references
            let! validComments = createValidComments context journalEntryHeaderId comments
            do! confirmLineList validLines
            return
                { header = validHeader
                  jeLines = validLines
                  externalReferences = validReferences
                  comments = validComments }
        }

    // =============================================================================
    // Read
    // =============================================================================

    let private composeFromFetchedLists
        (headers: JournalEntryHeader.JournalEntryHeader list)
        (lines: JournalEntryLine.JournalEntryLine list)
        (references: JournalEntryExternalReference.JournalEntryExternalReference list)
        (comments: JournalEntryComment.JournalEntryComment list)
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
              jeLines = linesForHeader
              externalReferences = referencesForHeader
              comments = commentsForHeader })

    let private fetchHeadersFromFilter
        (context: Context.Context)
        (filter: JournalEntryFetchFilter)
        (expectedRows: AcceptableExpectedRows)
        : Result<JournalEntryHeader.JournalEntryHeader list, IAppError> =
        result {
            let! filterDateRangeOption =
                filter.temporalFilter
                |> convertOptionToDesiredTypeWithFallibleConverter (getDateRangeFromTemporalFilter context)
            let dateRange = filterDateRangeOption |> Option.map (fun x -> x.beginDate, x.endInclusive)
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
                      Some "left join ledger.journal_entry_ext_reference jer on je.unique_id = jer.journal_entry_id"
                ]
                |> List.choose id
            let joinOption = if joins |> List.isEmpty then None else Some joins
            let sort = Some "je.entry_date asc"
            let! headersDuplicates =
                JournalEntryHeader.query context joinOption predicate None sort parameters AnyQuantityIsAcceptable
            let deduped = headersDuplicates |> List.distinctBy(fun h -> h |> JournalEntryHeader.journalEntryHeaderId)
            let dedupedCount = deduped |> List.length
            do! confirmNumRows dedupedCount expectedRows
            return deduped
        }

    let fetchFiltered
        (context: Context.Context)
        (filter: JournalEntryFetchFilter)
        (expectedRows: AcceptableExpectedRows)
        : Result<JournalEntry list, IAppError> =
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
        (context: Context.Context)
        (journalEntryHeaderId: JournalEntryHeaderId)
        : Result<JournalEntry, IAppError> =
        let filter =
            { journalEntryHeaderId = Some journalEntryHeaderId
              financialInstitution = None
              referenceText = None
              temporalFilter = None }
        // Note: expected rows of exactly one works here only because we don't
        // have any other filter conditions that would join other tables. In
        // future, if we ever expand this filter or use this function as a
        // template for a new fetch function, know that the deduplication of
        // records happens *after* DAL checks the exactly one condition.
        let expectedRows = ExactlyOne
        fetchFiltered context filter expectedRows
        |> whenNoRows (JournalEntryHeaderIdDoesntExist (journalEntryHeaderId |> JournalEntryHeaderId.value))
        |> Result.map List.head

    let fetchByPeriod
        (context: Context.Context)
        (fiscalPeriod: FiscalPeriod.FiscalPeriod)
        : Result<JournalEntry list, IAppError> =
        let filter =
            { journalEntryHeaderId = None
              financialInstitution = None
              referenceText = None
              temporalFilter =
                Some(fiscalPeriod |> FiscalPeriod.fiscalPeriodId |> TemporalFilter.FiscalPeriodIdentifier) }
        let expectedRows = AnyQuantityIsAcceptable
        fetchFiltered context filter expectedRows

    let fetchByDateRange
        (context: Context.Context)
        (beginDate: LocalDate)
        (endDateInclusive: LocalDate)
        : Result<JournalEntry list, IAppError> =
        if beginDate > endDateInclusive
        then
            Error (JournalEntryFetchByDateRangeBeginAfterEnd (beginDate, endDateInclusive))
        else
            let filter =
                { journalEntryHeaderId = None
                  financialInstitution = None
                  referenceText = None
                  temporalFilter =
                      TemporalFilter.DateRange { beginDate = beginDate; endInclusive = endDateInclusive }
                      |> Some }
            let expectedRows = AnyQuantityIsAcceptable
            fetchFiltered context filter expectedRows

    let fetchByReference
        (context: Context.Context)
        (financialInstitution: JournalRefFinancialInstitution option)
        (referenceText: JournalExternalReferenceText option)
        : Result<JournalEntry list, IAppError> =
        result {
            do!
                if financialInstitution |> Option.isNone && referenceText |> Option.isNone then
                    Error(JournalEntryFetchByReferenceBothArgumentsNull)
                else
                    Ok()
            let filter =
                { journalEntryHeaderId = None
                  financialInstitution = financialInstitution
                  referenceText = referenceText
                  temporalFilter = None }
            let expectedRows = AnyQuantityIsAcceptable
            return! fetchFiltered context filter expectedRows
        }
