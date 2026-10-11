module Ui.InterfaceBridge.BoundaryConverters.ReportConverters

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.TrialBalanceReport
open Business.CrossDomainOrchestration.BalanceSheetIntegrity
open Business.CrossDomainOrchestration.PeriodActivity
open Business.CrossDomainOrchestration.Reconciliation
open Business.CrossDomainOrchestration.PrePostingReview
open Business.CrossDomainOrchestration.NetWorth
open Business.CrossDomainOrchestration.InvestmentWealthHistory
open Business.CrossDomainOrchestration.NetWorthHistory
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters

let ``convert [TrialBalanceRowFlattened] to [TrialBalanceReturnRow]``
    (context: Context.Context)
    (flattenedRow: TrialBalanceRowFlattened)
    : Result<TrialBalanceReturnRow, IAppError> =
    result {
        let! accountCode = flattenedRow.accountId |> ``convert AccountId to AccountCodeString`` context
        let! accountName = flattenedRow.accountId |> ``convert AccountId to AccountNameString`` context
        return {
            accountCode = accountCode
            accountName = accountName
            generation = flattenedRow.generation
            totalCredits = flattenedRow.totalCredits |> Money.amount
            totalDebits = flattenedRow.totalDebits |> Money.amount
            netBalance = flattenedRow.netBalance |> Money.amount
        }
    }

let ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
    (context: Context.Context)
    (flattenedRows: TrialBalanceRowFlattened list)
    : Result<TrialBalanceReturnRow list, IAppError> =
    flattenedRows
    |> List.map (``convert [TrialBalanceRowFlattened] to [TrialBalanceReturnRow]`` context)
    |> convertListOfResultsToResultsList

let ``convert [PrePostingEntry list] to [PrePostingEntryReturnRow list]``
    (context: Context.Context)
    (entries: PrePostingEntry list)
    : Result<PrePostingEntryReturnRow list, IAppError> =
    let convertPayment (p: PrePostingPayment) : PrePostingPaymentReturnRow =
        { paymentId = p.paymentId |> PaymentId.value
          invoiceId = p.invoiceId |> InvoiceId.value
          paymentAmount = p.paymentAmount |> PaymentAmount.value |> Money.amount
          invoiceDate = p.invoiceDate |> InvoiceDate.value
          dueDate = p.dueDate |> DueDate.value
          invoiceAmount = p.invoiceAmount |> InvoiceAmount.value |> Money.amount
          paymentState = p.paymentState |> PaymentState.toString
          instanceDate = p.instanceDate }
    let convertLine (l: PrePostingLine) : Result<PrePostingLineReturnRow, IAppError> =
        result {
            let! accountCode = l.accountId |> ``convert AccountId to AccountCodeString`` context
            let! accountName = l.accountId |> ``convert AccountId to AccountNameString`` context
            let! ruleName =
                l.classificationRuleId
                |> convertOptionToDesiredTypeWithFallibleConverter (
                    Business.FinancialServices.Classification.ClassificationRule.idToName context)
            let! paymentAgreementName =
                l.agreement
                |> convertOptionToDesiredTypeWithFallibleConverter (fun a ->
                    a.paymentAgreementId |> Business.FinancialServices.CashFlow.PaymentAgreement.idToName context)
            let! masterAgreementName =
                l.agreement
                |> convertOptionToDesiredTypeWithFallibleConverter (fun a ->
                    a.masterAgreementId |> Business.FinancialServices.CashFlow.MasterAgreement.idToName context)
            return
                { stageEntryLineId = l.stageEntryLineId |> StageEntryLineId.value
                  lineType = l.lineType |> JournalEntryLineType.toString
                  amount = l.amount |> Money.amount
                  memo = l.memo |> Option.map JournalEntryLineMemo.value
                  accountCode = accountCode
                  accountName = accountName
                  ruleName = ruleName |> Option.map ClassificationRuleName.value
                  paymentAgreementName = paymentAgreementName |> Option.map PaymentAgreementName.value
                  masterAgreementName = masterAgreementName |> Option.map AgreementName.value
                  payments = l.payments |> List.map convertPayment }
        }
    entries
    |> List.map (fun e ->
        result {
            let! sourceName = e.ingestionSourceId |> Business.FinancialServices.DataIngestion.IngestionSource.idToName context
            let! lines = e.lines |> List.map convertLine |> convertListOfResultsToResultsList
            return
                { stageEntryHeaderId = e.stageEntryHeaderId |> StageEntryHeaderId.value
                  entryDate = e.entryDate
                  description = e.description |> JournalEntryDescription.value
                  sourceName = sourceName |> JournalRefFinancialInstitution.value
                  fiReference = e.fiReference |> JournalExternalReferenceText.value
                  status = e.status |> StagedEntryStatus.toString
                  lines = lines }
        })
    |> convertListOfResultsToResultsList

