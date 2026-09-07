module Model.Ledger.JournalEntryExternalReference

open Model.Ledger.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery


type JournalEntryExternalReference =
    private
        { journalEntryExternalReferenceId: JournalEntryExternalReferenceId
          journalEntryHeaderId: JournalEntryHeaderId
          financialInstitution: JournalRefFinancialInstitution
          referenceText: JournalExternalReferenceText
          createdAt: Instant
          modifiedAt: Instant }

let journalEntryExternalReferenceId jer = jer.journalEntryExternalReferenceId
let journalEntryHeaderId jer = jer.journalEntryHeaderId
let financialInstitution jer = jer.financialInstitution
let referenceText jer = jer.referenceText
let createdAt jer = jer.createdAt
let modifiedAt jer = jer.modifiedAt

let create
    (journalEntryExternalReferenceId: JournalEntryExternalReferenceId)
    (journalEntryHeaderId: JournalEntryHeaderId)
    (financialInstitution: JournalRefFinancialInstitution)
    (referenceText: JournalExternalReferenceText)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : JournalEntryExternalReference =
    { journalEntryExternalReferenceId = journalEntryExternalReferenceId
      journalEntryHeaderId = journalEntryHeaderId
      financialInstitution = financialInstitution
      referenceText = referenceText
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (externalReference: JournalEntryExternalReference) : Result<unit, AppError> =
    let queryStatement =
        """
        INSERT INTO ledger.journal_entry_ext_reference(
           unique_id, journal_entry_id, financial_institution, reference, created_at, modified_at)
        VALUES (
            @unique_id, @journal_entry_id, @financial_institution, @reference, @created_at, @modified_at);"""
    let journalEntryExternalReferenceUuid =
        externalReference.journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
    let journalEntryUuid = externalReference.journalEntryHeaderId |> JournalEntryHeaderId.value
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId journalEntryExternalReferenceUuid }
          { name = "@journal_entry_id"; value = UniqueId journalEntryUuid }
          { name = "@financial_institution"
            value = CharString(externalReference.financialInstitution |> JournalRefFinancialInstitution.value) }
          { name = "@reference"
            value = CharString(externalReference.referenceText |> JournalExternalReferenceText.value) }
          { name = "@created_at"; value = DbInstant externalReference.createdAt }
          { name = "@modified_at"; value = DbInstant externalReference.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "journal_entry_id"),
    (row |> RowReader.getString "financial_institution"),
    (row |> RowReader.getString "reference"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private reconstitute raw : Result<JournalEntryExternalReference, AppError> =
    let uuid, journalEntryUuid, financialInstitutionStr, referenceTextStr, createdAt, modifiedAt = raw
    let journalEntryExternalReferenceId = uuid |> JournalEntryExternalReferenceId.fromGuid
    let journalEntryId = journalEntryUuid |> JournalEntryHeaderId.fromGuid
    result {
        let! financialInstitution = financialInstitutionStr |> JournalRefFinancialInstitution.create
        let! referenceText = referenceTextStr |> JournalExternalReferenceText.create
        return
            create
                journalEntryExternalReferenceId
                journalEntryId
                financialInstitution
                referenceText
                createdAt
                modifiedAt
    }

let private query
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<JournalEntryExternalReference list, AppError> =
    let select =
        """
        jer.unique_id, jer.journal_entry_id, jer.financial_institution, jer.reference,
        jer.created_at, jer.modified_at
        """
    let from = "ledger.journal_entry_ext_reference jer"
    let queryStatement = buildReadQuery None select from None predicate limit None orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById
    (context: Context.Context)
    (journalEntryExternalReferenceId: JournalEntryExternalReferenceId)
    : Result<JournalEntryExternalReference, AppError> =
    let uuid = journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
    let predicate = "jer.unique_id = @unique_id"
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    query context (Some predicate) None None parameters ExactlyOne |> Result.map List.head

let fetchByJournalEntryId
    (context: Context.Context)
    (journalEntryId: JournalEntryHeaderId)
    : Result<JournalEntryExternalReference list, AppError> =
    let uuid = journalEntryId |> JournalEntryHeaderId.value
    let predicate = "jer.journal_entry_id = @unique_id"
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    query context (Some predicate) None None parameters AnyQuantityIsAcceptable

let fetchByJournalEntryHeaderIdList
    (context: Context.Context)
    (journalEntryHeaderIds: JournalEntryHeaderId list)
    : Result<JournalEntryExternalReference list, AppError> =
    if journalEntryHeaderIds |> List.isEmpty then Error JournalEntryHeaderIdListCannotBeEmpty else
    let ordinals = [ 1 .. journalEntryHeaderIds.Length ]
    let zipped = List.zip ordinals journalEntryHeaderIds
    let namesAndParameters =
        zipped
        |> List.map(fun (ordinal, id) ->
            let uuid = id |> JournalEntryHeaderId.value
            let name = $"@journal_entry_id{ordinal}"
            let parameter = { name = name; value = UniqueId uuid }
            name, parameter)
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"jer.journal_entry_id in ({names})"
    query context (Some predicate) None None parameters AnyQuantityIsAcceptable
