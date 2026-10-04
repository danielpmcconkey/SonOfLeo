module Business.CrossDomainOrchestration.PositionsLedgerLinks

open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// The record asking for the link, so that re-saving its own link is not taken for a second one.
type LinkingRecord =
    | LinkingInvestmentAccount of InvestmentAccountId option
    | LinkingProperty of PropertyId option

let private fetchLedgerAccount (context: Context.Context) (accountId: AccountId) : Result<Account.Account, IAppError> =
    accountId |> Account.fetchById context |> whenNoRows (AccountIdDoesntMatch(accountId |> AccountId.value))

let private codeOf (account: Account.Account) = account |> Account.code |> AccountCode.value

let private describeType (account: Account.Account) =
    (account |> Account.accountType |> AccountType.toString),
    (account |> Account.accountSubType |> Option.map AccountSubtype.toString)

// An Investment Account's ledger account and a Property's asset account share one pool: a ledger account stands for at
// most one of either.
let private confirmNotLinkedElsewhere
    (context: Context.Context)
    (account: Account.Account)
    (linkingRecord: LinkingRecord)
    : Result<unit, IAppError> =
    let accountId = account |> Account.accountId
    result {
        let! investmentAccount = accountId |> InvestmentAccount.fetchByLedgerAccountId context
        let! property = accountId |> Property.fetchByLedgerAssetAccountId context
        match investmentAccount, property, linkingRecord with
        | Some linked, _, LinkingInvestmentAccount self when Some(linked |> InvestmentAccount.investmentAccountId) = self ->
            return ()
        | Some linked, _, _ ->
            let name = linked |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
            return! error (PositionsLedgerAccountAlreadyLinked(codeOf account, "Investment Account", name))
        | None, Some linked, LinkingProperty self when Some(linked |> Property.propertyId) = self -> return ()
        | None, Some linked, _ ->
            let name = linked |> Property.propertyName |> PropertyName.value
            return! error (PositionsLedgerAccountAlreadyLinked(codeOf account, "Property", name))
        | None, None, _ -> return ()
    }

let confirmInvestmentAccountLink
    (context: Context.Context)
    (self: InvestmentAccountId option)
    (accountId: AccountId)
    : Result<unit, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        do!
            match account |> Account.accountType, account |> Account.accountSubType with
            | AccountType.Asset, Some AccountSubtype.Investment -> Ok()
            | _ ->
                let accountType, subtype = describeType account
                error (PositionsInvestmentLedgerAccountNotAssetInvestment(codeOf account, accountType, subtype))
        do! confirmNotLinkedElsewhere context account (LinkingInvestmentAccount self)
    }

let confirmPropertyAssetLink
    (context: Context.Context)
    (self: PropertyId option)
    (accountId: AccountId)
    : Result<unit, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        do!
            match account |> Account.accountType, account |> Account.accountSubType with
            | AccountType.Asset, Some AccountSubtype.FixedAsset -> Ok()
            | _ ->
                let accountType, subtype = describeType account
                error (PositionsPropertyLedgerAccountNotAssetFixedAsset(codeOf account, accountType, subtype))
        do! confirmNotLinkedElsewhere context account (LinkingProperty self)
    }

let confirmMortgageLink
    (context: Context.Context)
    (self: PropertyId option)
    (accountId: AccountId)
    : Result<unit, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        do!
            match account |> Account.accountType with
            | AccountType.Liability -> Ok()
            | other -> error (PositionsMortgageAccountNotLiability(codeOf account, other |> AccountType.toString))
        let! linked = accountId |> Property.fetchByMortgageAccountId context
        match linked with
        | Some property when Some(property |> Property.propertyId) <> self ->
            let name = property |> Property.propertyName |> PropertyName.value
            return! error (PositionsMortgageAccountAlreadyLinked(codeOf account, name))
        | _ -> return ()
    }

/// A linked ledger account's code and name, for showing the link.
let ledgerAccountCodeAndName (context: Context.Context) (accountId: AccountId) : Result<string * string, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        return codeOf account, account |> Account.accountName |> AccountName.value
    }
