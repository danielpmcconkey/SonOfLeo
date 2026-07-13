module ModelOrchestrator.JournalEntryFetching

open System
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open NodaTime
open Utilities.DAL
open Utilities.ListHelper
open Utilities.ResultCE
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction

let private fetchHeaderIdsByReference // REQ-JE-3.5, REQ-JE-3.8
        (transaction: DbTransaction option)
        (fi: string option)
        (reference: string option)
        : Result<Guid list, string> =
    let mapRaw (row: RowReader) =
        (row |> RowReader.getUuid "unique_id") , ()
    let constructRaw _transaction raw :Result<Guid,string> =
        let id, _ = raw
        Ok id
    if fi = None && reference = None
    then Error "Both FI and reference cannot both be null"
    else 
        let whereClausesAndParams =
            [
                fi |> Option.map ( fun x -> (
                    "and jer.financial_institution = @financial_institution", { name = "@financial_institution"; value = CharString x }))
                reference |> Option.map ( fun x -> (
                    "and jer.reference = @reference", { name = "@reference"; value = CharString x }))
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
        : Result<Guid list, string> =
    let mapRaw (row: RowReader) =
        (row |> RowReader.getUuid "unique_id") , ()
    let constructRaw _transaction raw :Result<Guid,string> =
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
        (uniqueId: Guid)
        : Result<JournalEntry, string> =
    result {
        let! validHeader = uniqueId |> JournalEntryHeader.fetchById None
        let! validLines = uniqueId |> JournalEntryLine.fetchByJournalEntryId None
        let! validReferences = uniqueId |> JournalEntryExternalReference.fetchByJournalEntryId None
        let! validComments = uniqueId |> JournalEntryComment.fetchByJournalEntryId None
        return! constructFromPreValidatedComponents validHeader validLines validReferences validComments
    }

let fetchByPeriod // REQ-JE-3.1
        (fiscalPeriodId: FiscalPeriodId)
        : Result<JournalEntry list, string> =
    result {
        let! headers = fiscalPeriodId |> JournalEntryHeader.fetchByPeriod None
        let headerResultsList = headers |> List.map(fun h ->
            let id = JournalEntryHeader.journalEntryId h
            let entryResult = fetchById id 
            entryResult)
        return! headerResultsList |> listOfResultsToResultsList
    }
    
let fetchByReference // REQ-JE-3.1, REQ-JE-3.5, REQ-JE-3.8
        (fi: string option)
        (reference: string option)
        : Result<JournalEntry list, string> =
    result {
        let! headers = fetchHeaderIdsByReference None fi reference
        let headerResultsList = headers |> List.map(fun h -> h |> fetchById)
        return! headerResultsList |> listOfResultsToResultsList
    }

let fetchByDateRange // REQ-JE-3.7
        (beginDate: LocalDate)
        (endDateInclusive: LocalDate)
        : Result<JournalEntry list, string> =
    result {
        let! headers = fetchHeaderIdsByDateRange None beginDate endDateInclusive
        let headerResultsList = headers |> List.map(fun h -> h |> fetchById)
        return! headerResultsList |> listOfResultsToResultsList
    }
