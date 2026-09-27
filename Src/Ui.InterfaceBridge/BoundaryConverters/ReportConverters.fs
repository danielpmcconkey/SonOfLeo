module Ui.InterfaceBridge.BoundaryConverters.ReportConverters

open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.CrossDomainOrchestration.TrialBalanceReport
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration.PrePostingReview

let ``convert [TrialBalanceRowFlattened] to [TrialBalanceReturnRow]``
    (flattenedRow: TrialBalanceRowFlattened)
    : TrialBalanceReturnRow = {
        accountCode = flattenedRow.accountCode |> AccountCode.value
        accountName = flattenedRow.accountName |> AccountName.value
        generation = flattenedRow.generation
        totalCredits = flattenedRow.totalCredits |> Money.amount
        totalDebits = flattenedRow.totalDebits |> Money.amount
        netBalance = flattenedRow.netBalance |> Money.amount
    }
    
let ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
    (flattenedRows: TrialBalanceRowFlattened list)
    : TrialBalanceReturnRow list =
    flattenedRows |> List.map ``convert [TrialBalanceRowFlattened] to [TrialBalanceReturnRow]``

let ``convert [PrePostingEntry list] to [PrePostingEntryReturnRow list]``
    (entries: PrePostingEntry list)
    : PrePostingEntryReturnRow list =
    let convertPayment (p: PrePostingPayment) : PrePostingPaymentReturnRow =
        { paymentAmount = p.paymentAmount |> Money.amount
          invoiceDate = p.invoiceDate
          dueDate = p.dueDate
          invoiceAmount = p.invoiceAmount |> Money.amount
          paymentState = p.paymentState |> PaymentState.toString
          instanceDate = p.instanceDate }
    let convertLine (l: PrePostingLine) : PrePostingLineReturnRow =
        { stageEntryLineId = l.stageEntryLineId |> StageEntryLineId.value
          lineType = l.lineType |> JournalEntryLineType.toString
          amount = l.amount |> Money.amount
          memo = l.memo |> Option.map JournalEntryLineMemo.value
          accountCode = l.accountCode |> AccountCode.value
          accountName = l.accountName |> AccountName.value
          ruleName = l.ruleName |> Option.map ClassificationRuleName.value
          paymentAgreementName = l.agreement |> Option.map (fun a -> a.paymentAgreementName |> PaymentAgreementName.value)
          masterAgreementName = l.agreement |> Option.map (fun a -> a.masterAgreementName |> AgreementName.value)
          payments = l.payments |> List.map convertPayment }
    entries
    |> List.map (fun e ->
        { stageEntryHeaderId = e.stageEntryHeaderId |> StageEntryHeaderId.value
          entryDate = e.entryDate
          description = e.description |> JournalEntryDescription.value
          sourceName = e.sourceName |> JournalRefFinancialInstitution.value
          fiReference = e.fiReference |> JournalExternalReferenceText.value
          status = e.status |> StagedEntryStatus.toString
          lines = e.lines |> List.map convertLine })
