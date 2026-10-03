module Business.CrossDomainOrchestration.PeriodActivity

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.AccountActivity
open Business.CrossDomainOrchestration.TrialBalanceReport

type PeriodActivityLine =
    { entryDate: LocalDate
      journalEntryId: JournalEntryHeaderId
      description: JournalEntryDescription
      lineType: JournalEntryLineType
      amount: Money.Money
      memo: JournalEntryLineMemo option }

type PeriodActivityAccount =
    { accountCode: AccountCode
      accountName: AccountName
      // in the account's normal-balance direction
      netTotal: Money.Money
      lines: PeriodActivityLine list }

/// The non-voided lines on accounts of one type dated in the range, each with the account it is on.
let private fetchLinesOfType context (range: FilterDateRange) (accountType: AccountType) =
    let filter =
        { accountId = None
          temporalFilter = Some (DateRange range)
          source = None
          accountType = Some accountType
          accountSubtype = None
          accountParentId = None
          journalEntryId = None
          amount = None
          description = None
          unVoidedOnly = true }
    fetchFiltered context filter None
    |> Result.map (List.choose (fun activity ->
        activity.activityDetail
        |> Option.map (fun detail ->
            activity.accountId,
            { entryDate = detail.entryDate
              journalEntryId = detail.journalEntryHeaderId
              description = detail.journalEntryDescription
              lineType = detail.lineType
              amount = detail.amount
              memo = detail.lineMemo })))

let private netTotal (accountType: AccountType) (lines: PeriodActivityLine list) : Result<Money.Money, IAppError> =
    let sumOf lineType = lines |> List.filter (fun l -> l.lineType = lineType) |> List.map _.amount |> Money.sumList
    result {
        let! debits = sumOf JournalEntryLineType.Debit
        let! credits = sumOf JournalEntryLineType.Credit
        return!
            match accountType |> AccountType.normalBalance with
            | AccountTypeNormalBalance.Debit -> Money.subtractVal1FromVal2 credits debits
            | AccountTypeNormalBalance.Credit -> Money.subtractVal1FromVal2 debits credits
    }

let fetchPeriodActivity
    (context: Context.Context)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<PeriodActivityAccount list, IAppError> =
    let range = { beginDate = beginDate; endInclusive = endDate }
    result {
        let! revenueLines = fetchLinesOfType context range Revenue
        let! expenseLines = fetchLinesOfType context range Expense
        let linesByAccount = revenueLines @ expenseLines |> List.groupBy fst |> Map.ofList
        let! accounts = Account.fetchAll context false
        return!
            accounts
            |> accountsInTrialBalanceOrder
            |> List.choose (fun account ->
                linesByAccount
                |> Map.tryFind (account |> Account.accountId)
                |> Option.map (fun accountAndLines -> account, accountAndLines |> List.map snd))
            |> List.map (fun (account, lines) ->
                let lines = lines |> List.sortBy (fun l -> l.entryDate, l.journalEntryId |> JournalEntryHeaderId.value)
                netTotal (account |> Account.accountType) lines
                |> Result.map (fun net ->
                    { accountCode = account |> Account.code
                      accountName = account |> Account.accountName
                      netTotal = net
                      lines = lines }))
            |> convertListOfResultsToResultsList
    }
