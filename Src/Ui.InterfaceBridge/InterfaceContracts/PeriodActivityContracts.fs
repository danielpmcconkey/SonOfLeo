module Ui.InterfaceBridge.InterfaceContracts.PeriodActivityContracts

open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

// begin and end are inclusive; with Report and interpolateAsOf, the file name gets -yyyy-MM-dd_yyyy-MM-dd (begin_end)
type PeriodActivityInput = { beginDate: LocalDate; endDate: LocalDate; reportOutput: OutputSpecifier }

type PeriodActivityLineReturnRow = {
    entryDate: LocalDate
    journalEntryId: System.Guid
    description: string
    lineType: string
    amount: decimal
    memo: string option
}

type PeriodActivityAccountReturnRow = {
    accountCode: string
    accountName: string
    // in the account's normal-balance direction
    netTotal: decimal
    lines: PeriodActivityLineReturnRow list
}

type PeriodActivityReturn =
    | DataOnly of PeriodActivityAccountReturnRow list
    | Report of OutputPathReturn
