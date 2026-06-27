module ModelOrchestrator.JournalEntryFetching

open System
open Model.Ledger.Journaling
open Utilities.DAL
open Utilities.ListHelper
open Utilities.ResultCE
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction

let private fetchHeaderIdsByReference
        (fi: string)
        (reference: string)
        : Result<Guid list, string> =
    let mapRowForDbRead (row: RowReader) : Result<Guid, string> =
        Ok ( row |> RowReader.getUuid "unique_id" )            
    let query = """
        SELECT je.unique_id
        FROM ledger.journal_entry je
        left join ledger.journal_entry_ext_reference jer on je.unique_id = jer.journal_entry_id
        where jer.financial_institution = @financial_institution
        and jer.reference = @reference
        ;"""
    let parameters = [
        { name = "@financial_institution"; value = CharString fi };
        { name = "@reference"; value = CharString reference };
    ] // REQ-DAL-2.3
    executeReaderQuery query parameters mapRowForDbRead AnyQuantityIsAcceptable None

let fetchById
        (uniqueId: Guid)
        : Result<JournalEntry, string> =
    result {
        let! validHeader = uniqueId |> JournalEntryHeader.fetchById None
        let! validLines = uniqueId |> JournalEntryLine.fetchByJournalEntryId None
        let! validReferences = uniqueId |> JournalEntryExternalReference.fetchByJournalEntryId None
        let! validComments = uniqueId |> JournalEntryComment.fetchByJournalEntryId None
        return! constructFromPreValidatedComponents validHeader validLines validReferences validComments
    }

let fetchByPeriodKey
        (key: string)
        : Result<JournalEntry list, string> =
    result {
        let! headers = key |> JournalEntryHeader.fetchByPeriodKey None
        let headerResultsList = headers |> List.map(fun h ->
            let id = JournalEntryHeader.uniqueId h
            let entryResult = fetchById id 
            entryResult)
        return! headerResultsList |> listOfResultsToResultsList
    }
    
let fetchByReference
        (fi: string)
        (reference: string)
        : Result<JournalEntry list, string> =
    result {
        let! headers = fetchHeaderIdsByReference fi reference
        let headerResultsList = headers |> List.map(fun h -> h |> fetchById)
        return! headerResultsList |> listOfResultsToResultsList
    }