/// Resolves each row's account code to its account, failing with the code of the first that resolves to none.
let ``convert [ReconciliationInput] to [(AccountId * Money * LocalDate) list]``
    (context: Context.Context)
    (input: ReconciliationInput)
    : Result<(AccountId * Money.Money * LocalDate) list, IAppError> =
    input.rows
    |> List.map (fun inputRow ->
        result {
            let! accountId = inputRow.accountCode |> fallibleConverterAccountCodeToAccountId context
            let! externalBalance = inputRow.externalBalance |> Money.fromDecimal
            return accountId, externalBalance, inputRow.asOf
        })
    |> convertListOfResultsToResultsList

let ``convert [ReconciliationRow] to [ReconciliationReturnRow]``
    (context: Context.Context)
    (row: ReconciliationRow)
    : Result<ReconciliationReturnRow, IAppError> =
    result {
        let! accountCode = row.accountId |> ``convert AccountId to AccountCodeString`` context
        let! accountName = row.accountId |> ``convert AccountId to AccountNameString`` context
        return
            { accountCode = accountCode
              accountName = accountName
              asOf = row.asOf
              externalBalance = row.externalBalance |> Money.amount
              ledgerBalance = row.ledgerBalance |> Money.amount
              delta = row.delta |> Money.amount }
    }

let ``convert [DeactivatedAccountWithBalance] to [DeactivatedAccountWithBalanceReturnRow]``
    (context: Context.Context)
    (account: DeactivatedAccountWithBalance)
    : Result<DeactivatedAccountWithBalanceReturnRow, IAppError> =
    result {
        let! accountCode = account.accountId |> ``convert AccountId to AccountCodeString`` context
        let! accountName = account.accountId |> ``convert AccountId to AccountNameString`` context
        return
            { accountCode = accountCode
              accountName = accountName
              activeEnd = account.activeEnd
              balance = account.balance |> Money.amount
              entriesAfterActiveEnd =
                account.entriesAfterActiveEnd
                |> List.map (fun je ->
                    { journalEntryId = je |> JournalEntryHeader.journalEntryHeaderId |> JournalEntryHeaderId.value
                      entryDate = je |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                      description = je |> JournalEntryHeader.description |> JournalEntryDescription.value
                      postedAt = je |> JournalEntryHeader.createdAt
                      voidedAt = je |> JournalEntryHeader.voidedAt }) }
    }

let ``convert [BalanceSheetIntegrity] to [BalanceSheetIntegrityReturnRow]``
    (context: Context.Context)
    (integrity: BalanceSheetIntegrity)
    : Result<BalanceSheetIntegrityReturnRow, IAppError> =
    result {
        let! deactivatedAccountsWithBalance =
            integrity.deactivatedAccountsWithBalance
            |> List.map (``convert [DeactivatedAccountWithBalance] to [DeactivatedAccountWithBalanceReturnRow]`` context)
            |> convertListOfResultsToResultsList
        return
            { asOf = integrity.asOf
              totalDebits = integrity.totalDebits |> Money.amount
              totalCredits = integrity.totalCredits |> Money.amount
              debitsEqualCredits = integrity.debitsEqualCredits
              assets = integrity.assets |> Money.amount
              liabilities = integrity.liabilities |> Money.amount
              equity = integrity.equity |> Money.amount
              revenue = integrity.revenue |> Money.amount
              expenses = integrity.expenses |> Money.amount
              netIncome = integrity.netIncome |> Money.amount
              residual = integrity.residual |> Money.amount
              deactivatedAccountsWithBalance = deactivatedAccountsWithBalance }
    }

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
    (context: Context.Context)
    (account: PeriodActivityAccount)
    : Result<PeriodActivityAccountReturnRow, IAppError> =
    result {
        let! accountCode = account.accountId |> ``convert AccountId to AccountCodeString`` context
        let! accountName = account.accountId |> ``convert AccountId to AccountNameString`` context
        return
            { accountCode = accountCode
              accountName = accountName
              netTotal = account.netTotal |> Money.amount
              lines = account.lines |> List.map ``convert [PeriodActivityLine] to [PeriodActivityLineReturnRow]`` }
    }

