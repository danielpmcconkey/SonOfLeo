namespace Model.Ledger.Journaling

open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultHelper

type JournalEntryExternalReference =
    private
        { journalEntryExternalReferenceId: JournalEntryExternalReferenceId // REQ-JE-1.40
          journalEntryHeaderId: JournalEntryHeaderId // REQ-JE-1.41
          financialInstitution: JournalRefFinancialInstitution // REQ-JE-1.42
          referenceText: JournalExternalReferenceText
          createdAt: Instant
          modifiedAt: Instant }

module JournalEntryExternalReference =
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
        (createdAt: Instant) // REQ-SYS-3.2
        (modifiedAt: Instant) // REQ-SYS-3.2
        : JournalEntryExternalReference =
        { journalEntryExternalReferenceId = journalEntryExternalReferenceId
          journalEntryHeaderId = journalEntryHeaderId
          financialInstitution = financialInstitution
          referenceText = referenceText
          createdAt = createdAt
          modifiedAt = modifiedAt }

    let insertNewToDb
        (externalReference: JournalEntryExternalReference)
        (transaction: DbTransaction option)
        : Result<unit, AppError> =
        let query =
            """
            INSERT INTO ledger.journal_entry_ext_reference(
               unique_id, journal_entry_id, financial_institution, reference, created_at, modified_at)
            VALUES (
                @unique_id, @journal_entry_id, @financial_institution, @reference, @created_at, @modified_at);"""
        let journalEntryExternalReferenceUuid =
            externalReference.journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
        let journalEntryUuid = externalReference.journalEntryHeaderId |> JournalEntryHeaderId.value
        let parameters =
            [ //  REQ-DAL-2.1, REQ-DAL-2.3
              { name = "@unique_id"; value = UniqueId journalEntryExternalReferenceUuid }
              { name = "@journal_entry_id"; value = UniqueId journalEntryUuid }
              { name = "@financial_institution"
                value = CharString(externalReference.financialInstitution |> JournalRefFinancialInstitution.value) }
              { name = "@reference"
                value = CharString(externalReference.referenceText |> JournalExternalReferenceText.value) }
              { name = "@created_at"; value = DbInstant externalReference.createdAt }
              { name = "@modified_at"; value = DbInstant externalReference.modifiedAt } ]
        executeNonQuery query parameters ExactlyOne transaction


    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here
    let private mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getUuid "unique_id"),
        (row |> RowReader.getUuid "journal_entry_id"),
        (row |> RowReader.getString "financial_institution"),
        (row |> RowReader.getString "reference"),
        (row |> RowReader.getInstant "created_at"),
        (row |> RowReader.getInstant "modified_at")

    /// reconstitute constructs from primitives, performing zero validation at
    /// the collective level. All fields are assumed to have come from a
    /// trusted source (e.g. the database) where such validation occurred at
    /// the time of writing the entity. Important: no additional DB lookups can
    /// be triggered inside this function since it is called within a database
    /// reader.
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


    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases
    let private readRowsFromDb
        (predicate: string option)
        (limit: int option)
        (orderBy: string option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        (transaction: DbTransaction option)
        : Result<JournalEntryExternalReference list, AppError> =
        let select =
            """
            jer.unique_id, jer.journal_entry_id, jer.financial_institution, jer.reference,
            jer.created_at, jer.modified_at
            """
        let from = "ledger.journal_entry_ext_reference jer"
        let query = buildReadQuery select from None predicate limit None orderBy
        executeReaderQuery query parameters mapRawForDbRead reconstitute expectedRows transaction

    let fetchById
        (transaction: DbTransaction option)
        (journalEntryExternalReferenceId: JournalEntryExternalReferenceId)
        : Result<JournalEntryExternalReference, AppError> =
        let uuid = journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
        let predicate = "jer.unique_id = @unique_id"
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None None parameters ExactlyOne transaction |> Result.map List.head

    let fetchByJournalEntryId
        (transaction: DbTransaction option)
        (journalEntryId: JournalEntryHeaderId)
        : Result<JournalEntryExternalReference list, AppError> =
        let uuid = journalEntryId |> JournalEntryHeaderId.value
        let predicate = "jer.journal_entry_id = @unique_id"
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None None parameters AnyQuantityIsAcceptable transaction

    let fetchByJournalEntryHeaderIdList
        (transaction: DbTransaction option)
        (journalEntryHeaderIds: JournalEntryHeaderId list)
        : Result<JournalEntryExternalReference list, AppError> =
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
        readRowsFromDb (Some predicate) None None parameters AnyQuantityIsAcceptable transaction
