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

let fetchLedgerAccount (context: Context.Context) (accountId: AccountId) : Result<Account.Account, IAppError> =
    accountId |> Account.fetchById context |> whenNoRows (AccountIdDoesntMatch(accountId |> AccountId.value))

let codeOf (account: Account.Account) = account |> Account.code |> AccountCode.value

/// The account's type and subtype as shown in an error.
let describeType (account: Account.Account) =
    (account |> Account.accountType |> AccountType.toString),
    (account |> Account.accountSubType |> Option.map AccountSubtype.toString)

/// A linked ledger account's code and name, for showing the link.
let ledgerAccountCodeAndName (context: Context.Context) (accountId: AccountId) : Result<string * string, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        return codeOf account, account |> Account.accountName |> AccountName.value
    }

// Net worth counts what a linked account stands for from Positions and never reads its balances, so a pre-ledger balance
// on an account that becomes linked would silently count for nothing. A Property's mortgage account is not linked in
// that sense.
let confirmCarriesNoPreLedgerBalance (context: Context.Context) (account: Account.Account) : Result<unit, IAppError> =
    result {
        let! balances = account |> Account.accountId |> PreLedgerBalance.fetchByAccount context
        match balances |> PreLedgerBalance.dateRange with
        | Some(earliest, latest) -> return! error (PositionsPreLedgerBalancesBlockLink(codeOf account, earliest, latest))
        | None -> return ()
    }
