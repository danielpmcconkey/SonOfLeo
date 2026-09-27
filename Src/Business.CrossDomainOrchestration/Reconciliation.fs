module Business.CrossDomainOrchestration.Reconciliation

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.LedgerError
open Business.CrossDomainOrchestration.TrialBalanceReport

type ReconciliationRow =
    { accountCode: AccountCode
      accountName: AccountName
      asOf: LocalDate
      externalBalance: Money.Money
      ledgerBalance: Money.Money
      delta: Money.Money }

/// Pairs each request with its account, failing on the first account the input gives more than once.
let private resolveRequestAccounts
    (accounts: Account.Account list)
    (requests: (AccountId * Money.Money * LocalDate) list)
    : Result<(Account.Account * Money.Money * LocalDate) list, IAppError> =
    let ids = requests |> List.map (fun (accountId, _, _) -> accountId)
    let findAccount accountId =
        accounts
        |> List.tryFind (fun account -> Account.accountId account = accountId)
        |> Option.map Ok
        |> Option.defaultValue (Error(AccountIdDoesntMatch(accountId |> AccountId.value) :> IAppError))
    match ids |> List.tryFind (fun accountId -> ids |> List.filter ((=) accountId) |> List.length > 1) with
    | Some accountId ->
        findAccount accountId
        |> Result.bind (fun account ->
            Error(ReconciliationAccountCodeGivenTwice(account |> Account.code |> AccountCode.value)))
    | None ->
        requests
        |> List.map (fun (accountId, externalBalance, asOf) ->
            findAccount accountId |> Result.map (fun account -> account, externalBalance, asOf))
        |> convertListOfResultsToResultsList

/// Each request is one balance captured from an institution: the account, what the institution says it held on the
/// as-of date in the account's normal-balance direction (a credit card balance owed is positive), and that date.
/// Compares each request with the ledger's net balance as of the request's own date, by the trial balance rules.
/// A non-zero delta is data, not an error.
let reconcile
    (context: Context.Context)
    (requests: (AccountId * Money.Money * LocalDate) list)
    : Result<ReconciliationRow list, IAppError> =
    result {
        let! accounts = Account.fetchAll context false
        let! resolved = requests |> resolveRequestAccounts accounts
        let! trialBalances =
            resolved
            |> List.map (fun (_, _, asOf) -> asOf)
            |> List.distinct
            |> List.map (fun asOf -> fetchTrialBalanceData context asOf |> Result.map (fun rows -> asOf, rows))
            |> convertListOfResultsToResultsList
        return!
            resolved
            |> List.map (fun (account, externalBalance, asOf) ->
                let trialBalanceRow =
                    trialBalances
                    |> List.find (fun (tbAsOf, _) -> tbAsOf = asOf)
                    |> snd
                    |> List.find (fun row -> row.accountCode = Account.code account)
                // external minus ledger
                Money.subtractVal1FromVal2 trialBalanceRow.netBalance externalBalance
                |> Result.map (fun delta ->
                    { accountCode = trialBalanceRow.accountCode
                      accountName = trialBalanceRow.accountName
                      asOf = asOf
                      externalBalance = externalBalance
                      ledgerBalance = trialBalanceRow.netBalance
                      delta = delta }))
            |> convertListOfResultsToResultsList
    }

/// Posts every postable staged entry, then reconciles. The caller owns the transaction and must roll it back.
let reconcileAfterPostingStagedEntries
    (context: Context.Context)
    (requests: (AccountId * Money.Money * LocalDate) list)
    : Result<ReconciliationRow list, IAppError> =
    result {
        do! StageEntryOrchestration.post context
        return! requests |> reconcile context
    }
