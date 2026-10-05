module Business.CrossDomainOrchestration.PositionsLedgerLinks

open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.AccountComponent

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
