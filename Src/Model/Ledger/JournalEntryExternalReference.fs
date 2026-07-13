namespace Model.Ledger.Journaling

open System
open Model.Audit
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.DAL
open Utilities.ResultCE

type JournalRefFinancialInstitution = private JournalRefFinancialInstitution of string
module JournalRefFinancialInstitution =
    let value (JournalRefFinancialInstitution d) = d 
    let create (raw: string) : Result<JournalRefFinancialInstitution, string> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error "JournalRefFinancialInstitution cannot be empty"  // REQ-JE-1.42, REQ-SYS-1.2
        elif trimmed.Length > 100 then
            Error "JournalRefFinancialInstitution cannot exceed 100 characters" // REQ-JE-1.49
        else
            Ok (JournalRefFinancialInstitution trimmed)

type JournalExternalReferenceText = private JournalExternalReferenceText of string
module JournalExternalReferenceText =
    let value (JournalExternalReferenceText d) = d 
    let create (raw: string) : Result<JournalExternalReferenceText, string> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error "JournalExternalReferenceText cannot be empty"  // REQ-JE-1.44, REQ-SYS-1.2
        elif trimmed.Length > 100 then
            Error "JournalExternalReferenceText cannot exceed 100 characters" // REQ-JE-1.45
        else
            Ok (JournalExternalReferenceText trimmed)

type JournalEntryExternalReference =
  private  {    uniqueId: Guid // REQ-JE-1.40
                journalEntryId: JournalEntryId // REQ-JE-1.41
                financialInstitution: JournalRefFinancialInstitution // REQ-JE-1.42
                referenceText: JournalExternalReferenceText
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryExternalReference =
    let uniqueId jer = jer.uniqueId
    let journalEntryId jer = jer.journalEntryId
    let financialInstitution jer = jer.financialInstitution
    let referenceText jer = jer.referenceText
    let createdAt jer = jer.createdAt
    let modifiedAt jer = jer.modifiedAt
    
    let validateJournalEntryHeader
            (transaction: DbTransaction option)
            (journalEntryId: JournalEntryId) 
            : Result<unit, string> =
        journalEntryId |> JournalEntryHeader.fetchById transaction |> Result.map ignore

    /// validateThenConstruct is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private validateThenConstruct
            (uniqueId: Guid) // 
            (journalEntryUuid: Guid) // 
            (financialInstitution: string)
            (referenceText: string)
            (createdAt: Instant) // REQ-SYS-3.2
            (modifiedAt: Instant) // REQ-SYS-3.2
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference, string> =
        result {
            let! validFi = financialInstitution |> JournalRefFinancialInstitution.create
            let! validRefText = referenceText |> JournalExternalReferenceText.create
            let journalEntryId = journalEntryUuid |> JournalEntryId.fromGuid
            do! journalEntryId |> validateJournalEntryHeader transaction |> Result.map ignore
            return { uniqueId = uniqueId; journalEntryId = journalEntryId
                     financialInstitution = validFi; referenceText = validRefText
                     createdAt = createdAt; modifiedAt = modifiedAt } }

    let constructNew
            (journalEntryId: Guid) // 
            (financialInstitution: string)
            (referenceText: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference, string> =
        let uniqueId = Guid.NewGuid() // REQ-JE-2.9
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        validateThenConstruct uniqueId journalEntryId financialInstitution referenceText createdAt modifiedAt transaction
    
    let private insertNewToDb (externalReference:JournalEntryExternalReference) (transaction: DbTransaction option): Result<unit, string> =
        let query = """
            INSERT INTO ledger.journal_entry_ext_reference(
               unique_id, journal_entry_id, financial_institution, reference, created_at, modified_at)
            VALUES (
                @unique_id, @journal_entry_id, @financial_institution, @reference, @created_at, @modified_at);"""
        let journalEntryUuid = externalReference.journalEntryId |> JournalEntryId.value
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId externalReference.uniqueId }
            { name = "@journal_entry_id"; value = UniqueId journalEntryUuid }
            { name = "@financial_institution"; value = CharString (externalReference.financialInstitution |> JournalRefFinancialInstitution.value) };
            { name = "@reference"; value = CharString (externalReference.referenceText |> JournalExternalReferenceText.value) };
            { name = "@created_at"; value = DbInstant externalReference.createdAt };
            { name = "@modified_at"; value = DbInstant externalReference.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne transaction

    let constructNewAndSaveToDb // 
            (journalEntryId: Guid) // 
            (financialInstitution: string)
            (referenceText: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference, string> =
        result {
            let! validJournalExternalReference =
                constructNew journalEntryId financialInstitution referenceText auditEnvelope transaction
            let! () = insertNewToDb validJournalExternalReference transaction
            return validJournalExternalReference }

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let private mapRawForDbRead (row: RowReader) =
            ( row |> RowReader.getUuid "unique_id" ),
            ( row |> RowReader.getUuid "journal_entry_id" ),
            ( row |> RowReader.getString "financial_institution" ),
            ( row |> RowReader.getString "reference" ),
            ( row |> RowReader.getInstant "created_at" ),
            ( row |> RowReader.getInstant "modified_at" )
            
    let private constructFromRawForDbRead
            (transaction: DbTransaction option)
            raw
            : Result<JournalEntryExternalReference, string> =
        let id, jeId, fi, reference, createdAt, modifiedAt = raw
        validateThenConstruct id jeId fi reference createdAt modifiedAt transaction

    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases 
    let private readRowsFromDb
            (predicate: string option)
            (limit: int option)
            (orderBy: string option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference list, string> =
        let select = """
            jer.unique_id, jer.journal_entry_id, jer.financial_institution, jer.reference,
            jer.created_at, jer.modified_at
            """ 
        let from = "ledger.journal_entry_ext_reference jer"
        let query = buildReadQuery select from None predicate limit None orderBy
        executeReaderQuery query parameters mapRawForDbRead constructFromRawForDbRead expectedRows transaction

    let fetchById
            (transaction: DbTransaction option)
            (uniqueId: Guid)
            : Result<JournalEntryExternalReference, string> = 
        let predicate = "jer.unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uniqueId };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None None parameters ExactlyOne transaction
        |> Result.map List.head

    let fetchByJournalEntryId
            (transaction: DbTransaction option)
            (journalEntryId: JournalEntryId)
            : Result<JournalEntryExternalReference list, string> = 
        let uuid = journalEntryId |> JournalEntryId.value
        let predicate = "jer.journal_entry_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uuid };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None None parameters AnyQuantityIsAcceptable transaction
        
    let updateFiAndReferenceText // REQ-JE-4.9
            (auditEnvelope: AuditEnvelope)
            (uniqueId: Guid)
            (newFi: string)
            (newReference: string)
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference, string> = 
        let query = $"""
            UPDATE ledger.journal_entry_ext_reference
            set
                modified_at = @modified -- REQ-SYS-3.3
                , financial_institution = @financial_institution
                , reference = @reference
            WHERE unique_id = @unique_id
            ;
        """
        result {
            let! validFi = JournalRefFinancialInstitution.create newFi
            let! validRef = JournalExternalReferenceText.create newReference
            let parameters = [
                    { name = "@unique_id"; value = UniqueId uniqueId };
                    { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
                    { name = "@financial_institution"; value = CharString (validFi |> JournalRefFinancialInstitution.value)}
                    { name = "@reference"; value = CharString (validRef |> JournalExternalReferenceText.value)}
                ]
            let! _ = executeNonQuery query parameters ExactlyOne transaction
            return! uniqueId |> fetchById transaction
        }
        
        
    


