module Business.FinancialServices.Ledger.JournalEntryComponent

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.FiscalPeriodComponent

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
    let create (raw: string) : Result<JournalRefFinancialInstitution, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalRefFinancialInstitutionIsEmpty raw)
        elif trimmed.Length > max then
            Error(JournalRefFinancialInstitutionTooLong(raw, max))
        else
            Ok(JournalRefFinancialInstitution trimmed)

type JournalExternalReferenceText = private JournalExternalReferenceText of string
module JournalExternalReferenceText =
    let value (JournalExternalReferenceText d) = d
    let create (raw: string) : Result<JournalExternalReferenceText, IAppError> =
        let max = 100
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryReferenceTextIsEmpty raw)
        elif trimmed.Length > max then
            Error(JournalEntryReferenceTextTooLong(raw, max))
        else
            Ok(JournalExternalReferenceText trimmed)

type JournalEntryDescription = private JournalEntryDescription of string

module JournalEntryDescription =
    let value (JournalEntryDescription d) = d
    let create (raw: string) : Result<JournalEntryDescription, IAppError> =
        let max = 1000
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryDescriptionIsEmpty raw)
        elif trimmed.Length > max then
            Error(JournalEntryDescriptionTooLong(raw, max))
        else
            Ok(JournalEntryDescription trimmed)

type JournalEntrySource = private JournalEntrySource of string

module JournalEntrySource =
    let value (JournalEntrySource d) = d
    let create (raw: string) : Result<JournalEntrySource, IAppError> =
        let max = 50
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntrySourceIsEmpty raw)
        elif trimmed.Length > max then
            Error(JournalEntrySourceTooLong(raw, max))
        else
            Ok(JournalEntrySource trimmed)

type EntryDate =
    private
        { entryDate: LocalDate
          fiscalPeriodId: FiscalPeriodId }

module EntryDate =
    let entryDate (e: EntryDate) : LocalDate = e.entryDate
    let fiscalPeriodId (e: EntryDate) : FiscalPeriodId = e.fiscalPeriodId
    let create (context: Context.Context) (entryDate: LocalDate) : Result<EntryDate, IAppError> =
        result {
            let! id =
                entryDate
                |> FiscalPeriodKey.ofDate
                |> FiscalPeriodKey.value
                |> FiscalPeriod.fetchIdByKey context
                // only a missing period means the date is outside every period; anything else passes through
                |> Result.mapError(fun e ->
                    match e with
                    | AsError (FiscalPeriodNoPeriodMatchingKey _) -> JournalEntryDateNotInFiscalPeriod entryDate :> IAppError
                    | other -> other)
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

type JournalEntryLineType =
    | Debit
    | Credit

module JournalEntryLineType =
    let fromString (s: string) : Result<JournalEntryLineType, IAppError> =
        match s.Trim() with
        | "Debit" -> Ok Debit
        | "Credit" -> Ok Credit
        | _ -> Error(JournalEntryLineTypeInvalid s)

    let toString s =
        match s with
        | Debit -> "Debit"
        | Credit -> "Credit"

/// BalancedLines is the one balanced double-entry rule, shared by journal entries and staged entries so ledger and
/// staging can't disagree: at least two lines, and debits sum to credits. Each caller names its own typed errors.
module BalancedLines =
    let confirm
        (insufficientLines: int -> IAppError)
        (debitCreditMismatch: decimal * decimal -> IAppError)
        (lines: (JournalEntryLineType * Money.Money) list)
        : Result<unit, IAppError> =
        let sumOf lineType = lines |> List.filter (fun (t, _) -> t = lineType) |> List.map snd |> Money.sumList
        result {
            do! if lines |> List.length < 2 then Error(insufficientLines (lines |> List.length)) else Ok()
            let! totalDebits = sumOf JournalEntryLineType.Debit
            let! totalCredits = sumOf JournalEntryLineType.Credit
            return!
                if Money.isEqual totalCredits totalDebits then Ok()
                else
                    Error(debitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
        }

type JournalEntryLineMemo = private LineMemo of string

module JournalEntryLineMemo =
    let value (LineMemo d) = d
    let create (raw: string) : Result<JournalEntryLineMemo, IAppError> =
        let max = 1000
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryLineMemoIsEmpty raw)
        elif trimmed.Length > max then
            Error(JournalEntryLineMemoTooLong(raw, max))
        else
            Ok(LineMemo trimmed)
type CommentText = private CommentText of string
module CommentText =
    let max = 2000
    let value (CommentText d) = d
    let create (raw: string) : Result<CommentText, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(JournalEntryCommentIsEmpty raw)
        elif trimmed.Length > max then
            Error(JournalEntryCommentTooLong(raw, max))
        else
            Ok(CommentText trimmed)
