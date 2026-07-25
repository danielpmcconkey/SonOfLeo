module Model.Ledger.Journaling.JournalEntryComponent

open System
open Model.Ledger.FiscalPeriods
open Utilities.AppError
open NodaTime
open Utilities.ResultHelper
open DataAccessLayer.DbTransaction

type JournalEntryHeaderId = private JournalEntryHeaderId of Guid
module JournalEntryHeaderId =
    let create () : JournalEntryHeaderId = JournalEntryHeaderId(Guid.NewGuid())
    let fromGuid g = JournalEntryHeaderId g
    let value (JournalEntryHeaderId g) : Guid = g

type JournalEntryLineId = private JournalEntryLineId of Guid
module JournalEntryLineId =
    let create () : JournalEntryLineId = JournalEntryLineId(Guid.NewGuid())
    let fromGuid g = JournalEntryLineId g
    let value (JournalEntryLineId g) : Guid = g

type JournalEntryCommentId = private JournalEntryCommentId of Guid
module JournalEntryCommentId =
    let create () : JournalEntryCommentId = JournalEntryCommentId(Guid.NewGuid())
    let fromGuid g = JournalEntryCommentId g
    let value (JournalEntryCommentId g) : Guid = g

type JournalEntryExternalReferenceId = private JournalEntryExternalReferenceId of Guid
module JournalEntryExternalReferenceId =
    let create () : JournalEntryExternalReferenceId =
        JournalEntryExternalReferenceId(Guid.NewGuid())
    let fromGuid g = JournalEntryExternalReferenceId g
    let value (JournalEntryExternalReferenceId g) : Guid = g

type JournalRefFinancialInstitution = private JournalRefFinancialInstitution of string
module JournalRefFinancialInstitution =
    let max = 100
    let value (JournalRefFinancialInstitution d) = d
    let create (raw: string) : Result<JournalRefFinancialInstitution, AppError> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryExternalReferenceIsEmpty raw) // REQ-JE-1.42, REQ-SYS-1.2
        elif trimmed.Length > max then
            Error(JournalEntryExternalReferenceTooLong(raw, max)) // REQ-JE-1.49
        else
            Ok(JournalRefFinancialInstitution trimmed)

type JournalExternalReferenceText = private JournalExternalReferenceText of string
module JournalExternalReferenceText =
    let value (JournalExternalReferenceText d) = d
    let create (raw: string) : Result<JournalExternalReferenceText, AppError> =
        let max = 100
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryReferenceTextIsEmpty raw) // REQ-JE-1.44, REQ-SYS-1.2
        elif trimmed.Length > max then
            Error(JournalEntryReferenceTextTooLong(raw, max)) // REQ-JE-1.45
        else
            Ok(JournalExternalReferenceText trimmed)

type JournalEntryDescription = private JournalEntryDescription of string

module JournalEntryDescription =
    let value (JournalEntryDescription d) = d
    let create (raw: string) : Result<JournalEntryDescription, AppError> =
        let max = 1000
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryDescriptionIsEmpty raw) // REQ-JE-1.4, REQ-SYS-1.2
        elif trimmed.Length > max then
            Error(JournalEntryDescriptionTooLong(raw, max)) // REQ-JE-1.5
        else
            Ok(JournalEntryDescription trimmed)

type JournalEntrySource = private JournalEntrySource of string

module JournalEntrySource =
    let value (JournalEntrySource d) = d
    let create (raw: string) : Result<JournalEntrySource, AppError> =
        let max = 50
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntrySourceIsEmpty raw) // REQ-JE-1.7, REQ-SYS-1.2
        elif trimmed.Length > max then
            Error(JournalEntrySourceTooLong(raw, max)) // REQ-JE-1.8
        else
            Ok(JournalEntrySource trimmed)

type EntryDate =
    private
        { entryDate: LocalDate // REQ-JE-1.10
          fiscalPeriodId: FiscalPeriodId }

module EntryDate =
    let entryDate (e: EntryDate) : LocalDate = e.entryDate
    let fiscalPeriodId (e: EntryDate) : FiscalPeriodId = e.fiscalPeriodId
    let create (transaction: DbTransaction) (entryDate: LocalDate) : Result<EntryDate, AppError> = // REQ-JE-2.5
        let monthF = entryDate.Month.ToString("D2")
        result {
            let key = $"{entryDate.Year}-{monthF}" // REQ-JE-1.11
            let! id =
                key
                |> FiscalPeriod.fetchIdByKey transaction // REQ-JE-2.6
                |> Result.mapError(fun _ -> (JournalEntryDateNotInFiscalPeriod entryDate))
            return { entryDate = entryDate; fiscalPeriodId = id }
        }

    /// createWithFiscalPeriodId is used by functions in the model who are
    /// already have what they believe is a valid FP ID. We use this so we can
    /// avoid a DB lookup in the middle of a spooling DB read.
    ///
    /// WARNING: this very much assumes that you know what you're doing and
    /// that you are certain that your date matches the period ID (ie, you
    /// reconstituted it directly from the DB without modification).
    let internal createWithFiscalPeriodId (entryDate: LocalDate) (fiscalPeriodId: FiscalPeriodId) : EntryDate =
        { entryDate = entryDate; fiscalPeriodId = fiscalPeriodId }

type JournalEntryLineType = // REQ-JE-1.25
    | Debit
    | Credit

module JournalEntryLineType =
    let fromString (s: string) : Result<JournalEntryLineType, AppError> =
        match s.Trim() with
        | "Debit" -> Ok Debit
        | "Credit" -> Ok Credit
        | _ -> Error(JournalEntryLineTypeInvalid s)

    let toString s =
        match s with
        | Debit -> "Debit"
        | Credit -> "Credit"

type JournalEntryLineMemo = private LineMemo of string

module JournalEntryLineMemo =
    let value (LineMemo d) = d
    let create (raw: string) : Result<JournalEntryLineMemo, AppError> =
        let max = 1000
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryLineMemoIsEmpty raw) // REQ-JE-1.27, REQ-SYS-1.2
        elif trimmed.Length > max then
            Error(JournalEntryLineMemoTooLong(raw, max)) // REQ-JE-1.28
        else
            Ok(LineMemo trimmed)
type CommentText = private CommentText of string
module CommentText =
    let max = 2000
    let value (CommentText d) = d
    let create (raw: string) : Result<CommentText, AppError> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryCommentIsEmpty raw) // REQ-JE-1.54, REQ-SYS-1.2
        elif trimmed.Length > max then
            Error(JournalEntryCommentTooLong(raw, max)) // REQ-JE-1.54
        else
            Ok(CommentText trimmed)
