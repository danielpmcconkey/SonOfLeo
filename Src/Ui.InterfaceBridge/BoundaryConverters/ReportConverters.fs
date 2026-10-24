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

/// A pre-posting line with the names its returns show for its account, rule and agreement.
type PrePostingNamedLine =
    { line: PrePostingLine
      accountCode: AccountCode
      accountName: AccountName
      ruleName: ClassificationRuleName option
      agreementNames: (AgreementName * PaymentAgreementName) option }

/// An entry with its source's name and its named lines.
type PrePostingNamedEntry =
    { entry: PrePostingEntry
      sourceName: JournalRefFinancialInstitution
      lines: PrePostingNamedLine list }

let private ``name [PrePostingLine]`` (context: Context.Context) (line: PrePostingLine) : Result<PrePostingNamedLine, IAppError> =
    result {
        let! accountCode = line.accountId |> Account.idToCode context
        let! accountName = line.accountId |> Account.idToName context
        let! ruleName =
            line.classificationRuleId
            |> convertOptionToDesiredTypeWithFallibleConverter (
                Business.FinancialServices.Classification.ClassificationRule.idToName context)
        let! agreementNames =
            line.agreement
            |> convertOptionToDesiredTypeWithFallibleConverter (fun a ->
                result {
                    let! masterAgreementName =
                        a.masterAgreementId |> Business.FinancialServices.CashFlow.MasterAgreement.idToName context
                    let! paymentAgreementName =
                        a.paymentAgreementId |> Business.FinancialServices.CashFlow.PaymentAgreement.idToName context
                    return masterAgreementName, paymentAgreementName
                })
        return
            { line = line
              accountCode = accountCode
              accountName = accountName
              ruleName = ruleName
              agreementNames = agreementNames }
    }

/// Resolves an entry's source name and each line's account, rule and agreement names; the converter and the writer share it.
let ``name [PrePostingEntry]`` (context: Context.Context) (entry: PrePostingEntry) : Result<PrePostingNamedEntry, IAppError> =
    result {
        let! sourceName =
            entry.ingestionSourceId |> Business.FinancialServices.DataIngestion.IngestionSource.idToName context
        let! lines = entry.lines |> List.map (``name [PrePostingLine]`` context) |> convertListOfResultsToResultsList
        return { entry = entry; sourceName = sourceName; lines = lines }
    }

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
    let convertLine (named: PrePostingNamedLine) : PrePostingLineReturnRow =
        let l = named.line
        { stageEntryLineId = l.stageEntryLineId |> StageEntryLineId.value
          lineType = l.lineType |> JournalEntryLineType.toString
          amount = l.amount |> Money.amount
          memo = l.memo |> Option.map JournalEntryLineMemo.value
          accountCode = named.accountCode |> AccountCode.value
          accountName = named.accountName |> AccountName.value
          ruleName = named.ruleName |> Option.map ClassificationRuleName.value
          paymentAgreementName = named.agreementNames |> Option.map (snd >> PaymentAgreementName.value)
          masterAgreementName = named.agreementNames |> Option.map (fst >> AgreementName.value)
          payments = l.payments |> List.map convertPayment }
    entries
    |> List.map (fun e ->
        e
        |> ``name [PrePostingEntry]`` context
        |> Result.map (fun named ->
            { stageEntryHeaderId = e.stageEntryHeaderId |> StageEntryHeaderId.value
              entryDate = e.entryDate
              description = e.description |> JournalEntryDescription.value
              sourceName = named.sourceName |> JournalRefFinancialInstitution.value
              fiReference = e.fiReference |> JournalExternalReferenceText.value
              status = e.status |> StagedEntryStatus.toString
              lines = named.lines |> List.map convertLine }))
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

let ``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]``
    (context: Context.Context)
    (row: LedgerAccountBalance)
    : Result<NetWorthLedgerAccountReturnRow, IAppError> =
    result {
        let! code = row.accountId |> ``convert AccountId to AccountCodeString`` context
        let! name = row.accountId |> ``convert AccountId to AccountNameString`` context
        return { code = code; name = name; balance = row.balance |> Money.amount }
    }

let private ``convert [LedgerAccountBalance list] to [NetWorthLedgerAccountReturnRow list]`` context rows =
    rows |> List.map (``convert [LedgerAccountBalance] to [NetWorthLedgerAccountReturnRow]`` context) |> convertListOfResultsToResultsList

