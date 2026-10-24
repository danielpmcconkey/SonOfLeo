module Business.CrossDomainOrchestration.NetWorth

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General.PersonComponent
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.HoldingsAsOf

/// A ledger account's balance on the date.
type LedgerAccountBalance = { accountId: AccountId; balance: Money.Money }

/// A Balance Account's balance on the date: its latest Balance on or before it, and that Balance's date.
type NetWorthBalanceAccount = {
    balanceAccountId: BalanceAccountId
    kind: BalanceAccountKind
    ownerIds: Set<PersonId>
    balance: Money.Money
    balanceDate: LocalDate
}

type NetWorthProperty = {
    propertyId: PropertyId
    propertyUse: PropertyUse
    ownerIds: Set<PersonId>
    value: Money.Money
    valueSource: PropertyValueSource
    mortgageAccounts: LedgerAccountBalance list
    /// Those whose balance is not zero; equity counts them all.
    mortgageBalanceAccounts: NetWorthBalanceAccount list
    equity: Money.Money
}

type NetWorth = {
    asOf: LocalDate
    assetAccounts: LedgerAccountBalance list
    liabilityAccounts: LedgerAccountBalance list
    investmentAccounts: NetWorthInvestmentAccount list
    properties: NetWorthProperty list
    /// Every Cash Balance Account and every Debt one that is not an owned Property's mortgage, whose balance is not
    /// zero; the totals count them all.
    balanceAccounts: NetWorthBalanceAccount list
    totalLedgerAssets: Money.Money
    totalInvestments: Money.Money
    totalPropertyValues: Money.Money
    totalCash: Money.Money
    totalLiabilities: Money.Money
    totalDebts: Money.Money
    totalOwnedPropertyMortgages: Money.Money
    netWorth: Money.Money
    investableWealth: Money.Money
    investmentsByTaxTreatment: (TaxTreatment * Money.Money) list
    investmentsByAccountGroup: (AccountGroup * Money.Money) list
    absentComponents: NetWorthComponent list
}

/// Whether the date is within a fiscal period. A date earlier than the start of the earliest fiscal period is accepted
/// but within none, as is every date when there is no fiscal period; any other date fails.
let isWithinFiscalPeriod (fiscalPeriods: FiscalPeriod.FiscalPeriod list) (date: LocalDate) : Result<bool, IAppError> =
    let inPeriod =
        fiscalPeriods |> List.exists (fun fp -> FiscalPeriod.startDate fp <= date && date <= FiscalPeriod.endDate fp)
    let beforeLedger =
        match fiscalPeriods |> FiscalPeriod.earliestStart with
        | Some ledgerStart -> date < ledgerStart
        | None -> true
    if inPeriod then Ok true
    elif beforeLedger then Ok false
    else error (PositionsNetWorthDateOutsideFiscalPeriods date)

// Each account's balance on the date, or None for an account with no balance that day: outside every fiscal period no
// account has one.
let private balancesOn
    (context: Context.Context)
    (asOf: LocalDate)
    (withinFiscalPeriod: bool)
    : Result<AccountId -> Money.Money option, IAppError> =
    result {
        if withinFiscalPeriod then
            let! balances = AccountBalance.fetchByAccountIdList context None (Some asOf)
            let balanceById =
                balances |> List.map (fun b -> AccountBalance.accountId b, AccountBalance.netBalance b) |> Map.ofList
            let! zero = Money.fromDecimal 0M
            return fun accountId -> Some(balanceById |> Map.tryFind accountId |> Option.defaultValue zero)
        else
            return fun _ -> None
    }

// Every Balance Account that has a balance on the date, in name order. One with no Balance on or before the date has
// none, and is left out.
let private balanceAccountsOn (context: Context.Context) (asOf: LocalDate) : Result<NetWorthBalanceAccount list, IAppError> =
    result {
        let! balanceAccounts = BalanceAccount.fetchAll context
        let! latest = BalanceAccountBalance.fetchLatestOnOrBefore context asOf
        let latestById = latest |> List.map (fun b -> BalanceAccountBalance.balanceAccountId b, b) |> Map.ofList
        return
            balanceAccounts
            |> List.sortBy (BalanceAccount.balanceAccountName >> BalanceAccountName.value)
            |> List.choose (fun balanceAccount ->
                let balanceAccountId = balanceAccount |> BalanceAccount.balanceAccountId
                latestById
                |> Map.tryFind balanceAccountId
                |> Option.map (fun b ->
                    { balanceAccountId = balanceAccountId
                      kind = balanceAccount |> BalanceAccount.kind
                      ownerIds = balanceAccount |> BalanceAccount.owners
                      balance = b |> BalanceAccountBalance.amount
                      balanceDate = b |> BalanceAccountBalance.balanceDate }))
    }

