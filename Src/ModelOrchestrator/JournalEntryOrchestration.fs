namespace ModelOrchestrator.JournalEntries

open System
open Model
open Model.Audit
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.Accounts
open ModelOrchestrator
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.DAL
open Utilities.ListHelper
open Utilities.ResultCE

type JournalEntry =
  private  {    header: JournalEntryHeader
                lines: JournalEntryLine list
                externalReferences: JournalEntryExternalReference list // REQ-JE-1.46
                comments: JournalEntryComment list } // REQ-JE-1.55

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
                if totalCredits = totalDebits then Ok ()
                else Error (JournalEntryDebitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
            }
    
    let private validateLineCount (lines: JournalEntryLine list) : Result<unit, AppError> =
        if lines |> List.length < 2
        then Error (JournalEntryInsufficientLines (lines |> List.length)) // REQ-JE-1.12
        else Ok ()
        
    let validateLineList (lines: JournalEntryLine list) : Result<unit, AppError> =
        result {
            let! _ = validateLineCount lines // REQ-JE-1.12
            let! _ = validateAmountEquality lines // REQ-JE-1.13
            return ()
        }

    // =============================================================================
    // Create
    // =============================================================================
    
    let private createValidHeader
            (description: JournalEntryDescription)
            (source: JournalEntrySource option)
            (entryDate: EntryDate)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, AppError> =
        JournalEntryHeaderOrchestration.constructNewAndSaveToDb description source entryDate auditEnvelope transaction
    
    let private confirmAccountIsActiveAtEntryDate
            (transaction: DbTransaction option)
            (entryDate: EntryDate)
            (accountId: AccountId)
            : Result<unit, AppError> =
        result {
            let! account =
                match accountId |> Account.fetchById transaction with
                | Ok a -> Ok a
                | Error (DalResultantRowsDidntMatchExpectation _ ) ->
                    Error (JournalEntryLineAccountDoesntExist (accountId |> AccountId.value))
                | Error e -> Error e
            let referenceDate = entryDate |> EntryDate.entryDate
            let activityPeriod = account |> Account.activityPeriod
            return!
                match activityPeriod |> AccountActivityPeriod.isActive referenceDate with
                | true -> Ok ()
                | false ->
                    let accountUuid = accountId |> AccountId.value
                    let entryDateLd = entryDate |> EntryDate.entryDate
                    let beginDate = activityPeriod |> AccountActivityPeriod.activeBegin
                    let endDate = activityPeriod |> AccountActivityPeriod.activeEnd
                    Error (JournalEntryLineAccountInactive(accountUuid, entryDateLd, beginDate, endDate))  // REQ-JE-2.8
        }
    
    let private createValidLines
            (journalEntryId : JournalEntryHeaderId)
            (entryDate: EntryDate)
            (lines: (AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine list, AppError> =
        lines
        |> List.map(fun line ->
            let accountId, amount, lineType, memo = line
            result {    do! accountId |> confirmAccountIsActiveAtEntryDate transaction entryDate // REQ-JE-2.8
                        return! JournalEntryLineOrchestration.constructNewAndSaveToDb
                            journalEntryId
                            accountId
                            amount
                            lineType
                            memo
                            auditEnvelope
                            transaction })
        |> listOfResultsToResultsList
    
    let private createValidExternalReferences
            (journalEntryHeaderId: JournalEntryHeaderId)
            (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference list, AppError> =
        references
        |> List.map(fun reference ->
            let financialInstitution, referenceText = reference
            JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                journalEntryHeaderId
                financialInstitution
                referenceText
                auditEnvelope
                transaction) 
        |> listOfResultsToResultsList
    
    let private createValidComments
            (primaryJournalEntryId: JournalEntryHeaderId)
            (comments: (JournalEntryHeaderId option * CommentText) list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryComment list, AppError> =
        comments
        |> List.map(fun comment ->
            let secondaryJournalEntryId, commentText = comment
            JournalEntryCommentOrchestration.constructNewAndSaveToDb
                primaryJournalEntryId
                secondaryJournalEntryId
                commentText
                auditEnvelope
                transaction) 
        |> listOfResultsToResultsList

    /// constructNewAndSaveToDb validates that the components work together to
    /// form a valid whole before adding it to the persistence layer. All new
    /// Journal Entry creation should route through here before being sent to the
    /// persistence layer. Internal model functions may construct through other
    /// means if they're operating on known good data. 
    let constructNewAndSaveToDb // REQ-JE-2.13
            (description: JournalEntryDescription)
            (source: JournalEntrySource option)
            (entryDate: EntryDate)
            (lines: (AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list)
            (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
            (comments: (JournalEntryHeaderId option * CommentText) list)
            (auditEnvelope: AuditEnvelope)
            : Result<JournalEntry, AppError> =
        let transaction = createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)) // if this fails, nothing can proceed
        let railRoad = result {
            let! validHeader = createValidHeader description source entryDate auditEnvelope (Some transaction)
            let journalEntryHeaderId = validHeader |> JournalEntryHeader.journalEntryHeaderId
            let! validLines = createValidLines journalEntryHeaderId entryDate lines auditEnvelope (Some transaction)
            let! validReferences = createValidExternalReferences journalEntryHeaderId references auditEnvelope (Some transaction)
            let! validComments = createValidComments journalEntryHeaderId comments auditEnvelope (Some transaction)
            do! validateLineList validLines
            return {    header = validHeader
                        lines = validLines
                        externalReferences = validReferences
                        comments = validComments } }
        match railRoad with // REQ-JE-2.11
        | Error e ->
            transaction
            |> rollbackDbTransactionAndDisposeConnection
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)) // REQ-JE-2.12, REQ-JE-2.8, REQ-JE-1.12, REQ-JE-1.13
            Error e
        | Ok je ->
            transaction
            |> commitDbTransactionAndDisposeConnection
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
            Ok je

    // =============================================================================
    // Read
    // =============================================================================

    let private mapRawForDbRead (row: RowReader) =
        ( row |> RowReader.getUuid "je_id"), 
        ( row |> RowReader.getString "description"), 
        ( row |> RowReader.getStringOption "je_source"), 
        ( row |> RowReader.getDate "entry_date"), 
        ( row |> RowReader.getUuid "fiscal_period_id"), 
        ( row |> RowReader.getBoolOption "voided_at"), 
        ( row |> RowReader.getInstant "je_created_at"), 
        ( row |> RowReader.getInstant "je_modified_at"),
        ( row |> RowReader.getUuidOption "jel_id"), 
        ( row |> RowReader.getUuidOption "account_id"), 
        ( row |> RowReader.getNumericOption "amount"), 
        ( row |> RowReader.getStringOption "line_type"), 
        ( row |> RowReader.getStringOption "memo"), 
        ( row |> RowReader.getInstantOption "jel_created_at"), 
        ( row |> RowReader.getInstantOption "jel_modified_at"),
        ( row |> RowReader.getUuidOption "jer_id"), 
        ( row |> RowReader.getStringOption "financial_institution"), 
        ( row |> RowReader.getStringOption "reference"), 
        ( row |> RowReader.getInstantOption "jer_created_at"), 
        ( row |> RowReader.getInstantOption "jer_modified_at"),
        ( row |> RowReader.getUuidOption "jec_id"), 
        ( row |> RowReader.getUuidOption "journal_secondary_entry_id"), 
        ( row |> RowReader.getStringOption "comment_text"), 
        ( row |> RowReader.getInstantOption "jec_created_at"), 
        ( row |> RowReader.getInstantOption "jec_modified_at")
    
    /// restateRaw is here because the DAL reader function doesn't allow us to
    /// reconstitute at aggregate. But it does require a reconstitution
    /// function. So this is just a simple passthrough.
    let private restateRaw raw = Ok raw
    
    let private buildJournalEntriesFromFetchFiltered rawRows : JournalEntry list =
        
        
    let fetchFiltered
            (transaction: DbTransaction option)
            (filter: JournalEntryFetchFilter)
            (sort: FetchSort option)
            : Result<JournalEntryHeaderId list, AppError> =
        result {
            let! dateRange = 
                match filter.temporalFilter with
                | None -> Ok None
                | Some (DateRange dr) -> Ok (Some (dr.beginDate, dr.endInclusive))
                | Some (FiscalPeriodIdentifier fpId) ->
                    fpId
                    |> FiscalPeriod.fetchById transaction
                    |> Result.map (fun fp -> Some (fp |> FiscalPeriod.startDate, fp |> FiscalPeriod.endDate))
            let voidClause = if filter.unVoidedOnly then "and je.voided_at is null" else ""
            let sortClause =
                match sort with
                | None -> ""
                | Some AccountCodeAsc -> "order by a.code asc"
                | Some AccountCodeDesc -> "order by a.code desc"
                | Some EntryDateAsc -> "order by je.entry_date asc"
                | Some EntryDateDesc -> "order by je.entry_date desc"
                | Some AmountAsc -> "order by jel.amount asc"
                | Some AmountDesc -> "order by jel.amount desc"
            let whereClausesAndParams =
                [
                    filter.journalEntryHeaderId |> Option.map (
                    fun x -> ("and je.unique_id = @header_id",
                              { name = "@header_id"; value = UniqueId (x |> JournalEntryHeaderId.value) }))
                    dateRange |> Option.map (
                        fun (x, _) -> ("and je.entry_date >= @begin_date", { name = "@begin_date"; value = DbLocalDate x }))
                    dateRange |> Option.map (
                        fun (_, x) -> ("and je.entry_date <= @end_date", { name = "@end_date"; value = DbLocalDate x }))
                    filter.source |> Option.map (
                        fun x -> ("and je.je_source = @je_source",
                                  { name = "@je_source"; value = CharString (x |> JournalEntrySource.value) }))
                    filter.financialInstitution |> Option.map ( fun x -> (
                        let fiString = x |> JournalRefFinancialInstitution.value
                        "and jer.financial_institution = @financial_institution",
                        { name = "@financial_institution"; value = CharString fiString }))
                    filter.referenceText |> Option.map ( fun x -> (
                        let refString = x |> JournalExternalReferenceText.value
                        "and jer.reference = @reference", { name = "@reference"; value = CharString refString }))
                ] |> List.choose id
            let whereClauses = whereClausesAndParams |> List.map fst |> String.concat Environment.NewLine
            let parameters = whereClausesAndParams |> List.map snd
            let query = $"""
                SELECT 
                    je.unique_id as je_id, je.description, je.je_source, je.entry_date, je.fiscal_period_id, je.voided_at, je.created_at as je_created_at, je.modified_at as je_modified_at,
                    jel.unique_id as jel_id, jel.account_id, jel.amount, jel.line_type, jel.memo, jel.created_at as jel_created_at, jel.modified_at as jel_modified_at,
                    jer.unique_id as jer_id, jer.financial_institution, jer.reference, jer.created_at as jer_created_at, jer.modified_at as jer_modified_at,
                    jec.unique_id as jec_id, jec.journal_secondary_entry_id, jec.comment_text, jec.created_at as jec_created_at, jec.modified_at as jec_modified_at
                FROM ledger.journal_entry je
                left join ledger.journal_entry_line jel on je.unique_id = jel.journal_entry_id
                left join ledger.journal_entry_ext_reference jer on je.unique_id = jer.journal_entry_id
                left join ledger.journal_entry_comment jec on je.unique_id = jec.journal_primary_entry_id
                left join ledger.account a on jel.account_id = a.unique_id            
                where 1 = 1
                {whereClauses}
                {voidClause}
                {sortClause}
                """
            let! reconstitutedRows = executeReaderQuery query parameters mapRawForDbRead restateRaw AnyQuantityIsAcceptable transaction
            return! reconstitutedRows |> buildJournalEntriesFromFetchFiltered }
        
        
    let private fetchHeaderIdsByReference // REQ-JE-3.5, REQ-JE-3.8
            (transaction: DbTransaction option)
            (financialInstitution: JournalRefFinancialInstitution option)
            (referenceText: JournalExternalReferenceText option)
            : Result<JournalEntryHeaderId list, AppError> =
        let mapRaw (row: RowReader) =
            (row |> RowReader.getUuid "unique_id") , ()
        let constructRaw
            raw
            : Result<JournalEntryHeaderId,AppError> =
            let uuid, _ = raw
            Ok (uuid |> JournalEntryHeaderId.fromGuid)
        if financialInstitution = None && referenceText = None then Error (JournalEntryFetchByReference ()) // there is no req for reads where an error silently succeeds
        else 
            let whereClausesAndParams =
                [
                    financialInstitution |> Option.map ( fun x -> (
                        let fiString = x |> JournalRefFinancialInstitution.value
                        "and jer.financial_institution = @financial_institution",
                        { name = "@financial_institution"; value = CharString fiString }))
                    referenceText |> Option.map ( fun x -> (
                        let refString = x |> JournalExternalReferenceText.value
                        "and jer.reference = @reference", { name = "@reference"; value = CharString refString }))
                ] |> List.choose id
            let whereClauses = whereClausesAndParams |> List.map fst |> String.concat Environment.NewLine
            let parameters = whereClausesAndParams |> List.map snd // REQ-DAL-2.3
            let query = $"""
                SELECT je.unique_id
                FROM ledger.journal_entry je
                left join ledger.journal_entry_ext_reference jer on je.unique_id = jer.journal_entry_id
                where 1 = 1
                {whereClauses}
                order by je.entry_date asc
                ;"""
            result {
                let! fullList = executeReaderQuery query parameters mapRaw constructRaw AnyQuantityIsAcceptable transaction
                return fullList |> List.distinct } // the distinct is here because one JE might have multiple refs with the same reference

    let private fetchHeaderIdsByDateRange // REQ-JE-3.7
            (transaction: DbTransaction option)
            (beginDate: LocalDate)
            (endDateInclusive: LocalDate)
            : Result<Guid list, AppError> =
        let mapRaw (row: RowReader) =
            (row |> RowReader.getUuid "unique_id") , ()
        let constructRaw _transaction raw :Result<Guid, AppError> =
            let id, _ = raw
            Ok id
        let query = """
            SELECT je.unique_id
            FROM ledger.journal_entry je
            where je.entry_date >= @begin_date and je.entry_date <= @end_date
            order by je.entry_date asc
            ;"""
        let parameters = [  { name = "@begin_date"; value = DbLocalDate beginDate };
                            { name = "@end_date"; value = DbLocalDate endDateInclusive }; ] // REQ-DAL-2.3
        executeReaderQuery query parameters mapRaw constructRaw AnyQuantityIsAcceptable transaction



    let fetchById // REQ-JE-3.1, REQ-JE-3.2
            (journalEntryHeaderId: JournalEntryHeaderId)
            : Result<JournalEntry, AppError> =  result {
        let! validHeader = journalEntryHeaderId |> JournalEntryHeader.fetchById None
        let! validLines = journalEntryHeaderId |> JournalEntryLine.fetchByJournalEntryId None
        let! validReferences = journalEntryHeaderId |> JournalEntryExternalReference.fetchByJournalEntryId None
        let! validComments = journalEntryHeaderId |> JournalEntryComment.fetchByJournalEntryId None
        return {    header = validHeader
                    lines = validLines
                    externalReferences = validReferences
                    comments = validComments } }

    let fetchByPeriod // REQ-JE-3.1
            (fiscalPeriodId: FiscalPeriodId)
            : Result<JournalEntry list, AppError> =
        result {
            let! headers = fiscalPeriodId |> JournalEntryHeader.fetchByPeriod None
            let headerResultsList = headers |> List.map(fun h ->
                let id = JournalEntryHeader.journalEntryHeaderId h
                let entryResult = fetchById id 
                entryResult)
            return! headerResultsList |> listOfResultsToResultsList
        }
        
    let fetchByReference // REQ-JE-3.1, REQ-JE-3.5, REQ-JE-3.8
            (fi: JournalRefFinancialInstitution option)
            (reference: JournalExternalReferenceText option)
            : Result<JournalEntry list, AppError> =
        result {
            let! headers = fetchHeaderIdsByReference None fi reference
            let headerResultsList = headers |> List.map(fun h -> h |> fetchById)
            return! headerResultsList |> listOfResultsToResultsList
        }

    let fetchByDateRange // REQ-JE-3.7
            (beginDate: LocalDate)
            (endDateInclusive: LocalDate)
            : Result<JournalEntry list, AppError> =
        result {
            let! headers = fetchHeaderIdsByDateRange None beginDate endDateInclusive
            let headerResultsList = headers |> List.map(fun h -> h |> fetchById)
            return! headerResultsList |> listOfResultsToResultsList
        }