/// A net worth investment account's name and its owners' names.
let netWorthInvestmentAccountNames
    (context: Context.Context)
    (account: NetWorthInvestmentAccount)
    : Result<string * string list, IAppError> =
    result {
        let! name = account.investmentAccountId |> Business.FinancialServices.Positions.InvestmentAccount.idToName context
        let! owners = account.ownerIds |> PositionsFieldConverters.ownerNamesOf context
        return name |> InvestmentAccountName.value, owners
    }

/// A net worth property's name and its owners' names.
let netWorthPropertyNames (context: Context.Context) (property: NetWorthProperty) : Result<string * string list, IAppError> =
    result {
        let! name = property.propertyId |> Business.FinancialServices.Positions.Property.idToName context
        let! owners = property.ownerIds |> PositionsFieldConverters.ownerNamesOf context
        return name |> PropertyName.value, owners
    }

/// A net worth Balance Account's name and its owners' names.
let netWorthBalanceAccountNames
    (context: Context.Context)
    (balanceAccount: NetWorthBalanceAccount)
    : Result<string * string list, IAppError> =
    result {
        let! name = balanceAccount.balanceAccountId |> Business.FinancialServices.Positions.BalanceAccount.idToName context
        let! owners = balanceAccount.ownerIds |> PositionsFieldConverters.ownerNamesOf context
        return name |> BalanceAccountName.value, owners
    }

let ``convert [NetWorthBalanceAccount] to [NetWorthBalanceAccountReturnRow]``
    (context: Context.Context)
    (balanceAccount: NetWorthBalanceAccount)
    : Result<NetWorthBalanceAccountReturnRow, IAppError> =
    netWorthBalanceAccountNames context balanceAccount
    |> Result.map (fun (name, owners) ->
        { balanceAccountName = name
          kind = balanceAccount.kind |> BalanceAccountKind.toString
          owners = owners
          balance = balanceAccount.balance |> Money.amount
          balanceDate = balanceAccount.balanceDate })

let ``convert [NetWorth] to [NetWorthReturnRow]`` (context: Context.Context) (netWorth: NetWorth) : Result<NetWorthReturnRow, IAppError> =
    let groupTotal (group, total) : NetWorthGroupTotalReturnRow = { group = group; marketValue = total |> Money.amount }
    result {
        let! investmentAccounts =
            netWorth.investmentAccounts
            |> List.map (fun a ->
                netWorthInvestmentAccountNames context a
                |> Result.map (fun (accountName, owners) ->
                    { accountName = accountName
                      owners = owners
                      accountGroup = a.accountGroup |> AccountGroup.value
                      taxTreatment = a.taxTreatment |> TaxTreatment.toString
                      snapshotDate = a.snapshotDate
                      provenance = a.provenance |> Provenance.toString
                      marketValue = a.marketValue |> Money.amount
                      contributionBasis = a.contributionBasis |> Option.map Money.amount }))
            |> convertListOfResultsToResultsList
        let! properties =
            netWorth.properties
            |> List.map (fun p ->
                result {
                    let! propertyName, owners = netWorthPropertyNames context p
                    let! mortgageAccounts =
                        p.mortgageAccounts |> ``convert [LedgerAccountBalance list] to [NetWorthLedgerAccountReturnRow list]`` context
                    let! mortgageBalanceAccounts =
                        p.mortgageBalanceAccounts
                        |> List.map (fun b ->
                            b.balanceAccountId
                            |> Business.FinancialServices.Positions.BalanceAccount.idToName context
                            |> Result.map (fun name ->
                                ({ balanceAccountName = name |> BalanceAccountName.value; balance = b.balance |> Money.amount }
                                : NetWorthMortgageBalanceAccountReturnRow)))
                        |> convertListOfResultsToResultsList
                    return
                        { propertyName = propertyName
                          propertyUse = p.propertyUse |> PropertyUse.toString
                          owners = owners
                          value = p.value |> Money.amount
                          valuationDate =
                            match p.valueSource with
                            | ValuationDated d -> Some d
                            | PurchaseBasisValue -> None
                          valueIsPurchaseBasis = (p.valueSource = PurchaseBasisValue)
                          mortgageAccounts = mortgageAccounts
                          mortgageBalanceAccounts = mortgageBalanceAccounts
                          equity = p.equity |> Money.amount }
                })
            |> convertListOfResultsToResultsList
        let! assetAccounts =
            netWorth.assetAccounts |> ``convert [LedgerAccountBalance list] to [NetWorthLedgerAccountReturnRow list]`` context
        let! liabilityAccounts =
            netWorth.liabilityAccounts |> ``convert [LedgerAccountBalance list] to [NetWorthLedgerAccountReturnRow list]`` context
        let! balanceAccounts =
            netWorth.balanceAccounts
            |> List.map (``convert [NetWorthBalanceAccount] to [NetWorthBalanceAccountReturnRow]`` context)
            |> convertListOfResultsToResultsList
        return
            { asOf = netWorth.asOf
              assetAccounts = assetAccounts
              liabilityAccounts = liabilityAccounts
              investmentAccounts = investmentAccounts
              properties = properties
              balanceAccounts = balanceAccounts
              totalLedgerAssets = netWorth.totalLedgerAssets |> Money.amount
              totalInvestments = netWorth.totalInvestments |> Money.amount
              totalPropertyValues = netWorth.totalPropertyValues |> Money.amount
              totalCash = netWorth.totalCash |> Money.amount
              totalLiabilities = netWorth.totalLiabilities |> Money.amount
              totalDebts = netWorth.totalDebts |> Money.amount
              totalOwnedPropertyMortgages = netWorth.totalOwnedPropertyMortgages |> Money.amount
              netWorth = netWorth.netWorth |> Money.amount
              investableWealth = netWorth.investableWealth |> Money.amount
              investmentsByTaxTreatment =
                netWorth.investmentsByTaxTreatment |> List.map (fun (t, total) -> groupTotal (t |> TaxTreatment.toString, total))
              investmentsByAccountGroup =
                netWorth.investmentsByAccountGroup |> List.map (fun (g, total) -> groupTotal (g |> AccountGroup.value, total))
              absentComponents = netWorth.absentComponents |> List.map NetWorthComponent.toString }
    }

