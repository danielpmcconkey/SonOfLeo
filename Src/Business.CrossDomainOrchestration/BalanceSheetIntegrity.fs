module Business.CrossDomainOrchestration.BalanceSheetIntegrity

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent

/// A deactivated Account still holding money, with the journal entries posted or voided after its active end.
type DeactivatedAccountWithBalance =
    { code: AccountCode
      accountName: AccountName
      activeEnd: LocalDate
      balance: Money.Money
      entriesAfterActiveEnd: JournalEntryHeader.JournalEntryHeader list }

type BalanceSheetIntegrity =
    { asOf: LocalDate
      totalDebits: Money.Money
      totalCredits: Money.Money
      debitsEqualCredits: bool
      // each account type's net balance in its normal-balance direction
      assets: Money.Money
      liabilities: Money.Money
      equity: Money.Money
      revenue: Money.Money
      expenses: Money.Money
      netIncome: Money.Money
      // assets minus (liabilities plus equity plus net income)
      residual: Money.Money
      // as of the operation's date, whatever asOf says; empty when every deactivated account holds zero
      deactivatedAccountsWithBalance: DeactivatedAccountWithBalance list }

let private deactivatedAccountsWithBalance
    (context: Context.Context)
    (accounts: Account.Account list)
    : Result<DeactivatedAccountWithBalance list, IAppError> =
    let today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
    let deactivated =
        accounts
        |> List.choose (fun a ->
            a |> Account.activityPeriod |> Business.General.ActivityPeriod.activeEnd
            |> Option.filter (fun activeEnd -> activeEnd < today)
            |> Option.map (fun activeEnd -> a, activeEnd))
        |> List.sortBy (fun (a, _) -> a |> Account.code |> AccountCode.value)
    if deactivated |> List.isEmpty then Ok [] else
    result {
        let! balances =
            AccountBalance.fetchByAccountIdList context (Some (deactivated |> List.map (fst >> Account.accountId))) (Some today)
        let balanceOf accountId = balances |> List.find (fun b -> (b |> AccountBalance.accountId) = accountId) |> AccountBalance.netBalance
        let holdingMoney =
            deactivated |> List.filter (fun (a, _) -> a |> Account.accountId |> balanceOf |> Money.isZero |> not)
        let! reported =
            holdingMoney
            |> List.map (fun (account, activeEnd) ->
                result {
                    let! entries = account |> Account.accountId |> JournalEntryHeader.fetchByAccountId context
                    let after (instant: Instant) = (instant |> Calendar.dateFromInstant) > activeEnd
                    let entriesAfterActiveEnd =
                        entries
                        |> List.filter (fun je ->
                            (je |> JournalEntryHeader.createdAt |> after)
                            || (je |> JournalEntryHeader.voidedAt |> Option.exists after))
                    return
                        { code = account |> Account.code
                          accountName = account |> Account.accountName
                          activeEnd = activeEnd
                          balance = account |> Account.accountId |> balanceOf
                          entriesAfterActiveEnd = entriesAfterActiveEnd }
                })
            |> convertListOfResultsToResultsList
        return reported
    }

/// REQ-RPT-5.3: an imbalance or a non-zero residual is data, not an error; the caller decides whether to stop.
let computeBalanceSheetIntegrity (context: Context.Context) (asOf: LocalDate) : Result<BalanceSheetIntegrity, IAppError> =
    result {
        // each account's own lines, not rolled up to its parent, so summing across accounts counts every line once
        let! balances = AccountBalance.fetchByAccountIdList context None (Some asOf)
        let! accounts = Account.fetchAll context false
        let accountTypes = accounts |> List.map (fun a -> Account.accountId a, Account.accountType a) |> Map.ofList
        let netBalanceOf accountType =
            balances
            |> List.filter (fun b -> accountTypes[b |> AccountBalance.accountId] = accountType)
            |> List.map AccountBalance.netBalance
            |> Money.sumList
        let! totalDebits = balances |> List.map AccountBalance.totalDebits |> Money.sumList
        let! totalCredits = balances |> List.map AccountBalance.totalCredits |> Money.sumList
        let! assets = netBalanceOf Asset
        let! liabilities = netBalanceOf Liability
        let! equity = netBalanceOf Equity
        let! revenue = netBalanceOf Revenue
        let! expenses = netBalanceOf Expense
        let! netIncome = Money.subtractVal1FromVal2 expenses revenue
        let! liabilitiesAndEquity = Money.add liabilities equity
        let! claims = Money.add liabilitiesAndEquity netIncome
        let! residual = Money.subtractVal1FromVal2 claims assets
        let! deactivatedWithBalance = deactivatedAccountsWithBalance context accounts
        return
            { asOf = asOf
              totalDebits = totalDebits
              totalCredits = totalCredits
              debitsEqualCredits = Money.isEqual totalDebits totalCredits
              assets = assets
              liabilities = liabilities
              equity = equity
              revenue = revenue
              expenses = expenses
              netIncome = netIncome
              residual = residual
              deactivatedAccountsWithBalance = deactivatedWithBalance }
    }
