namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Operation.CoreAuditableAction
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryVoiding
open Business.CrossDomainOrchestration.BalanceSheetIntegrity
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Xunit


(* Every test runs inside its own rolled-back transaction, on one account of each type that exists only there, and
   reads integrity before and after writing its own journal entries. The shared fixture's lines are in both readings,
   so each expectation is about the difference between them and is a sum of amounts written in this file.

     BI-1000 Asset    BI-2000 Liability    BI-3000 Equity    BI-4000 Revenue    BI-5000 Expense

   The unbalanced cases cannot be built through the domain, which refuses an unbalanced entry, so they post a
   balanced entry and then raise one line's amount in SQL. *)
type IntegrityAccounts =
    { asset: AccountId
      liability: AccountId
      equity: AccountId
      revenue: AccountId
      expense: AccountId }

/// The change in every integrity figure between two readings, as decimals.
type IntegrityDelta =
    { debits: decimal
      credits: decimal
      assets: decimal
      liabilities: decimal
      equity: decimal
      revenue: decimal
      expenses: decimal
      netIncome: decimal
      residual: decimal }

[<Collection("SharedTestData")>]
type BalanceSheetIntegrityTests(fixture: TestDataFixture) =

    let today = Calendar.today()
    let yesterday = today.PlusDays(-1)

    let voidReason =
        "Voided so integrity can ignore it"
        |> CommentText.create
        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))

    let createAccounts (context: Context.Context) : Result<IntegrityAccounts, IAppError> =
        let create code name accountType subtype =
            createTestAccountFromPrimitives context code name accountType (today.PlusYears(-1)) None subtype None None
            |> Result.map snd
        result {
            let! asset = create "BI-1000" "Integrity asset" "Asset" (Some "Cash")
            let! liability = create "BI-2000" "Integrity liability" "Liability" (Some "CurrentLiability")
            let! equity = create "BI-3000" "Integrity equity" "Equity" None
            let! revenue = create "BI-4000" "Integrity revenue" "Revenue" (Some "OperatingRevenue")
            let! expense = create "BI-5000" "Integrity expense" "Expense" (Some "OperatingExpense")
            return { asset = asset; liability = liability; equity = equity; revenue = revenue; expense = expense }
        }

    let post context description entryDate lines =
        createTestJournalEntryFromPrimitives context description None entryDate lines [] [] |> Result.map snd

    /// Raises the amount of the entry's line on the given account by the given amount, leaving the entry unbalanced.
    let raiseLine (context: Context.Context) (entryId: JournalEntryHeaderId) (accountId: AccountId) (raiseBy: decimal) =
        executeNonQuery
            (context |> Context.getDatabaseTransaction)
            "update ledger.journal_entry_line set amount = amount + @raise_by
             where journal_entry_id = @journal_entry_id and account_id = @account_id"
            [ { name = "@raise_by"; value = Numeric raiseBy }
              { name = "@journal_entry_id"; value = UniqueId(entryId |> JournalEntryHeaderId.value) }
              { name = "@account_id"; value = UniqueId(accountId |> AccountId.value) } ]
            ExactlyOne

    let delta (before: BalanceSheetIntegrity) (after: BalanceSheetIntegrity) : IntegrityDelta =
        let d (f: BalanceSheetIntegrity -> Money.Money) = (after |> f |> Money.amount) - (before |> f |> Money.amount)
        { debits = d _.totalDebits
          credits = d _.totalCredits
          assets = d _.assets
          liabilities = d _.liabilities
          equity = d _.equity
          revenue = d _.revenue
          expenses = d _.expenses
          netIncome = d _.netIncome
          residual = d _.residual }

    let noChange =
        { debits = 0M; credits = 0M; assets = 0M; liabilities = 0M; equity = 0M
          revenue = 0M; expenses = 0M; netIncome = 0M; residual = 0M }

    /// Every test runs inside this rolled-back transaction, with the five accounts above already created.
    let withAccounts (test: Context.Context -> IntegrityAccounts -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! accounts = createAccounts context
                return! test (context |> TestContext.updateInitiationInstant) accounts
            })
        |> railroadWrapper

    (* One entry debiting Asset 70.00 and Expense 4.00 and crediting Liability 20.00, Equity 43.00 and Revenue 11.00:
       74.00 each side, and every line moves its own type up by its amount. *)
    let everyTypeLines (accounts: IntegrityAccounts) =
        [ (accounts.asset, 70.00M, "Debit", None)
          (accounts.expense, 4.00M, "Debit", None)
          (accounts.liability, 20.00M, "Credit", None)
          (accounts.equity, 43.00M, "Credit", None)
          (accounts.revenue, 11.00M, "Credit", None) ]

    let everyTypeDelta =
        { debits = 74.00M; credits = 74.00M; assets = 70.00M; liabilities = 20.00M; equity = 43.00M
          revenue = 11.00M; expenses = 4.00M; netIncome = 7.00M; residual = 0M }

    let lineDatedRelativeToAsOf entryDate asOf expected =
        withAccounts (fun context accounts ->
            result {
                let! before = computeBalanceSheetIntegrity context asOf
                let! _ = post context "Integrity dated line" entryDate (everyTypeLines accounts)
                let! after = computeBalanceSheetIntegrity context asOf
                Assert.Equal(expected, delta before after)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a journal entry line dated before the as-of date adds its amount to integrity total debits or total credits by its line type, leaves the other total unchanged, and moves its account type's net balance by its amount in that type's normal-balance direction``() =
        lineDatedRelativeToAsOf yesterday today everyTypeDelta

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a journal entry line dated on the as-of date adds its amount to integrity total debits or total credits by its line type, leaves the other total unchanged, and moves its account type's net balance by its amount in that type's normal-balance direction``() =
        lineDatedRelativeToAsOf today today everyTypeDelta

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a journal entry line dated after the as-of date leaves integrity total debits, total credits and every account-type net balance unchanged``() =
        lineDatedRelativeToAsOf today yesterday noChange

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a voided journal entry leaves integrity total debits, total credits and every account-type net balance unchanged``() =
        withAccounts (fun context accounts ->
            result {
                let! before = computeBalanceSheetIntegrity context today
                let! entryId = post context "Integrity voided" yesterday (everyTypeLines accounts)
                let! _ = entryId |> voidJournalEntry context None voidReason
                let! after = computeBalanceSheetIntegrity context today
                Assert.Equal(noChange, delta before after)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.1 integrity reports debits equal to credits as true when non-zero total debits equal total credits``() =
        withAccounts (fun context accounts ->
            result {
                let! _ = post context "Integrity balanced" today (everyTypeLines accounts)
                let! integrity = computeBalanceSheetIntegrity context today
                Assert.NotEqual(0M, integrity.totalDebits |> Money.amount)
                Assert.Equal(integrity.totalDebits |> Money.amount, integrity.totalCredits |> Money.amount)
                Assert.True(integrity.debitsEqualCredits)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.1 integrity reports debits equal to credits as false when total debits differ from total credits``() =
        withAccounts (fun context accounts ->
            result {
                let! entryId = post context "Integrity unbalanced" today (everyTypeLines accounts)
                do! raiseLine context entryId accounts.asset 7.00M
                let! integrity = computeBalanceSheetIntegrity context today
                Assert.NotEqual(integrity.totalDebits |> Money.amount, integrity.totalCredits |> Money.amount)
                Assert.False(integrity.debitsEqualCredits)
                return ()
            })

    (* Every type is both debited and credited, by different amounts, so a type read in the wrong direction comes out
       negated: Asset 100.00 - 30.00, Expense 40.00 - 33.00, Liability 25.00 - 5.00, Equity 12.00 - 2.00,
       Revenue 50.00 - 3.00. 150.00 each side. *)
    [<Fact>]
    member _.``REQ-RPT-5.2 each account-type net balance is its debits minus credits for Asset and Expense and its credits minus debits for Liability, Equity and Revenue``() =
        withAccounts (fun context accounts ->
            result {
                let! before = computeBalanceSheetIntegrity context today
                let! _ =
                    post context "Integrity both directions" today
                        [ (accounts.asset, 100.00M, "Debit", None)
                          (accounts.asset, 30.00M, "Credit", None)
                          (accounts.expense, 40.00M, "Debit", None)
                          (accounts.expense, 33.00M, "Credit", None)
                          (accounts.liability, 5.00M, "Debit", None)
                          (accounts.liability, 25.00M, "Credit", None)
                          (accounts.equity, 2.00M, "Debit", None)
                          (accounts.equity, 12.00M, "Credit", None)
                          (accounts.revenue, 3.00M, "Debit", None)
                          (accounts.revenue, 50.00M, "Credit", None) ]
                let! after = computeBalanceSheetIntegrity context today
                let d = delta before after
                Assert.Equal(70.00M, d.assets)
                Assert.Equal(7.00M, d.expenses)
                Assert.Equal(20.00M, d.liabilities)
                Assert.Equal(10.00M, d.equity)
                Assert.Equal(47.00M, d.revenue)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.2 integrity net income is the Revenue net balance minus the Expense net balance``() =
        withAccounts (fun context accounts ->
            result {
                let! before = computeBalanceSheetIntegrity context today
                let! _ =
                    post context "Integrity income" today
                        [ (accounts.asset, 45.00M, "Debit", None)
                          (accounts.expense, 15.00M, "Debit", None)
                          (accounts.revenue, 60.00M, "Credit", None) ]
                let! after = computeBalanceSheetIntegrity context today
                Assert.Equal(
                    (after.revenue |> Money.amount) - (after.expenses |> Money.amount),
                    after.netIncome |> Money.amount)
                Assert.Equal(45.00M, (delta before after).netIncome)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.2 integrity residual is Assets minus the sum of Liabilities, Equity and net income``() =
        withAccounts (fun context accounts ->
            result {
                let! entryId = post context "Integrity residual" today (everyTypeLines accounts)
                do! raiseLine context entryId accounts.liability 9.00M
                let! integrity = computeBalanceSheetIntegrity context today
                Assert.NotEqual(0M, integrity.residual |> Money.amount)
                Assert.Equal(
                    (integrity.assets |> Money.amount)
                    - ((integrity.liabilities |> Money.amount)
                       + (integrity.equity |> Money.amount)
                       + (integrity.netIncome |> Money.amount)),
                    integrity.residual |> Money.amount)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.3 integrity returns unequal debits and credits as data carrying both totals rather than as an error``() =
        withAccounts (fun context accounts ->
            result {
                let! before = computeBalanceSheetIntegrity context today
                let! entryId = post context "Integrity unequal totals" today (everyTypeLines accounts)
                do! raiseLine context entryId accounts.asset 7.00M
                let! after = computeBalanceSheetIntegrity context today
                let d = delta before after
                Assert.Equal(81.00M, d.debits)
                Assert.Equal(74.00M, d.credits)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-5.3 integrity returns a non-zero residual as data carrying that residual rather than as an error``() =
        withAccounts (fun context accounts ->
            result {
                let! before = computeBalanceSheetIntegrity context today
                let! entryId = post context "Integrity non-zero residual" today (everyTypeLines accounts)
                do! raiseLine context entryId accounts.asset 7.00M
                let! after = computeBalanceSheetIntegrity context today
                Assert.Equal(7.00M, (delta before after).residual)
                return ()
            })

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-RPT-5.4 a journal entry backdated onto an account after the account was deactivated lists that account with its code, name, active-end date, its non-zero balance and that entry, and no entry posted before its active end`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-5.4 voiding, after an account was deactivated, an entry that zeroed it lists the account with the residue balance and the voided entry`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-5.4 a deactivated account whose balance is zero is not listed, and an active account with a non-zero balance is not listed`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-5.4 an account whose active end is today, with a non-zero balance, is not listed`` () =
        Assert.Fail "Not yet implemented"