let private ``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]``
    (row: LedgerAccountBalance)
    : NetWorthLedgerAccountReturnRow =
    { code = row.code; name = row.name; balance = row.balance |> Money.amount; balanceDate = row.balanceDate }

let ``convert [NetWorth] to [NetWorthReturnRow]`` (netWorth: NetWorth) : NetWorthReturnRow =
    let groupTotal (group, total) : NetWorthGroupTotalReturnRow = { group = group; marketValue = total |> Money.amount }
    { asOf = netWorth.asOf
      isPreLedger = (netWorth.netWorthDate = PreLedgerDate)
      assetAccounts = netWorth.assetAccounts |> List.map ``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]``
      liabilityAccounts =
        netWorth.liabilityAccounts |> List.map ``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]``
      investmentAccounts =
        netWorth.investmentAccounts
        |> List.map (fun a ->
            { accountName = a.investmentAccountName
              owners = a.ownerNames
              accountGroup = a.accountGroup
              taxTreatment = a.taxTreatment |> Business.FinancialServices.Positions.PositionsComponent.TaxTreatment.toString
              snapshotDate = a.snapshotDate
              provenance = a.provenance |> Business.FinancialServices.Positions.PositionsComponent.Provenance.toString
              marketValue = a.marketValue |> Money.amount
              contributionBasis = a.contributionBasis |> Option.map Money.amount })
      properties =
        netWorth.properties
        |> List.map (fun p ->
            { propertyName = p.propertyName
              propertyUse = p.propertyUse |> Business.FinancialServices.Positions.PositionsComponent.PropertyUse.toString
              owners = p.ownerNames
              value = p.value |> Money.amount
              valuationDate =
                match p.valueSource with
                | ValuationDated d -> Some d
                | PurchaseBasisValue -> None
              valueIsPurchaseBasis = (p.valueSource = PurchaseBasisValue)
              mortgageAccounts = p.mortgageAccounts |> List.map ``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]``
              equity = p.equity |> Money.amount })
      totalLedgerAssets = netWorth.totalLedgerAssets |> Money.amount
      totalInvestments = netWorth.totalInvestments |> Money.amount
      totalPropertyValues = netWorth.totalPropertyValues |> Money.amount
      totalLiabilities = netWorth.totalLiabilities |> Money.amount
      totalOwnedPropertyMortgages = netWorth.totalOwnedPropertyMortgages |> Money.amount
      netWorth = netWorth.netWorth |> Money.amount
      investableWealth = netWorth.investableWealth |> Money.amount
      investmentsByTaxTreatment =
        netWorth.investmentsByTaxTreatment
        |> List.map (fun (t, total) ->
            groupTotal (t |> Business.FinancialServices.Positions.PositionsComponent.TaxTreatment.toString, total))
      investmentsByAccountGroup = netWorth.investmentsByAccountGroup |> List.map groupTotal
      absentComponents = netWorth.absentComponents |> List.map NetWorthComponent.toString }

let ``convert [WealthGroup] to [WealthGroupContract]`` (group: WealthGroup) : WealthGroupContract =
    match group with
    | GroupName name -> WealthGroupContract.Named name
    | GroupOwners owners -> WealthGroupContract.Owners owners
    | WealthGroup.Unassigned -> WealthGroupContract.Unassigned

let ``convert [WealthPoint] to [WealthPointReturnRow]`` (point: WealthPoint) : WealthPointReturnRow =
    { monthEnd = point.monthEnd
      totals =
        point.totals
        |> List.map (fun (group, total) ->
            { group = group |> ``convert [WealthGroup] to [WealthGroupContract]``; marketValue = total |> Money.amount })
      total = point.total |> Money.amount }

let ``convert [NetWorthPoint] to [NetWorthPointReturnRow]`` (point: NetWorthPoint) : NetWorthPointReturnRow =
    { monthEnd = point.monthEnd
      isPreLedger = (point.netWorthDate = PreLedgerDate)
      totalLedgerAssets = point.totalLedgerAssets |> Money.amount
      totalInvestments = point.totalInvestments |> Money.amount
      totalPropertyValues = point.totalPropertyValues |> Money.amount
      totalLiabilities = point.totalLiabilities |> Money.amount
      totalOwnedPropertyMortgages = point.totalOwnedPropertyMortgages |> Money.amount
      netWorth = point.netWorth |> Money.amount
      investableWealth = point.investableWealth |> Money.amount
      absentComponents = point.absentComponents |> List.map NetWorthComponent.toString }
