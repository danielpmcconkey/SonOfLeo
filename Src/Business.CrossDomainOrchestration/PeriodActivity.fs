module Business.CrossDomainOrchestration.PeriodActivity

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent

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

let private mapRawForDbRead (row: RowReader) : Guid * Guid * LocalDate * string * string * decimal * string option =
    (row |> RowReader.getUuid "account_id"),
    (row |> RowReader.getUuid "journal_entry_id"),
    (row |> RowReader.getDate "entry_date"),
    (row |> RowReader.getString "description"),
    (row |> RowReader.getString "line_type"),
    (row |> RowReader.getNumeric "amount"),
    (row |> RowReader.getStringOption "memo")

let private reconstitute raw : Result<AccountId * PeriodActivityLine, IAppError> =
    let accountUuid, journalEntryUuid, entryDate, description, lineType, amount, memo = raw
    result {
        let! description = description |> JournalEntryDescription.create
        let! lineType = lineType |> JournalEntryLineType.fromString
        let! amount = amount |> Money.fromDecimal
        let! memo = memo |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        return
            accountUuid |> AccountId.fromGuid,
            { entryDate = entryDate
              journalEntryId = journalEntryUuid |> JournalEntryHeaderId.fromGuid
              description = description
              lineType = lineType
              amount = amount
              memo = memo }
    }

/// REQ-RPT-1.6's depth-first order: top-level accounts by code, each parent immediately before its children.
let private depthFirstOrder (accounts: Account.Account list) : AccountId list =
    let childrenOf parentId =
        accounts
        |> List.filter (fun a -> a |> Account.parentId = parentId)
        |> List.sortBy (fun a -> a |> Account.code |> AccountCode.value)
    let rec crawl (account: Account.Account) =
        let accountId = account |> Account.accountId
        accountId :: (childrenOf (Some accountId) |> List.collect crawl)
    childrenOf None |> List.collect crawl

let private netTotal (accountType: AccountType) (lines: PeriodActivityLine list) : Result<Money.Money, IAppError> =
    let sumOf lineType = lines |> List.filter (fun l -> l.lineType = lineType) |> List.map _.amount |> Money.sumList
    result {
        let! debits = sumOf Debit
        let! credits = sumOf Credit
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
    let queryStatement =
        $"""
        select
            a.unique_id as account_id,
            je.unique_id as journal_entry_id,
            je.entry_date,
            je.description,
            jel.line_type,
            jel.amount,
            jel.memo
        from ledger.journal_entry_line jel
        join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
        join ledger.account a on jel.account_id = a.unique_id
        where je.voided_at is null
            and je.entry_date >= @begin_date
            and je.entry_date <= @end_date
            and a.account_type in ('{Revenue |> AccountType.toString}', '{Expense |> AccountType.toString}')
        """
    let parameters =
        [ { name = "@begin_date"; value = DbLocalDate beginDate }
          { name = "@end_date"; value = DbLocalDate endDate } ]
    result {
        let! accountLines =
            executeReaderQuery
                (context |> Context.getDatabaseTransaction)
                queryStatement
                parameters
                mapRawForDbRead
                reconstitute
                AnyQuantityIsAcceptable
        let! accounts = Account.fetchAll context false
        let accountsById = accounts |> List.map (fun a -> Account.accountId a, a) |> Map.ofList
        let linesByAccount = accountLines |> List.groupBy fst |> Map.ofList
        return!
            accounts
            |> depthFirstOrder
            |> List.choose (fun accountId -> linesByAccount |> Map.tryFind accountId |> Option.map (fun ls -> accountId, ls))
            |> List.map (fun (accountId, accountAndLines) ->
                let account = accountsById[accountId]
                let lines =
                    accountAndLines
                    |> List.map snd
                    |> List.sortBy (fun l -> l.entryDate, l.journalEntryId |> JournalEntryHeaderId.value)
                netTotal (account |> Account.accountType) lines
                |> Result.map (fun net ->
                    { accountCode = account |> Account.code
                      accountName = account |> Account.accountName
                      netTotal = net
                      lines = lines }))
            |> convertListOfResultsToResultsList
    }
