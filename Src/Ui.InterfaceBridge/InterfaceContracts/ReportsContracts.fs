module Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

type ReportAsOf = { asOf: LocalDate }

type OutputPathInput = {
    baseDir: string
    // if true, the file is named fileName, a hyphen and the report's date in yyyy-MM-dd format, then .html; if false,
    // fileName then .html. The report's date is its as-of date unless its input says otherwise (period activity uses
    // its range). The writer appends .html either way, so fileName carries no extension.
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
    


type PrePostingReviewInput = {
    // with Report and interpolateAsOf, the date appended to the file name is the date the report runs
    reportOutput: OutputSpecifier
}

type PrePostingPaymentReturnRow = {
    paymentAmount: decimal
    invoiceDate: LocalDate
    dueDate: LocalDate
    invoiceAmount: decimal
    paymentState: string
    instanceDate: LocalDate
}

type PrePostingLineReturnRow = {
    stageEntryLineId: System.Guid
    lineType: string
    amount: decimal
    memo: string option
    accountCode: string
    accountName: string
    // the classification rule that assigned the account; None when the parser or the operator did
    ruleName: string option
    paymentAgreementName: string option
    masterAgreementName: string option
    payments: PrePostingPaymentReturnRow list
}

type PrePostingEntryReturnRow = {
    stageEntryHeaderId: System.Guid
    entryDate: LocalDate
    description: string
    sourceName: string
    fiReference: string
    status: string
    lines: PrePostingLineReturnRow list
}

type PrePostingReviewReturn =
    | DataOnly of PrePostingEntryReturnRow list
    | Report of OutputPathReturn
