module Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

type ReportAsOf = { asOf: LocalDate }

type OutputPathInput = {
    baseDir: string
    // if true, the file is named fileName, a hyphen and the as-of date in yyyy-MM-dd format, then .html; if false,
    // fileName then .html. The writer appends .html either way, so fileName carries no extension.
    interpolateAsOf: bool
    fileName: string
}
type OutputPathReturn = { fullyQualifiedPath: string }
    
type OutputSpecifier =
    | DataOnly
    | Report of OutputPathInput
    
type TrialBalanceReportInput = { asOf: ReportAsOf; reportOutput: OutputSpecifier }

type TrialBalanceReportReturn = 
    | DataOnly of TrialBalanceReturnRow list
    | Report of OutputPathReturn
    

