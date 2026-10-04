module Business.CrossDomainOrchestration.NetWorth

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.HoldingsAsOf

type LedgerAccountBalance = {
    code: string
    name: string
    balance: Money.Money
}

type NetWorthInvestmentAccount = {
    investmentAccountName: string
    ownerNames: string list
    accountGroup: string
    taxTreatment: TaxTreatment
    snapshotDate: LocalDate
    provenance: Provenance
    marketValue: Money.Money
    contributionBasis: Money.Money option
}

/// Where a Property's value came from: the Valuation dated so, or its purchase basis when it has none on or before the
/// date.
type PropertyValueSource =
    | ValuationDated of LocalDate
    | PurchaseBasisValue

type NetWorthProperty = {
    propertyName: string
    propertyUse: PropertyUse
    ownerNames: string list
    value: Money.Money
    valueSource: PropertyValueSource
    mortgageAccounts: LedgerAccountBalance list
    equity: Money.Money
}

type NetWorth = {
    asOf: LocalDate
    assetAccounts: LedgerAccountBalance list
    liabilityAccounts: LedgerAccountBalance list
    investmentAccounts: NetWorthInvestmentAccount list
    properties: NetWorthProperty list
    totalLedgerAssets: Money.Money
    totalInvestments: Money.Money
    totalPropertyValues: Money.Money
    totalLiabilities: Money.Money
    netWorth: Money.Money
    investableWealth: Money.Money
    investmentsByTaxTreatment: (TaxTreatment * Money.Money) list
    investmentsByAccountGroup: (string * Money.Money) list
}

let private confirmInFiscalPeriod (context: Context.Context) (asOf: LocalDate) : Result<unit, IAppError> =
    result {
        let! periods = FiscalPeriod.fetchAll context false
        let covered =
            periods |> List.exists (fun fp -> FiscalPeriod.startDate fp <= asOf && asOf <= FiscalPeriod.endDate fp)
        if not covered then return! error (PositionsNetWorthDateOutsideFiscalPeriods asOf)
    }

let private sumOf (amounts: Money.Money list) = Money.sumList amounts

