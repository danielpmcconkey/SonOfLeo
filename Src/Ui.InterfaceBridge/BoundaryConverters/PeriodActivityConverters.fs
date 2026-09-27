module Ui.InterfaceBridge.BoundaryConverters.PeriodActivityConverters

open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.PeriodActivity
open Ui.InterfaceBridge.InterfaceContracts.PeriodActivityContracts

let ``convert [PeriodActivityLine] to [PeriodActivityLineReturnRow]``
    (line: PeriodActivityLine)
    : PeriodActivityLineReturnRow =
    { entryDate = line.entryDate
      journalEntryId = line.journalEntryId |> JournalEntryHeaderId.value
      description = line.description |> JournalEntryDescription.value
      lineType = line.lineType |> JournalEntryLineType.toString
      amount = line.amount |> Money.amount
      memo = line.memo |> Option.map JournalEntryLineMemo.value }

let ``convert [PeriodActivityAccount] to [PeriodActivityAccountReturnRow]``
    (account: PeriodActivityAccount)
    : PeriodActivityAccountReturnRow =
    { accountCode = account.accountCode |> AccountCode.value
      accountName = account.accountName |> AccountName.value
      netTotal = account.netTotal |> Money.amount
      lines = account.lines |> List.map ``convert [PeriodActivityLine] to [PeriodActivityLineReturnRow]`` }
