namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open App.Operation.CoreAuditableAction
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryVoiding
open Business.CrossDomainOrchestration.PeriodActivity
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Xunit


(* Every test runs inside its own rolled-back transaction, on accounts that exist only there, and writes its own
   journal entries. The range runs from five days ago to five days ahead; the shared fixture's Revenue and Expense
   accounts may show up in it too, so each test looks only at its own PA- accounts, except where it says otherwise.

     PA-4000 Revenue, parent of PA-4100 and PA-4900      PA-4500 Revenue     PA-4700 Revenue     PA-4800 Revenue
     PA-5000 Expense     PA-5500 Expense
     PA-1000 Asset       PA-2000 Liability               PA-3000 Equity

   Depth-first order is PA-4000, PA-4100, PA-4900, PA-4500, PA-5000; a flat code sort would put PA-4500 before
   PA-4900, so the two orders differ. *)
type PeriodActivityAccounts =
    { revenueParent: AccountId
      revenueChildLow: AccountId
      revenueChildHigh: AccountId
      revenue: AccountId
      revenueOutOfRange: AccountId
      revenueVoidedOnly: AccountId
      expense: AccountId
      expenseNetsToZero: AccountId
      asset: AccountId
      liability: AccountId
      equity: AccountId }

[<Collection("SharedTestData")>]
type PeriodActivityTests(fixture: TestDataFixture) =

    let today = Calendar.today()
    let beginDate = today.PlusDays(-5)
    let endDate = today.PlusDays(5)
    let inRange = today

    let voidReason =
        "Voided so period activity can ignore it"
        |> CommentText.create
        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))

    let createAccounts (context: Context.Context) : Result<PeriodActivityAccounts, IAppError> =
        let create code accountType subtype parent =
            createTestAccountFromPrimitives
                context code $"Period activity {code}" accountType (today.PlusYears(-1)) None subtype parent None
            |> Result.map snd
        let revenue code parent = create code "Revenue" (Some "OperatingRevenue") parent
        let expense code = create code "Expense" (Some "OperatingExpense") None
        result {
            let! revenueParent = revenue "PA-4000" None
            let! revenueChildLow = revenue "PA-4100" (Some revenueParent)
            let! revenueChildHigh = revenue "PA-4900" (Some revenueParent)
            let! revenueAccount = revenue "PA-4500" None
            let! revenueOutOfRange = revenue "PA-4700" None
            let! revenueVoidedOnly = revenue "PA-4800" None
            let! expenseAccount = expense "PA-5000"
            let! expenseNetsToZero = expense "PA-5500"
            let! asset = create "PA-1000" "Asset" (Some "Cash") None
            let! liability = create "PA-2000" "Liability" (Some "CurrentLiability") None
            let! equity = create "PA-3000" "Equity" None None
            return
                { revenueParent = revenueParent
                  revenueChildLow = revenueChildLow
                  revenueChildHigh = revenueChildHigh
                  revenue = revenueAccount
                  revenueOutOfRange = revenueOutOfRange
                  revenueVoidedOnly = revenueVoidedOnly
                  expense = expenseAccount
                  expenseNetsToZero = expenseNetsToZero
                  asset = asset
                  liability = liability
                  equity = equity }
        }

    let post context description entryDate lines =
        createTestJournalEntryFromPrimitives context description None entryDate lines [] [] |> Result.map snd

    let activity context = fetchPeriodActivity context beginDate endDate

    let ownRows (rows: PeriodActivityAccount list) =
        rows |> List.filter (fun r -> (r.accountCode |> AccountCode.value).StartsWith "PA-")

    let rowFor code (rows: PeriodActivityAccount list) =
        rows |> List.filter (fun r -> r.accountCode |> AccountCode.value = code) |> List.exactlyOne

    let isListed code (rows: PeriodActivityAccount list) =
        rows |> List.exists (fun r -> r.accountCode |> AccountCode.value = code)

    let netOf code rows = (rows |> rowFor code).netTotal |> Money.amount

    let allLineEntryIds (rows: PeriodActivityAccount list) =
        rows |> List.collect (fun r -> r.lines |> List.map _.journalEntryId)

    /// Every test runs inside this rolled-back transaction, with the accounts above already created.
    let withAccounts (test: Context.Context -> PeriodActivityAccounts -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! accounts = createAccounts context
                return! test (context |> TestContext.updateInitiationInstant) accounts
            })
        |> railroadWrapper

    (* One line on the account under test, balanced by the asset account. *)
    let single context description entryDate (accounts: PeriodActivityAccounts) accountId amount lineType =
        let balancing = if lineType = "Debit" then "Credit" else "Debit"
        post context description entryDate [ (accountId, amount, lineType, None); (accounts.asset, amount, balancing, None) ]

    let includedOn entryDate =
        withAccounts (fun context accounts ->
            result {
                let! entryId = single context "Period activity boundary" entryDate accounts accounts.revenue 12.00M "Credit"
                let! rows = activity context
                let row = rows |> rowFor "PA-4500"
                Assert.Equal<JournalEntryHeaderId list>([ entryId ], row.lines |> List.map _.journalEntryId)
                Assert.Equal(entryDate, (row.lines |> List.exactlyOne).entryDate)
                Assert.Equal(12.00M, row.netTotal |> Money.amount)
                return ()
            })

    let excludedOn entryDate =
        withAccounts (fun context accounts ->
            result {
                let! outsideId = single context "Period activity outside" entryDate accounts accounts.revenue 12.00M "Credit"
                let! insideId = single context "Period activity inside" inRange accounts accounts.revenue 5.00M "Credit"
                let! rows = activity context
                Assert.DoesNotContain(outsideId, rows |> allLineEntryIds)
                Assert.Equal<JournalEntryHeaderId list>([ insideId ], (rows |> rowFor "PA-4500").lines |> List.map _.journalEntryId)
                Assert.Equal(5.00M, rows |> netOf "PA-4500")
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.1 a Revenue account's period activity net total is its credits minus its debits over the lines dated in the range, including debits against its normal balance``() =
        withAccounts (fun context accounts ->
            result {
                let! _ = single context "Period activity sale" inRange accounts accounts.revenue 100.00M "Credit"
                let! _ = single context "Period activity refund" inRange accounts accounts.revenue 30.00M "Debit"
                let! rows = activity context
                Assert.Equal(70.00M, rows |> netOf "PA-4500")
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.1 an Expense account's period activity net total is its debits minus its credits over the lines dated in the range, including credits against its normal balance``() =
        withAccounts (fun context accounts ->
            result {
                let! _ = single context "Period activity purchase" inRange accounts accounts.expense 80.00M "Debit"
                let! _ = single context "Period activity return" inRange accounts accounts.expense 25.00M "Credit"
                let! rows = activity context
                Assert.Equal(55.00M, rows |> netOf "PA-5000")
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated on the begin date is included in its account's period activity lines and net total``() =
        includedOn beginDate

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated on the end date is included in its account's period activity lines and net total``() =
        includedOn endDate

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated before the begin date is in no account's period activity lines and adds nothing to any net total``() =
        excludedOn (beginDate.PlusDays(-1))

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated after the end date is in no account's period activity lines and adds nothing to any net total``() =
        excludedOn (endDate.PlusDays(1))

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists every Revenue and Expense account with a line dated in the range, each exactly once with its code and name``() =
        withAccounts (fun context accounts ->
            result {
                let! _ = single context "Period activity revenue" inRange accounts accounts.revenue 10.00M "Credit"
                let! _ = single context "Period activity expense" inRange accounts accounts.expense 10.00M "Debit"
                let! _ = single context "Period activity child" inRange accounts accounts.revenueChildLow 10.00M "Credit"
                let! rows = activity context
                Assert.Equal<string list>(
                    [ "PA-4100"; "PA-4500"; "PA-5000" ],
                    rows |> ownRows |> List.map (_.accountCode >> AccountCode.value) |> List.sort)
                [ "PA-4100"; "PA-4500"; "PA-5000" ]
                |> List.iter (fun code ->
                    Assert.Equal($"Period activity {code}", (rows |> rowFor code).accountName |> AccountName.value))
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists no Revenue or Expense account whose lines all fall outside the range``() =
        withAccounts (fun context accounts ->
            result {
                let! _ = single context "Period activity early" (beginDate.PlusDays(-1)) accounts accounts.revenueOutOfRange 10.00M "Credit"
                let! _ = single context "Period activity late" (endDate.PlusDays(1)) accounts accounts.revenueOutOfRange 10.00M "Credit"
                let! rows = activity context
                Assert.False(rows |> isListed "PA-4700")
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists a Revenue or Expense account whose in-range lines net to zero, with a net total of zero``() =
        withAccounts (fun context accounts ->
            result {
                let! _ = single context "Period activity charge" inRange accounts accounts.expenseNetsToZero 20.00M "Debit"
                let! _ = single context "Period activity reversal" inRange accounts accounts.expenseNetsToZero 20.00M "Credit"
                let! rows = activity context
                let row = rows |> rowFor "PA-5500"
                Assert.Equal(0M, row.netTotal |> Money.amount)
                Assert.Equal(2, row.lines |> List.length)
                return ()
            })

    (* Beyond this file's own accounts, every account listed at all must be Revenue or Expense. *)
    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists no Asset, Liability or Equity account even when it has lines dated in the range``() =
        withAccounts (fun context accounts ->
            result {
                let! _ =
                    post context "Period activity every side" inRange
                        [ (accounts.asset, 60.00M, "Debit", None)
                          (accounts.liability, 20.00M, "Credit", None)
                          (accounts.equity, 30.00M, "Credit", None)
                          (accounts.revenue, 10.00M, "Credit", None) ]
                let! rows = activity context
                let! allAccounts = Account.fetchAll context false
                let typeOf code =
                    allAccounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountType
                Assert.True(rows |> isListed "PA-4500")
                Assert.False(rows |> isListed "PA-1000")
                Assert.False(rows |> isListed "PA-2000")
                Assert.False(rows |> isListed "PA-3000")
                Assert.All(rows, fun r ->
                    Assert.Contains(typeOf (r.accountCode |> AccountCode.value), [ Revenue; Expense ]))
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.1 each period activity account carries exactly its lines dated in the range, each with its entry date, journal entry ID, journal entry description, line type, amount and memo``() =
        withAccounts (fun context accounts ->
            result {
                let! firstId =
                    post context "Period activity first sale" (inRange.PlusDays(-1))
                        [ (accounts.revenue, 41.00M, "Credit", Some "first memo"); (accounts.asset, 41.00M, "Debit", None) ]
                let! secondId =
                    post context "Period activity refund" inRange
                        [ (accounts.revenue, 7.00M, "Debit", None); (accounts.asset, 7.00M, "Credit", None) ]
                let! _ = single context "Period activity too early" (beginDate.PlusDays(-1)) accounts accounts.revenue 99.00M "Credit"
                let! rows = activity context
                let actual =
                    (rows |> rowFor "PA-4500").lines
                    |> List.map (fun l ->
                        l.entryDate,
                        l.journalEntryId,
                        l.description |> JournalEntryDescription.value,
                        l.lineType |> JournalEntryLineType.toString,
                        l.amount |> Money.amount,
                        l.memo |> Option.map JournalEntryLineMemo.value)
                Assert.Equal<(NodaTime.LocalDate * JournalEntryHeaderId * string * string * decimal * string option) list>(
                    [ inRange.PlusDays(-1), firstId, "Period activity first sale", "Credit", 41.00M, Some "first memo"
                      inRange, secondId, "Period activity refund", "Debit", 7.00M, None ],
                    actual)
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.2 a voided journal entry's lines are in no account's period activity lines and add nothing to any net total``() =
        withAccounts (fun context accounts ->
            result {
                let! voidedId = single context "Period activity voided" inRange accounts accounts.revenue 40.00M "Credit"
                let! _ = voidedId |> voidJournalEntry context None voidReason
                let! keptId = single context "Period activity kept" inRange accounts accounts.revenue 5.00M "Credit"
                let! rows = activity context
                Assert.DoesNotContain(voidedId, rows |> allLineEntryIds)
                Assert.Equal<JournalEntryHeaderId list>([ keptId ], (rows |> rowFor "PA-4500").lines |> List.map _.journalEntryId)
                Assert.Equal(5.00M, rows |> netOf "PA-4500")
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.2 period activity lists no account whose only activity in the range is on voided journal entries``() =
        withAccounts (fun context accounts ->
            result {
                let! voidedId = single context "Period activity voided only" inRange accounts accounts.revenueVoidedOnly 40.00M "Credit"
                let! _ = voidedId |> voidJournalEntry context None voidReason
                let! rows = activity context
                Assert.False(rows |> isListed "PA-4800")
                return ()
            })

    [<Fact>]
    member _.``REQ-RPT-6.3 period activity accounts are in trial balance order: top-level accounts by code, each parent immediately before its children, children by code``() =
        withAccounts (fun context accounts ->
            result {
                // posted out of order, so neither creation nor posting order gives the expected one
                let! _ =
                    [ accounts.expense, "Debit"
                      accounts.revenueChildHigh, "Credit"
                      accounts.revenue, "Credit"
                      accounts.revenueChildLow, "Credit"
                      accounts.revenueParent, "Credit" ]
                    |> List.map (fun (accountId, lineType) ->
                        single context "Period activity order" inRange accounts accountId 10.00M lineType)
                    |> convertListOfResultsToResultsList
                let! rows = activity context
                Assert.Equal<string list>(
                    [ "PA-4000"; "PA-4100"; "PA-4900"; "PA-4500"; "PA-5000" ],
                    rows |> ownRows |> List.map (_.accountCode >> AccountCode.value))
                return ()
            })

    (* Several entries share a date, so only the journal entry ID can order them. IDs are random, so the posting order
       could happen to match ID order, and then a sort by date alone would pass; entries are added until it does not. *)
    [<Fact>]
    member _.``REQ-RPT-6.3 lines within a period activity account are ordered by entry date, then by journal entry ID for lines on the same date``() =
        withAccounts (fun context accounts ->
            let later = inRange
            let earlier = inRange.PlusDays(-2)
            let postLater n = single context $"Period activity later {n}" later accounts accounts.revenue 1.00M "Credit"
            let rec postUntilOutOfIdOrder (posted: JournalEntryHeaderId list) =
                result {
                    let! next = postLater (posted.Length + 1)
                    let posted = posted @ [ next ]
                    let sorted = posted |> List.sortBy JournalEntryHeaderId.value
                    if posted.Length >= 3 && sorted <> posted then return posted
                    elif posted.Length >= 12 then return! TestError.error (TestingError "IDs stayed in posting order")
                    else return! postUntilOutOfIdOrder posted
                }
            result {
                let! first = postLater 0
                let! earlierId = single context "Period activity earlier" earlier accounts accounts.revenue 2.00M "Credit"
                let! rest = postUntilOutOfIdOrder [ first ]
                let sameDate = rest |> List.sortBy JournalEntryHeaderId.value
                let! rows = activity context
                Assert.Equal<JournalEntryHeaderId list>(
                    earlierId :: sameDate,
                    (rows |> rowFor "PA-4500").lines |> List.map _.journalEntryId)
                return ()
            })

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-RPT-6.1 a parent Expense account with no lines of its own in the range is absent from period activity even when its children have lines in the range, and no child's lines appear under it`` () =
        Assert.Fail "Not yet implemented"