let private totalsBy (key: 'k -> 'g) (amount: 'k -> Money.Money) (items: 'k list) : Result<('g * Money.Money) list, IAppError> =
    items
    |> List.groupBy key
    |> List.map (fun (group, members) -> members |> List.map amount |> sumOf |> Result.map (fun total -> group, total))
    |> convertListOfResultsToResultsList
    |> Result.map (List.sortBy fst)

let computeNetWorth (context: Context.Context) (asOf: LocalDate) : Result<NetWorth, IAppError> =
    result {
        do! confirmInFiscalPeriod context asOf
        let! ledgerAccounts = Account.fetchAll context false
        let! balances = AccountBalance.fetchByAccountIdList context None (Some asOf)
        let balanceById =
            balances |> List.map (fun b -> AccountBalance.accountId b, AccountBalance.netBalance b) |> Map.ofList
        let! zero = Money.fromDecimal 0M
        let balanceOf accountId = balanceById |> Map.tryFind accountId |> Option.defaultValue zero
        let rowOf (account: Account.Account) =
            { code = account |> Account.code |> AccountCode.value
              name = account |> Account.accountName |> AccountName.value
              balance = account |> Account.accountId |> balanceOf }
        let accountById = ledgerAccounts |> List.map (fun a -> Account.accountId a, a) |> Map.ofList

        let! investmentAccounts = InvestmentAccount.fetchAll context
        let! properties = Property.fetchAll context
        let! valuations = Valuation.fetchAll context
        let linkedAssetIds =
            (investmentAccounts |> List.choose InvestmentAccount.ledgerAccountId)
            @ (properties |> List.choose Property.ledgerAssetAccountId)
            |> Set.ofList
        let ownedProperties =
            properties
            |> List.filter (fun p -> p |> Property.ownedPeriod |> OwnedPeriod.isOwnedOn asOf)
            |> List.sortBy (Property.propertyName >> PropertyName.value)
        let ownedMortgageIds = ownedProperties |> List.collect Property.mortgageAccountIds |> Set.ofList

        let assetAccounts =
            ledgerAccounts
            |> List.filter (fun a -> Account.accountType a = AccountType.Asset)
            |> List.filter (fun a -> not (linkedAssetIds |> Set.contains (Account.accountId a)))
            |> List.map rowOf
            |> List.sortBy (fun r -> r.code)
        let liabilityAccounts =
            ledgerAccounts
            |> List.filter (fun a -> Account.accountType a = AccountType.Liability)
            |> List.filter (fun a -> not (ownedMortgageIds |> Set.contains (Account.accountId a)))
            |> List.map rowOf
            |> List.sortBy (fun r -> r.code)

        let! holdings = fetchHoldingsAsOf context asOf
        let! investmentRows =
            holdings
            |> List.map (fun account ->
                account.lines
                |> List.map (fun l -> l.marketValue)
                |> sumOf
                |> Result.map (fun marketValue ->
                    { investmentAccountName = account.investmentAccountName
                      ownerNames = account.ownerNames
                      accountGroup = account.accountGroup
                      taxTreatment = account.taxTreatment
                      snapshotDate = account.snapshotDate
                      provenance = account.provenance
                      marketValue = marketValue
                      contributionBasis = account.contributionBasis }))
            |> convertListOfResultsToResultsList

        let! propertyRows =
            ownedProperties
            |> List.map (fun property ->
                result {
                    let value = Valuation.valueOn asOf property valuations
                    let source =
                        valuations
                        |> List.filter (fun v ->
                            Valuation.propertyId v = Property.propertyId property && Valuation.valuationDate v <= asOf)
                        |> List.map Valuation.valuationDate
                        |> List.sortDescending
                        |> List.tryHead
                        |> Option.map ValuationDated
                        |> Option.defaultValue PurchaseBasisValue
                    let! ownerNames = property |> Property.owners |> PersonOrchestration.personNamesOf context
                    let mortgageRows =
                        property
                        |> Property.mortgageAccountIds
                        |> List.choose (fun id -> accountById |> Map.tryFind id)
                        |> List.map rowOf
                        |> List.sortBy (fun r -> r.code)
                    let! mortgageTotal = mortgageRows |> List.map (fun r -> r.balance) |> sumOf
                    let! equity = Money.subtractVal1FromVal2 mortgageTotal value
                    return
                        { propertyName = property |> Property.propertyName |> PropertyName.value
                          propertyUse = property |> Property.propertyUse
                          ownerNames = ownerNames
                          value = value
                          valueSource = source
                          mortgageAccounts = mortgageRows
                          equity = equity }
                })
            |> convertListOfResultsToResultsList

        let! totalLedgerAssets = assetAccounts |> List.map (fun r -> r.balance) |> sumOf
        let! totalInvestments = investmentRows |> List.map (fun r -> r.marketValue) |> sumOf
        let! totalPropertyValues = propertyRows |> List.map (fun r -> r.value) |> sumOf
        let! totalLiabilities = liabilityAccounts |> List.map (fun r -> r.balance) |> sumOf
        let! totalMortgages = propertyRows |> List.collect (fun r -> r.mortgageAccounts) |> List.map (fun r -> r.balance) |> sumOf
        let! grossAssets = sumOf [ totalLedgerAssets; totalInvestments; totalPropertyValues ]
        let! allLiabilities = Money.add totalLiabilities totalMortgages
        let! netWorth = Money.subtractVal1FromVal2 allLiabilities grossAssets
        let! investableWealth =
            match propertyRows |> List.tryFind (fun r -> r.propertyUse = PropertyUse.PrimaryResidence) with
            | Some residence -> Money.subtractVal1FromVal2 residence.equity netWorth
            | None -> Ok netWorth
        let! byTaxTreatment = investmentRows |> totalsBy (fun r -> r.taxTreatment) (fun r -> r.marketValue)
        let! byAccountGroup = investmentRows |> totalsBy (fun r -> r.accountGroup) (fun r -> r.marketValue)
        return
            { asOf = asOf
              assetAccounts = assetAccounts
              liabilityAccounts = liabilityAccounts
              investmentAccounts = investmentRows
              properties = propertyRows
              totalLedgerAssets = totalLedgerAssets
              totalInvestments = totalInvestments
              totalPropertyValues = totalPropertyValues
              totalLiabilities = totalLiabilities
              netWorth = netWorth
              investableWealth = investableWealth
              investmentsByTaxTreatment = byTaxTreatment
              investmentsByAccountGroup = byAccountGroup }
    }
