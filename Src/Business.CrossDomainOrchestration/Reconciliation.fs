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

/// One balance captured from an institution: what it says the account held on the as-of date, in the account's
/// normal-balance direction (a credit card balance owed is positive).
type ReconciliationRequest =
    { accountCode: AccountCode
      externalBalance: Money.Money
      asOf: LocalDate }

type ReconciliationRow =
    { accountCode: AccountCode
      accountName: AccountName
      asOf: LocalDate
      externalBalance: Money.Money
      ledgerBalance: Money.Money
      delta: Money.Money }

/// Names the first account code the input gives more than once, then the first that no account holds.
let private confirmRequestCodes
    (accounts: Account.Account list)
    (requests: ReconciliationRequest list)
    : Result<unit, IAppError> =
    let codes = requests |> List.map (fun r -> r.accountCode |> AccountCode.value)
    let repeated = codes |> List.tryFind (fun code -> codes |> List.filter ((=) code) |> List.length > 1)
    let known = accounts |> List.map (Account.code >> AccountCode.value) |> Set.ofList
    match repeated, codes |> List.tryFind (fun code -> known |> Set.contains code |> not) with
    | Some code, _ -> Error(ReconciliationAccountCodeGivenTwice code)
    | None, Some code -> Error(ReconciliationAccountCodeNotFound code)
    | None, None -> Ok()

/// Compares each request with the ledger's net balance as of the request's own date, by the trial balance rules.
/// A non-zero delta is data, not an error.
let reconcile
    (context: Context.Context)
    (requests: ReconciliationRequest list)
    : Result<ReconciliationRow list, IAppError> =
    result {
        let! accounts = Account.fetchAll context false
        do! requests |> confirmRequestCodes accounts
        let! trialBalances =
            requests
            |> List.map _.asOf
            |> List.distinct
            |> List.map (fun asOf -> fetchTrialBalanceData context asOf |> Result.map (fun rows -> asOf, rows))
            |> convertListOfResultsToResultsList
        return!
            requests
            |> List.map (fun request ->
                let trialBalanceRow =
                    trialBalances
                    |> List.find (fun (asOf, _) -> asOf = request.asOf)
                    |> snd
                    |> List.find (fun row -> row.accountCode = request.accountCode)
                // external minus ledger
                Money.subtractVal1FromVal2 trialBalanceRow.netBalance request.externalBalance
                |> Result.map (fun delta ->
                    { accountCode = request.accountCode
                      accountName = trialBalanceRow.accountName
                      asOf = request.asOf
                      externalBalance = request.externalBalance
                      ledgerBalance = trialBalanceRow.netBalance
                      delta = delta }))
            |> convertListOfResultsToResultsList
    }

/// Posts every postable staged entry, then reconciles. The caller owns the transaction and must roll it back.
let reconcileAfterPostingStagedEntries
    (context: Context.Context)
    (requests: ReconciliationRequest list)
    : Result<ReconciliationRow list, IAppError> =
    result {
        do! StageEntryOrchestration.post context
        return! requests |> reconcile context
    }
