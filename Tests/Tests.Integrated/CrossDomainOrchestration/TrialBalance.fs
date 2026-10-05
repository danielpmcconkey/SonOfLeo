namespace Tests.Integrated.CrossDomainOrchestration


open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.TrialBalanceReport
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open App.Utility
open App.Utility.Result
open Xunit
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath

/// Expected values read from the fixture's own account list, never from the report.
module TrialBalanceFixture =
    /// Every account's (code, generation), the generation counted by walking parentId up to an account with no parent.
    let expectedGenerations (accounts: Account list) : (string * int) list =
        let byId = accounts |> List.map (fun a -> a |> Account.accountId, a) |> Map.ofList
        let rec generationOf (a: Account) =
            match a |> Account.parentId with
            | None -> 0
            | Some parent -> 1 + generationOf byId.[parent]
        accounts
        |> List.map (fun a -> a |> Account.code |> AccountCode.value, generationOf a)
        |> List.sort

[<Collection("SharedTestData")>]
type TrialBalanceTests(fixture: TestDataFixture) =

    let nextMonth = Calendar.today().PlusMonths(1)
    let context = Context.create NoTransaction FetchOnly
    let prefetchedTb = fetchTrialBalanceData context nextMonth

    let unvoidedLines =
        fixture.Data.journalEntries
        |> List.filter(fun je ->
            je |> header |> JournalEntryHeader.voidedAt |> Option.isNone)
        |> List.collect jeLines

    let sumLinesForAccount accountId lineType =
        unvoidedLines
        |> List.filter(fun l ->
            l |> JournalEntryLine.accountId = accountId
            && l |> JournalEntryLine.lineType = lineType)
        |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)

    /// An account's own fixture debits and credits plus those of every account below it, summed from fixture lines.
    let rolledUpFromFixture parentId =
        let rec isDescendantOf targetParentId accountId =
            match fixture.Data.accounts |> List.tryFind(fun a -> a |> Account.accountId = accountId) with
            | None -> false
            | Some acct ->
                match acct |> Account.parentId with
                | None -> false
                | Some pid -> pid = targetParentId || isDescendantOf targetParentId pid
        let descendantIds =
            fixture.Data.accounts
            |> List.filter(fun a -> isDescendantOf parentId (a |> Account.accountId))
            |> List.map Account.accountId
        let allIds = parentId :: descendantIds
        allIds |> List.sumBy(fun id -> sumLinesForAccount id Debit), allIds |> List.sumBy(fun id -> sumLinesForAccount id Credit)

    let codeOfId accountId =
        fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = accountId) |> Account.code

    [<Fact>]
    member _.``REQ-RPT-1.2 trial balance includes inactive accounts and accounts with no journal entry activity``() =
        let expectedCount = fixture.Data.accounts |> List.length
        let expectedClosedAccountCode = fixture.Data.closedAccount |> Account.code
        let expectedNoActivityCode =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = fixture.Data.retirement3030Id)
            |> Account.code
        result {
            let! rows = prefetchedTb
            Assert.Equal(expectedCount, rows |> List.length)
            let closedRow = rows |> List.tryFind(fun r -> r.accountCode = expectedClosedAccountCode)
            Assert.True(closedRow |> Option.isSome, "Inactive account missing from trial balance")
            let noActivityRow = rows |> List.find(fun r -> r.accountCode = expectedNoActivityCode)
            Assert.Equal(0M, noActivityRow.totalDebits |> Money.amount)
            Assert.Equal(0M, noActivityRow.totalCredits |> Money.amount)
            Assert.Equal(0M, noActivityRow.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.4 leaf account row reflects only its own balance with no roll-up``() =
        let leafId = fixture.Data.food5350Id
        let leafAccount = fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = leafId)
        let leafCode = leafAccount |> Account.code
        let expectedDebits = sumLinesForAccount leafId Debit
        let expectedCredits = sumLinesForAccount leafId Credit
        let expectedNet = expectedDebits - expectedCredits
        result {
            let! rows = prefetchedTb
            let leafRow = rows |> List.find(fun r -> r.accountCode = leafCode)
            Assert.Equal(expectedDebits, leafRow.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, leafRow.totalCredits |> Money.amount)
            Assert.Equal(expectedNet, leafRow.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.5 parent account row includes its own values plus recursive child roll-up``() =
        let parentId = fixture.Data.expenses5000Id
        let parentCode = codeOfId parentId
        let expectedDebits, expectedCredits = rolledUpFromFixture parentId
        let expectedNet = expectedDebits - expectedCredits
        result {
            let! rows = prefetchedTb
            let parentRow = rows |> List.find(fun r -> r.accountCode = parentCode)
            Assert.Equal(expectedDebits, parentRow.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, parentRow.totalCredits |> Money.amount)
            Assert.Equal(expectedNet, parentRow.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.5 an entry posted to an account three levels below a parent is in the rolled-up totals of its parent, grandparent and great-grandparent``() =
        // F-5311 sits under F-5310, which sits under F-5300, which sits under F-5000
        let posted = 412.37M
        let leafId = fixture.Data.healthInsuranceMedical5311Id
        let ancestors =
            [ fixture.Data.healthInsurance5310Id; fixture.Data.personalExpenses5300Id; fixture.Data.expenses5000Id ]
        let parentOf id =
            fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = id) |> Account.parentId
        Assert.Equal<AccountId option list>(
            ancestors |> List.map Some,
            [ parentOf leafId; parentOf ancestors.[0]; parentOf ancestors.[1] ])
        let expected =
            ancestors
            |> List.map (fun id ->
                let debits, credits = rolledUpFromFixture id
                codeOfId id, debits + posted, credits)
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ =
                    EntityFunctions.createTestJournalEntryFromPrimitives
                        context "Trial balance three-level roll-up" None (Calendar.today())
                        [ (leafId, posted, "Debit", None); (fixture.Data.moneyMarket1270Id, posted, "Credit", None) ] [] []
                let! rows = fetchTrialBalanceData context nextMonth
                Assert.Equal<(AccountCode * decimal * decimal) list>(
                    expected,
                    ancestors
                    |> List.map (fun id ->
                        let row = rows |> List.find(fun r -> r.accountCode = codeOfId id)
                        row.accountCode, row.totalDebits |> Money.amount, row.totalCredits |> Money.amount))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.6 each parent row is immediately followed by its children in code order``() =
        let codeOf (a: Account) = a |> Account.code |> AccountCode.value
        let childrenOf (parent: Account option) =
            fixture.Data.accounts
            |> List.filter(fun a -> (a |> Account.parentId) = (parent |> Option.map Account.accountId))
            |> List.sortBy codeOf
        let rec walk (account: Account) : string list =
            codeOf account :: (childrenOf (Some account) |> List.collect walk)
        let expectedCodes = childrenOf None |> List.collect walk
        result {
            let! rows = prefetchedTb
            let actualCodes = rows |> List.map(fun r -> r.accountCode |> AccountCode.value)
            Assert.NotEmpty(expectedCodes)
            Assert.Equal<string list>(expectedCodes, actualCodes)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.7 every account's generation is 0 with no parent and one more than its parent's otherwise``() =
        let expected = fixture.Data.accounts |> TrialBalanceFixture.expectedGenerations
        // the fixture nests at least three levels below a top-level account, so the walk is exercised
        Assert.True(expected |> List.exists (fun (_, g) -> g >= 3), "the fixture has no account three levels deep")
        result {
            let! rows = prefetchedTb
            Assert.Equal<(string * int) list>(
                expected,
                rows |> List.map (fun r -> r.accountCode |> AccountCode.value, r.generation) |> List.sort)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.9 entries dated after the as-of date are excluded``() =
        let today = Calendar.today()
        let asOfDate = today.PlusDays(-2)
        let expenseId = fixture.Data.temporalExpense5700Id
        let expenseCode =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = expenseId)
            |> Account.code
        let linesBeforeCutoff =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let h = je |> header
                h |> JournalEntryHeader.voidedAt |> Option.isNone
                && h |> JournalEntryHeader.entryDate |> EntryDate.entryDate <= asOfDate)
            |> List.collect jeLines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = expenseId)
        let expectedDebits =
            linesBeforeCutoff
            |> List.filter(fun l -> l |> JournalEntryLine.lineType = Debit)
            |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
        let expectedCredits =
            linesBeforeCutoff
            |> List.filter(fun l -> l |> JournalEntryLine.lineType = Credit)
            |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
        Assert.True(linesBeforeCutoff |> List.length > 0, "No lines before cutoff — test is vacuous")
        let cutoffContext = Context.create NoTransaction FetchOnly
        result {
            let! rows = fetchTrialBalanceData cutoffContext asOfDate
            let row = rows |> List.find(fun r -> r.accountCode = expenseCode)
            Assert.Equal(expectedDebits, row.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, row.totalCredits |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.11 an account whose only lines are dated after the as-of date appears with zero credits debits and net``() =
        // F-5700 Temporal Expense has lines, every one dated after the day before its earliest unvoided line
        let accountId = fixture.Data.temporalExpense5700Id
        let accountCode = codeOfId accountId
        let entriesOnAccount =
            fixture.Data.journalEntries
            |> List.filter(fun je -> je |> jeLines |> List.exists(fun l -> l |> JournalEntryLine.accountId = accountId))
        let isVoided je = je |> header |> JournalEntryHeader.voidedAt |> Option.isSome
        let entryDateOf je = je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
        let asOf =
            (entriesOnAccount |> List.filter (isVoided >> not) |> List.map entryDateOf |> List.min).PlusDays(-1)
        Assert.NotEmpty(entriesOnAccount)
        Assert.All(entriesOnAccount, fun je -> Assert.True(isVoided je || entryDateOf je > asOf))
        result {
            let! rows = fetchTrialBalanceData context asOf
            let row = rows |> List.find(fun r -> r.accountCode = accountCode)
            Assert.Equal(0M, row.totalDebits |> Money.amount)
            Assert.Equal(0M, row.totalCredits |> Money.amount)
            Assert.Equal(0M, row.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper
