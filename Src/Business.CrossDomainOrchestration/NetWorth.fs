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

/// A ledger account's balance on the date. On a pre-ledger date, balanceDate is the date of the Pre-ledger Balance it
/// came from; on a date in a fiscal period it is None.
type LedgerAccountBalance = {
    code: string
    name: string
    balance: Money.Money
    balanceDate: LocalDate option
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

type NetWorthProperty = {
    propertyName: string
    propertyUse: PropertyUse
    ownerNames: string list
    value: Money.Money
    valueSource: PropertyValueSource
    mortgageAccounts: LedgerAccountBalance list
    equity: Money.Money
}

/// The five parts of net worth that may have nothing contributing to them on a date.
type NetWorthComponent =
    | CountedLedgerAssets
    | Investments
    | PropertyValues
    | Liabilities
    | OwnedPropertyMortgages

module NetWorthComponent =
    let toString netWorthComponent =
        match netWorthComponent with
        | CountedLedgerAssets -> "CountedLedgerAssets"
        | Investments -> "Investments"
        | PropertyValues -> "PropertyValues"
        | Liabilities -> "Liabilities"
        | OwnedPropertyMortgages -> "OwnedPropertyMortgages"

/// Where a date's account balances come from: the ledger, for a date in a fiscal period, or Pre-ledger Balances, for a
/// date before the first fiscal period. A date never draws on both.
type NetWorthDate =
    | FiscalPeriodDate
    | PreLedgerDate

type NetWorth = {
    asOf: LocalDate
    isPreLedger: bool
    assetAccounts: LedgerAccountBalance list
    liabilityAccounts: LedgerAccountBalance list
    investmentAccounts: NetWorthInvestmentAccount list
    properties: NetWorthProperty list
    totalLedgerAssets: Money.Money
    totalInvestments: Money.Money
    totalPropertyValues: Money.Money
    totalLiabilities: Money.Money
    totalOwnedPropertyMortgages: Money.Money
    netWorth: Money.Money
    investableWealth: Money.Money
    investmentsByTaxTreatment: (TaxTreatment * Money.Money) list
    investmentsByAccountGroup: (string * Money.Money) list
    absentComponents: NetWorthComponent list
}

/// Whether the date is in a fiscal period or a pre-ledger date: earlier than the start of the earliest fiscal period.
/// When there is no fiscal period, no date is either.
let classifyDate (fiscalPeriods: FiscalPeriod.FiscalPeriod list) (date: LocalDate) : Result<NetWorthDate, IAppError> =
    let inPeriod =
        fiscalPeriods |> List.exists (fun fp -> FiscalPeriod.startDate fp <= date && date <= FiscalPeriod.endDate fp)
    let beforeLedger =
        not (fiscalPeriods |> List.isEmpty) && date < (fiscalPeriods |> List.map FiscalPeriod.startDate |> List.min)
    if inPeriod then Ok FiscalPeriodDate
    elif beforeLedger then Ok PreLedgerDate
    else error (PositionsNetWorthDateOutsideFiscalPeriods date)

// Each account's balance on the date and the date it came from, or None for an account with no balance that day.
let private balancesOn
    (context: Context.Context)
    (asOf: LocalDate)
    (netWorthDate: NetWorthDate)
    : Result<AccountId -> (Money.Money * LocalDate option) option, IAppError> =
    result {
        match netWorthDate with
        | FiscalPeriodDate ->
            let! balances = AccountBalance.fetchByAccountIdList context None (Some asOf)
            let balanceById =
                balances |> List.map (fun b -> AccountBalance.accountId b, AccountBalance.netBalance b) |> Map.ofList
            let! zero = Money.fromDecimal 0M
            return fun accountId -> Some(balanceById |> Map.tryFind accountId |> Option.defaultValue zero, None)
        | PreLedgerDate ->
            let! balances = PreLedgerBalance.fetchOnOrBefore context asOf
            let latest = balances |> PreLedgerBalance.latestOnOrBefore asOf
            return fun accountId ->
                latest
                |> Map.tryFind accountId
                |> Option.map (fun b -> PreLedgerBalance.balance b, Some(PreLedgerBalance.balanceDate b))
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
        let! fiscalPeriods = FiscalPeriod.fetchAll context false
        let! netWorthDate = classifyDate fiscalPeriods asOf
        let! ledgerAccounts = Account.fetchAll context false
        let! balanceOf = balancesOn context asOf netWorthDate
        let rowOf (account: Account.Account) =
            account
            |> Account.accountId
            |> balanceOf
            |> Option.map (fun (balance, balanceDate) ->
                { code = account |> Account.code |> AccountCode.value
                  name = account |> Account.accountName |> AccountName.value
                  balance = balance
                  balanceDate = balanceDate })
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

        let assetAccounts =
            ledgerAccounts
            |> List.filter (fun a -> Account.accountType a = AccountType.Asset)
            |> List.filter (fun a -> not (linkedAssetIds |> Set.contains (Account.accountId a)))
            |> List.choose rowOf
            |> List.sortBy (fun r -> r.code)
        let liabilityAccounts =
            ledgerAccounts
            |> List.filter (fun a -> Account.accountType a = AccountType.Liability)
            |> List.filter (fun a -> not (ownedMortgageIds |> Set.contains (Account.accountId a)))
            |> List.choose rowOf
            |> List.sortBy (fun r -> r.code)

        let! holdings = fetchHoldingValuesAsOf context asOf
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
                    let value, source = Valuation.valueOn asOf property valuations
                    let! ownerNames = property |> Property.owners |> PersonOrchestration.personNamesOf context
                    let mortgageRows =
                        property
                        |> Property.mortgageAccountIds
                        |> Set.toList
                        |> List.choose (fun id -> accountById |> Map.tryFind id)
                        |> List.choose rowOf
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
        let! totalOwnedPropertyMortgages =
            propertyRows |> List.collect (fun r -> r.mortgageAccounts) |> List.map (fun r -> r.balance) |> sumOf
        let! grossAssets = sumOf [ totalLedgerAssets; totalInvestments; totalPropertyValues ]
        let! allLiabilities = Money.add totalLiabilities totalOwnedPropertyMortgages
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
              Liabilities, liabilityAccounts |> List.isEmpty
              OwnedPropertyMortgages, propertyRows |> List.forall (fun r -> r.mortgageAccounts |> List.isEmpty) ]
            |> List.filter snd
            |> List.map fst
        return
            { asOf = asOf
              isPreLedger = (netWorthDate = PreLedgerDate)
              assetAccounts = assetAccounts
              liabilityAccounts = liabilityAccounts
              investmentAccounts = investmentRows
              properties = propertyRows
              totalLedgerAssets = totalLedgerAssets
              totalInvestments = totalInvestments
              totalPropertyValues = totalPropertyValues
              totalLiabilities = totalLiabilities
              totalOwnedPropertyMortgages = totalOwnedPropertyMortgages
              netWorth = netWorth
              investableWealth = investableWealth
              investmentsByTaxTreatment = byTaxTreatment
              investmentsByAccountGroup = byAccountGroup
              absentComponents = absentComponents }
    }
