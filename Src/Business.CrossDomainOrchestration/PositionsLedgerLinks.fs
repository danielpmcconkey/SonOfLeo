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