let ``convert [WealthGroup] to [WealthGroupContract]``
    (context: Context.Context)
    (group: WealthGroup)
    : Result<WealthGroupContract, IAppError> =
    match group with
    | GroupInvestmentAccount id ->
        id
        |> Business.FinancialServices.Positions.InvestmentAccount.idToName context
        |> Result.map (InvestmentAccountName.value >> WealthGroupContract.Named)
    | GroupAccountGroup accountGroup -> Ok(WealthGroupContract.Named(accountGroup |> AccountGroup.value))
    | GroupTaxTreatment taxTreatment -> Ok(WealthGroupContract.Named(taxTreatment |> TaxTreatment.toString))
    | GroupDimensionValue id ->
        id
        |> Business.FinancialServices.Positions.DimensionValue.idToName context
        |> Result.map (DimensionValueName.value >> WealthGroupContract.Named)
    | GroupOwners ownerIds -> ownerIds |> PositionsFieldConverters.ownerNamesOf context |> Result.map WealthGroupContract.Owners
    | WealthGroup.Unassigned -> Ok WealthGroupContract.Unassigned

/// The order groups are shown in: named groups by name, owner groups by their owners' names, unassigned last.
let wealthGroupOrder (group: WealthGroupContract) =
    match group with
    | WealthGroupContract.Named name -> 0, [ name ]
    | WealthGroupContract.Owners owners -> 1, owners
    | WealthGroupContract.Unassigned -> 2, []

let ``convert [WealthPoint] to [WealthPointReturnRow]``
    (context: Context.Context)
    (point: WealthPoint)
    : Result<WealthPointReturnRow, IAppError> =
    point.totals
    |> List.map (fun (group, total) ->
        group
        |> ``convert [WealthGroup] to [WealthGroupContract]`` context
        |> Result.map (fun resolved -> { group = resolved; marketValue = total |> Money.amount }))
    |> convertListOfResultsToResultsList
    |> Result.map (fun totals ->
        { monthEnd = point.monthEnd
          totals = totals |> List.sortBy (fun t -> t.group |> wealthGroupOrder)
          total = point.total |> Money.amount })

let ``convert [NetWorthPoint] to [NetWorthPointReturnRow]`` (point: NetWorthPoint) : NetWorthPointReturnRow =
    { monthEnd = point.monthEnd
      totalLedgerAssets = point.totalLedgerAssets |> Money.amount
      totalInvestments = point.totalInvestments |> Money.amount
      totalPropertyValues = point.totalPropertyValues |> Money.amount
      totalCash = point.totalCash |> Money.amount
      totalLiabilities = point.totalLiabilities |> Money.amount
      totalDebts = point.totalDebts |> Money.amount
      totalOwnedPropertyMortgages = point.totalOwnedPropertyMortgages |> Money.amount
      netWorth = point.netWorth |> Money.amount
      investableWealth = point.investableWealth |> Money.amount
      absentComponents = point.absentComponents |> List.map NetWorthComponent.toString }
