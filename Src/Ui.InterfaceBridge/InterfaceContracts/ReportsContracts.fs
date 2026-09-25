module Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

type ReportAsOf = { asOf: LocalDate }

type OutputPathInput = {
    baseDir: string
    interpolateAsOf: bool // if true, write the as-of in YYYY.MM.DD format between the fileName and file extension
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
    