let private isNotZero (b: NetWorthBalanceAccount) = b.balance |> Money.amount <> 0M

let private sumOf (amounts: Money.Money list) = Money.sumList amounts

let private totalsBy (key: 'k -> 'g) (amount: 'k -> Money.Money) (items: 'k list) : Result<('g * Money.Money) list, IAppError> =
    items
    |> List.groupBy key
    |> List.map (fun (group, members) -> members |> List.map amount |> sumOf |> Result.map (fun total -> group, total))
    |> convertListOfResultsToResultsList
    |> Result.map (List.sortBy fst)

let computeNetWorth (context: Context.Context) (asOf: LocalDate) : Result<NetWorth, IAppError> =
    result {
        let! fiscalPeriods = FiscalPeriod.fetchAll context false
        let! withinFiscalPeriod = isWithinFiscalPeriod fiscalPeriods asOf
        let! ledgerAccounts = Account.fetchAll context false
        let! balanceOf = balancesOn context asOf withinFiscalPeriod
        // the accounts in code order, each with its balance when it has one
        let rowsOf (accounts: Account.Account list) =
            accounts
            |> List.sortBy (Account.code >> AccountCode.value)
            |> List.choose (fun account ->
                let accountId = account |> Account.accountId
                accountId |> balanceOf |> Option.map (fun balance -> { accountId = accountId; balance = balance }))
        let accountById = ledgerAccounts |> List.map (fun a -> Account.accountId a, a) |> Map.ofList

        let! investmentAccounts = InvestmentAccount.fetchAll context
        let! properties = Property.fetchAll context
        let! valuations = Valuation.fetchAll context
        let linkedAssetIds =
            (investmentAccounts |> List.choose InvestmentAccount.ledgerAccountId)
            |> Set.ofList
            |> Set.union (properties |> List.map Property.assetAccountIds |> Set.unionMany)
        let ownedProperties =
            properties
            |> List.filter (fun p -> p |> Property.ownedPeriod |> OwnedPeriod.isOwnedOn asOf)
            |> List.sortBy (Property.propertyName >> PropertyName.value)
        let ownedMortgageIds = ownedProperties |> List.map Property.mortgageAccountIds |> Set.unionMany
        let ownedMortgageBalanceAccountIds = ownedProperties |> List.map Property.mortgageBalanceAccountIds |> Set.unionMany
        let! withBalance = balanceAccountsOn context asOf
        let cashAccounts = withBalance |> List.filter (fun b -> b.kind = BalanceAccountKind.Cash)
        let debtAccounts =
            withBalance
            |> List.filter (fun b -> b.kind = BalanceAccountKind.Debt)
            |> List.filter (fun b -> not (ownedMortgageBalanceAccountIds |> Set.contains b.balanceAccountId))
        let listedBalanceAccounts =
            withBalance
            |> List.filter (fun b -> b.kind = BalanceAccountKind.Cash || debtAccounts |> List.contains b)
            |> List.filter isNotZero

        let assetAccounts =
            ledgerAccounts
            |> List.filter (fun a -> Account.accountType a = AccountType.Asset)
            |> List.filter (fun a -> not (linkedAssetIds |> Set.contains (Account.accountId a)))
            |> rowsOf
        let liabilityAccounts =
            ledgerAccounts
            |> List.filter (fun a -> Account.accountType a = AccountType.Liability)
            |> List.filter (fun a -> not (ownedMortgageIds |> Set.contains (Account.accountId a)))
            |> rowsOf

        let! holdings = fetchHoldingsAsOf context asOf
        let! investmentRows =
            holdings
            |> List.map (fun account ->
                result {
                    let! marketValue = account.lines |> List.map (fun l -> l.marketValue) |> sumOf
                    return
                        { investmentAccountId = account.investmentAccountId
                          ownerIds = account.ownerIds
                          accountGroup = account.accountGroup
                          taxTreatment = account.taxTreatment
                          snapshotDate = account.snapshotDate
                          provenance = account.provenance
                          marketValue = marketValue
                          contributionBasis = account.contributionBasis |> Option.map ContributionBasis.value }
                })
            |> convertListOfResultsToResultsList

        // each owned Property's row, its mortgages' total, and whether any mortgage of it has a balance
        let! propertyFigures =
            ownedProperties
            |> List.map (fun property ->
                result {
                    let value, source = Valuation.valueOn asOf property valuations
                    let mortgageRows =
                        property
                        |> Property.mortgageAccountIds
                        |> Set.toList
                        |> List.choose (fun id -> accountById |> Map.tryFind id)
                        |> rowsOf
                    let mortgageIds = property |> Property.mortgageBalanceAccountIds
                    let mortgageBalanceAccounts =
                        withBalance |> List.filter (fun b -> mortgageIds |> Set.contains b.balanceAccountId)
                    let! mortgageTotal =
                        (mortgageRows |> List.map (fun r -> r.balance))
                        @ (mortgageBalanceAccounts |> List.map (fun b -> b.balance))
                        |> sumOf
                    let! equity = Money.subtractVal1FromVal2 mortgageTotal value
                    return
                        { propertyId = property |> Property.propertyId
                          propertyUse = property |> Property.propertyUse
                          ownerIds = property |> Property.owners
                          value = value
                          valueSource = source
                          mortgageAccounts = mortgageRows
                          mortgageBalanceAccounts = mortgageBalanceAccounts |> List.filter isNotZero
                          equity = equity },
                    mortgageTotal,
                    not (mortgageRows |> List.isEmpty && mortgageBalanceAccounts |> List.isEmpty)
                })
            |> convertListOfResultsToResultsList
        let propertyRows = propertyFigures |> List.map (fun (row, _, _) -> row)

        let! totalLedgerAssets = assetAccounts |> List.map (fun r -> r.balance) |> sumOf
        let! totalInvestments = investmentRows |> List.map (fun r -> r.marketValue) |> sumOf
        let! totalPropertyValues = propertyRows |> List.map (fun r -> r.value) |> sumOf
        let! totalCash = cashAccounts |> List.map (fun b -> b.balance) |> sumOf
        let! totalLiabilities = liabilityAccounts |> List.map (fun r -> r.balance) |> sumOf
        let! totalDebts = debtAccounts |> List.map (fun b -> b.balance) |> sumOf
        let! totalOwnedPropertyMortgages = propertyFigures |> List.map (fun (_, total, _) -> total) |> sumOf
        let! grossAssets = sumOf [ totalLedgerAssets; totalInvestments; totalPropertyValues; totalCash ]
        let! allLiabilities = sumOf [ totalLiabilities; totalDebts; totalOwnedPropertyMortgages ]
        let! netWorth = Money.subtractVal1FromVal2 allLiabilities grossAssets
        let! investableWealth =
            match propertyRows |> List.tryFind (fun r -> r.propertyUse = PropertyUse.PrimaryResidence) with
            | Some residence -> Money.subtractVal1FromVal2 residence.equity netWorth
            | None -> Ok netWorth
        let! byTaxTreatment = investmentRows |> totalsBy (fun r -> r.taxTreatment) (fun r -> r.marketValue)
        let! byAccountGroup = investmentRows |> totalsBy (fun r -> r.accountGroup) (fun r -> r.marketValue)
        let absentComponents =
            [ CountedLedgerAssets, assetAccounts |> List.isEmpty
              Investments, investmentRows |> List.isEmpty
              PropertyValues, propertyRows |> List.isEmpty
              Cash, cashAccounts |> List.isEmpty
              Liabilities, liabilityAccounts |> List.isEmpty
              Debts, debtAccounts |> List.isEmpty
              OwnedPropertyMortgages, propertyFigures |> List.forall (fun (_, _, hasMortgage) -> not hasMortgage) ]
            |> List.filter snd
            |> List.map fst
        return
            { asOf = asOf
              assetAccounts = assetAccounts
              liabilityAccounts = liabilityAccounts
              investmentAccounts = investmentRows
              properties = propertyRows
              balanceAccounts = listedBalanceAccounts
              totalLedgerAssets = totalLedgerAssets
              totalInvestments = totalInvestments
              totalPropertyValues = totalPropertyValues
              totalCash = totalCash
              totalLiabilities = totalLiabilities
              totalDebts = totalDebts
              totalOwnedPropertyMortgages = totalOwnedPropertyMortgages
              netWorth = netWorth
              investableWealth = investableWealth
              investmentsByTaxTreatment = byTaxTreatment
              investmentsByAccountGroup = byAccountGroup
              absentComponents = absentComponents }
    }
