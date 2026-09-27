module Business.CrossDomainOrchestration.BalanceSheetIntegrity

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent

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
      residual: Money.Money }

/// REQ-RPT-5: an imbalance or a non-zero residual is data, not an error; the caller decides whether to stop.
let computeBalanceSheetIntegrity (context: Context.Context) (asOf: LocalDate) : Result<BalanceSheetIntegrity, IAppError> =
    result {
        // each account's own lines, not rolled up to its parent, so summing across accounts counts every line once
        let! balances = AccountBalance.fetchByAccountIdList context None (Some asOf)
        let! accounts = Account.fetchAll context false
        let accountTypes = accounts |> List.map (fun a -> Account.accountId a, Account.accountType a) |> Map.ofList
        let netBalanceOf accountType =
            balances
            |> List.filter (fun b -> accountTypes[b.accountId] = accountType)
            |> List.map _.netBalance
            |> Money.sumList
        let! totalDebits = balances |> List.map _.totalDebits |> Money.sumList
        let! totalCredits = balances |> List.map _.totalCredits |> Money.sumList
        let! assets = netBalanceOf Asset
        let! liabilities = netBalanceOf Liability
        let! equity = netBalanceOf Equity
        let! revenue = netBalanceOf Revenue
        let! expenses = netBalanceOf Expense
        let! netIncome = Money.subtractVal1FromVal2 expenses revenue
        let! liabilitiesAndEquity = Money.add liabilities equity
        let! claims = Money.add liabilitiesAndEquity netIncome
        let! residual = Money.subtractVal1FromVal2 claims assets
        return
            { asOf = asOf
              totalDebits = totalDebits
              totalCredits = totalCredits
              debitsEqualCredits = (totalDebits |> Money.amount) = (totalCredits |> Money.amount)
              assets = assets
              liabilities = liabilities
              equity = equity
              revenue = revenue
              expenses = expenses
              netIncome = netIncome
              residual = residual }
    }
