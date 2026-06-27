namespace Model.Ledger.Journaling

open System
open Model.Ledger.FiscalPeriods
open Utilities.DAL
open NodaTime
open Utilities.ResultCE
module JournalEntryComponent =
    
    type Description = private Description of string

    module Description =
        let value (Description d) = d 
        let create (raw: string) : Result<Description, string> =
            let trimmed = raw.Trim() // REQ-SYS-1.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Description cannot be empty"  // REQ-JE-1.4, REQ-SYS-1.2
            elif trimmed.Length > 1000 then
                Error "Description cannot exceed 1000 characters" // REQ-JE-1.5
            else
                Ok (Description trimmed)

    type Source = private Source of string
    
    module Source =
        let value (Source d) = d 
        let create (raw: string) : Result<Source, string> =
            let trimmed = raw.Trim() // REQ-SYS-1.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Source cannot be empty"  // REQ-JE-1.7, REQ-SYS-1.2
            elif trimmed.Length > 50 then
                Error "Source cannot exceed 50 characters" // REQ-JE-1.8
            else
                Ok (Source trimmed)

    type EntryDate =
      private  {    entryDate: LocalDate // REQ-JE-1.10
                    fiscalPeriod: FiscalPeriod
      }

    module EntryDate =
        let entryDate (e:EntryDate) : LocalDate = e.entryDate
        let fiscalPeriod (e:EntryDate) : FiscalPeriod = e.fiscalPeriod
        let fiscalPeriodKey (e:EntryDate): string = e.fiscalPeriod |> FiscalPeriod.periodKey |> PeriodKey.value  // here as quality of life
        let create (transaction: DbTransaction option) (entryDate: LocalDate) : Result<EntryDate, string> =
            let monthF = entryDate.Month.ToString("D2")
            result {
                let key = $"{entryDate.Year}-{monthF}" // REQ-JE-1.11
                let! fp = key |> FiscalPeriod.fetchByKey transaction
                do! if fp |> FiscalPeriod.isOpen = false then Error $"Entry date {entryDate} is not associated to an open period" else Ok ()
                return { entryDate = entryDate; fiscalPeriod = fp }
            }

    type JournalEntryLineType = // REQ-JE-1.25
        | Debit
        | Credit
    
    module JournalEntryLineType =
        let fromString (s:string) : Result<JournalEntryLineType, string> =
            match s.Trim() with
            | "Debit" -> Ok Debit
            | "Credit" -> Ok Credit
            | _ -> Error $"Invalid JournalEntryLineType of {s}"
            
        let toString s  =
            match s with
            | Debit -> "Debit"
            | Credit -> "Credit"
    
    type LineMemo = private LineMemo of string
    
    module LineMemo =
        let value (LineMemo d) = d 
        let create (raw: string) : Result<LineMemo, string> =
            let trimmed = raw.Trim() // REQ-SYS-1.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "LineMemo cannot be empty"  // REQ-JE-1.27, REQ-SYS-1.2
            elif trimmed.Length > 1000 then
                Error "LineMemo cannot exceed 1000 characters" // REQ-JE-1.28
            else
                Ok (LineMemo trimmed)