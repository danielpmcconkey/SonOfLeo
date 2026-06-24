namespace Model.Ledger.Journaling

open Model.Ledger.JournalEntryComponent
open Model.Money
open Utilities.ResultCE

type JournalEntry =
  private  {    header: JournalEntryHeader
                lines: JournalEntryLine list
                externalReferences: JournalEntryExternalReference list
                comments: JournalEntryComment list }

module JournalEntry =
    let header je = je.header
    let lines je = je.lines
    let externalReferences je = je.externalReferences
    let comments je = je.comments
    
    let sumByType (lines: JournalEntryLine list) (lineType: JournalEntryLineType): Result<Money,string> =
        lines
        |> List.filter(fun x -> JournalEntryLine.lineType x = lineType)
        |> List.map(JournalEntryLine.amount) 
        |> Money.sumList 
        
    let validateAmountEquality (lines: JournalEntryLine list) : Result<JournalEntryLine list, string> =
        result {
            let! totalDebits = sumByType lines Debit
            let! totalCredits = sumByType lines Credit
            return!
                if totalCredits = totalDebits then Ok lines
                else Error "The sum of all debit line amounts must exactly equal the sum of all credit line amounts"
            }
    
    let validateLineCount (lines: JournalEntryLine list) : Result<JournalEntryLine list, string> =
        if lines |> List.length < 2
        then Error "Insufficient number of lines for a journal entry" // REQ-JE-1.12
        else Ok lines
        
    let validateLines (lines: JournalEntryLine list) : Result<JournalEntryLine list, string> =
        // todo: wire up validateLines into something
        result {
            let! _ = validateLineCount lines // REQ-JE-1.12
            let! _ = validateAmountEquality lines // REQ-JE-1.13
            return lines
        }
        

