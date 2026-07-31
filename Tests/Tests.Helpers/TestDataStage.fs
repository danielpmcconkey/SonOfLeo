namespace Tests.Helpers

open Context.Context
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open Logger.Audit
open Model.Ledger.Accounts
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open Tests.Helpers.EntityFunctions
open Utilities.ResultHelper
open Xunit
open Model.Ledger.FiscalPeriods
open Utilities
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Utilities.AppError

/// This data represents a known data state to stage at the beginning of test
/// runs. It should be used to test any read functions in the system. It can be
/// used to test any write functions so long as those write operations are
/// wrapped in a transaction and rolled back. The idea is to allow tests to
/// focus on what they're testing instead of staging the prerequisites.
type FixtureData =
    { assets1000Id: AccountId
      liabilities2000Id: AccountId
      equity3000Id: AccountId
      revenue4000Id: AccountId
      expenses5000Id: AccountId
      rothIra1250Id: AccountId
      moneyMarket1270Id: AccountId
      mortgage2210Id: AccountId
      creditCard2220Id: AccountId
      retirement3030Id: AccountId
      personalRevenue4290Id: AccountId
      food5350Id: AccountId
      entertainment5650Id: AccountId
      closedBank1290Id: AccountId
      closedAccount: Account
      openFiscalPeriodIds: FiscalPeriodId list
      closedFiscalPeriodId: FiscalPeriodId
      closedFiscalPeriod: FiscalPeriod
      basicJeId: JournalEntryHeaderId
      jeWithRefId: JournalEntryHeaderId
      jeWithRefExtRefId: JournalEntryExternalReferenceId
      jeWithLinesRefsAndCommentsId: JournalEntryHeaderId
      jeWithLinesRefsAndComments: JournalEntry
      voidedJeId: JournalEntryHeaderId
      jeInClosedPeriodId: JournalEntryHeaderId
      fixtureCommentId: JournalEntryCommentId
      sharedRefJe1Id: JournalEntryHeaderId
      sharedRefJe2Id: JournalEntryHeaderId
      sharedCommentJe2: JournalEntry
      sharedCommentJe1Id: JournalEntryHeaderId
      sharedCommentJe2Id: JournalEntryHeaderId
      totalAccounts: int
      totalClosedAccounts: int
      totalFiscalPeriods: int
      totalClosedFiscalPeriods: int
      totalJournalEntryHeaders: int
      totalVoidedJournalEntryHeaders: int
      totalJournalEntryLines: int
      totalVoidedJournalEntryLines: int
      totalAccountsWithLines: int
      totalAccountsWithNoLines: int
      accounts: Account list
      fiscalPeriods: FiscalPeriod list
      journalEntries: JournalEntry list
      journalEntryLines: JournalEntryLine list
      journalEntryExternalReferences: JournalEntryExternalReference list
      journalEntryComments: JournalEntryComment list }

type TestDataFixture() =
    let data =
        let today = Calendar.today()
        let yesterday = today.PlusDays(-1)
        let lastYear = today.PlusYears(-1)
        let twoMonthsAgo = today.PlusMonths(-2)

        let context = create NoTransaction FetchOnly

        let stageResult =

            result {

                // =============================================================================
                // Delete prior data
                // =============================================================================

                let deleteQuery =
                    """
                        TRUNCATE
                            ledger.journal_entry_comment,
                            ledger.journal_entry_ext_reference,
                            ledger.journal_entry_line,
                            ledger.journal_entry,
                            ledger.account,
                            ledger.fiscal_period
                        CASCADE;
                """
                let! _ = executeNonQuery (context |> getDatabaseTransaction) deleteQuery [] AnyQuantityIsAcceptable

                // =============================================================================
                // Set up counters to use for our fetch tests
                // =============================================================================

                // note: these are mutable for practical reasons and this is just a test harness
                let mutable accounts: Account list = []
                let mutable fiscalPeriods: FiscalPeriod list = []
                let mutable journalEntries: JournalEntry list = []

                // =============================================================================
                // Create accounts
                // =============================================================================

                let! assets1000, assets1000Id =
                    createTestAccountFromPrimitives context "F-1000" "Assets" "Asset" lastYear None None None None
                accounts <- assets1000 :: accounts

                let! liabilities2000, liabilities2000Id =
                    createTestAccountFromPrimitives
                        context
                        "F-2000"
                        "Liabilities"
                        "Liability"
                        lastYear
                        None
                        None
                        None
                        None
                accounts <- liabilities2000 :: accounts

                let! equity3000, equity3000Id =
                    createTestAccountFromPrimitives context "F-3000" "Equity" "Equity" lastYear None None None None
                accounts <- equity3000 :: accounts

                let! revenue4000, revenue4000Id =
                    createTestAccountFromPrimitives context "F-4000" "Revenue" "Revenue" lastYear None None None None
                accounts <- revenue4000 :: accounts

                let! expenses5000, expenses5000Id =
                    createTestAccountFromPrimitives context "F-5000" "Expenses" "Expense" lastYear None None None None
                accounts <- expenses5000 :: accounts

                let! rothIra1250, rothIra1250Id =
                    createTestAccountFromPrimitives
                        context
                        "F-1250"
                        "Roth IRA"
                        "Asset"
                        lastYear
                        None
                        (Some "Investment")
                        (Some assets1000Id)
                        None
                accounts <- rothIra1250 :: accounts

                let! moneyMarket1270, moneyMarket1270Id =
                    createTestAccountFromPrimitives
                        context
                        "F-1270"
                        "Money Market"
                        "Asset"
                        lastYear
                        None
                        (Some "Cash")
                        (Some assets1000Id)
                        None
                accounts <- moneyMarket1270 :: accounts

                let! mortgage2210, mortgage2210Id =
                    createTestAccountFromPrimitives
                        context
                        "F-2210"
                        "Mortgage Payable"
                        "Liability"
                        lastYear
                        None
                        (Some "LongTermLiability")
                        (Some liabilities2000Id)
                        None
                accounts <- mortgage2210 :: accounts

                let! creditCard2220, creditCard2220Id =
                    createTestAccountFromPrimitives
                        context
                        "F-2220"
                        "Credit Card"
                        "Liability"
                        lastYear
                        None
                        (Some "CurrentLiability")
                        (Some liabilities2000Id)
                        None
                accounts <- creditCard2220 :: accounts

                let! retirement3030, retirement3030Id =
                    createTestAccountFromPrimitives
                        context
                        "F-3030"
                        "Retirement Contributions"
                        "Equity"
                        lastYear
                        None
                        None
                        (Some equity3000Id)
                        None
                accounts <- retirement3030 :: accounts

                let! personalRevenue4290, personalRevenue4290Id =
                    createTestAccountFromPrimitives
                        context
                        "F-4290"
                        "Personal Revenue"
                        "Revenue"
                        lastYear
                        None
                        (Some "OperatingRevenue")
                        (Some revenue4000Id)
                        None
                accounts <- personalRevenue4290 :: accounts

                let! food5350, food5350Id =
                    createTestAccountFromPrimitives
                        context
                        "F-5350"
                        "Food"
                        "Expense"
                        lastYear
                        None
                        (Some "OperatingExpense")
                        (Some expenses5000Id)
                        None
                accounts <- food5350 :: accounts

                let! entertainment5650, entertainment5650Id =
                    createTestAccountFromPrimitives
                        context
                        "F-5650"
                        "Entertainment"
                        "Expense"
                        lastYear
                        None
                        (Some "OperatingExpense")
                        (Some expenses5000Id)
                        None
                accounts <- entertainment5650 :: accounts


                // create an account that will be closed after we add an entry to it
                let! _, closedBank1290Id =
                    createTestAccountFromPrimitives
                        context
                        "F-1290"
                        "Closed Bank"
                        "Asset"
                        lastYear
                        None
                        (Some "Cash")
                        (Some assets1000Id)
                        None
                // note: don't add it yet. only after it's been closed

                // =============================================================================
                // Create fiscal periods
                // =============================================================================

                let! openFiscalPeriods =
                    [ -4 .. 4 ]
                    |> List.map(fun x ->
                        let date = x |> today.PlusMonths
                        let monthF = date.Month.ToString("D2")
                        let key =
                            $"{date.Year}-{monthF}"
                            |> FiscalPeriodKey.fromString
                            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
                        key |> FiscalPeriodCreation.constructNewAndSaveToDb context)
                    |> convertListOfResultsToResultsList
                fiscalPeriods <- openFiscalPeriods @ fiscalPeriods

                // =============================================================================
                // Create fiscal period that will be closed (after JE creation)
                // =============================================================================

                let! closedFiscalPeriod =
                    let date = today.PlusMonths(-5)
                    let monthF = date.Month.ToString("D2")
                    let key =
                        $"{date.Year}-{monthF}"
                        |> FiscalPeriodKey.fromString
                        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
                    key |> FiscalPeriodCreation.constructNewAndSaveToDb context
                // note: don't add it to the FP list until after you've closed it

                let closedFiscalPeriodId = closedFiscalPeriod |> FiscalPeriod.fiscalPeriodId

                // =============================================================================
                // Create journal entries
                // =============================================================================

                let! basicJe, basicJeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "Basic journal entry"
                        None
                        today
                        [ (mortgage2210Id, 100.00M, "Debit", None)
                          (food5350Id, 100.00M, "Credit", (Some "Grocery run")) ]
                        []
                        [ (None, "Fixture comment for testing") ]
                journalEntries <- basicJe :: journalEntries

                let fixtureCommentId = // todo: figure out why we need this
                    basicJe |> JournalEntry.comments |> List.head |> JournalEntryComment.journalEntryCommentId

                let! jeWithRef, jeWithRefId =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture JE with reference"
                        (Some "TestImport")
                        yesterday
                        [ (rothIra1250Id, 50.00M, "Debit", None)
                          (personalRevenue4290Id, 50.00M, "Credit", None) ]
                        [ ("TestBank", "TXN-001") ]
                        []
                journalEntries <- jeWithRef :: journalEntries

                let jeWithRefExtRefId = // todo: figure out why we need this
                    jeWithRef
                    |> JournalEntry.externalReferences
                    |> List.head
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId

                let! _, jeToVoidId =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture voided JE"
                        None
                        yesterday
                        [ (entertainment5650Id, 75.00M, "Debit", None)
                          (creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                // note: don't add jeToVoid to the list because we later update it by voiding

                let! jeToNotVoid, _ = // this is here to ensure we have an account with both voided and not-voided JEs
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture voided JE"
                        None
                        yesterday
                        [ (entertainment5650Id, 86.04M, "Debit", None)
                          (creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                journalEntries <- jeToNotVoid :: journalEntries

                let closedPeriodEntryDate = (closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)

                let! jeInClosedPeriod, jeInClosedPeriodId =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture JE in closed period"
                        None
                        closedPeriodEntryDate
                        [ (mortgage2210Id, 25.00M, "Debit", None)
                          (food5350Id, 25.00M, "Credit", None) ]
                        []
                        []
                journalEntries <- jeInClosedPeriod :: journalEntries

                let! jeInClosedAccount, _ =
                    createTestJournalEntryFromPrimitives
                        context
                        "Journal entry in closed account"
                        None
                        closedPeriodEntryDate
                        [ (closedBank1290Id, 71.38M, "Debit", None)
                          (food5350Id, 71.38M, "Credit", (Some "Grocery run")) ]
                        []
                        []
                journalEntries <- jeInClosedAccount :: journalEntries

                // need to offset the transaction so it has a zero balance
                let! jeInClosedAccount2, _ =
                    createTestJournalEntryFromPrimitives
                        context
                        "Journal entry in closed account"
                        None
                        (closedPeriodEntryDate.PlusWeeks(1))
                        [ (closedBank1290Id, 71.38M, "Credit", None)
                          (food5350Id, 71.38M, "Debit", (Some "Grocery refund")) ]
                        []
                        []
                journalEntries <- jeInClosedAccount2 :: journalEntries

                let! jeWithLinesRefsAndComments, jeWithLinesRefsAndCommentsId =
                    createTestJournalEntryFromPrimitives
                        context
                        "Basic journal entry"
                        None
                        today
                        [ (mortgage2210Id, 100.00M, "Debit", None)
                          (food5350Id, 100.00M, "Credit", (Some "Grocery run")) ]
                        [ ("TestBank", "TXN-001") ]
                        [ (None, "Fixture comment for testing") ]
                journalEntries <- jeWithLinesRefsAndComments :: journalEntries

                // =============================================================================
                // Close the fiscal period and account (after JE creation)
                // =============================================================================

                let! updatedFiscalPeriod = FiscalPeriod.closeFiscalPeriod context closedFiscalPeriodId
                fiscalPeriods <- updatedFiscalPeriod :: fiscalPeriods

                let! closedBank1290 = closedBank1290Id |> Account.fetchById context
                let! updatedClosedBank =
                    closedBank1290 |> AccountDeactivation.deactivateAccount context (Some twoMonthsAgo)
                accounts <- updatedClosedBank :: accounts

                // =============================================================================
                // Void a journal entry (after JE creation)
                // =============================================================================

                let! commentText = "Fixture voiding reason" |> CommentText.create
                let! voidedJe = jeToVoidId |> JournalEntryVoiding.voidJournalEntry context None commentText
                let voidedJeId = voidedJe |> JournalEntry.header |> JournalEntryHeader.journalEntryHeaderId
                journalEntries <- voidedJe :: journalEntries

                // =============================================================================
                // Create shared-reference JE pair (two entries, one shared ext ref)
                // =============================================================================

                let! sharedRefJe1, sharedRefJe1Id =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture shared-ref JE 1"
                        (Some "Test")
                        today
                        [ (mortgage2210Id, 10.00M, "Debit", None)
                          (food5350Id, 10.00M, "Credit", None) ]
                        [ ("TestBank", "F-SHARED-001") ]
                        []
                journalEntries <- sharedRefJe1 :: journalEntries

                let! sharedRefJe2, sharedRefJe2Id =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture shared-ref JE 2"
                        (Some "Test")
                        today
                        [ (mortgage2210Id, 20.00M, "Debit", None)
                          (food5350Id, 20.00M, "Credit", None) ]
                        [ ("TestBank", "F-SHARED-001") ]
                        []
                journalEntries <- sharedRefJe2 :: journalEntries

                // =============================================================================
                // Create shared-comment JE pair (two entries, one with a comment referencing the other)
                // =============================================================================

                let! sharedCommentJe1, sharedCommentJe1Id =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture shared-comment JE 1"
                        (Some "Test")
                        today
                        [ (mortgage2210Id, 34.00M, "Debit", None)
                          (food5350Id, 34.00M, "Credit", None) ]
                        []
                        []
                journalEntries <- sharedCommentJe1 :: journalEntries

                let! sharedCommentJe2, sharedCommentJe2Id =
                    createTestJournalEntryFromPrimitives
                        context
                        "Fixture shared-comment JE 2"
                        (Some "Test")
                        today
                        [ (mortgage2210Id, 34.03M, "Debit", None)
                          (food5350Id, 34.03M, "Credit", None) ]
                        []
                        [ (Some sharedCommentJe1Id, "Comment that points to my bro") ]
                journalEntries <- sharedCommentJe2 :: journalEntries

                // =============================================================================
                // Calculate aggregate totals for fetch tests
                // =============================================================================

                let totalAccounts = accounts |> List.length
                let totalFiscalPeriods = fiscalPeriods |> List.length
                let totalClosedAccounts =
                    accounts
                    |> List.filter(fun l -> l |> Account.activityPeriod |> AccountActivityPeriod.isActive today |> not)
                    |> List.length
                let totalClosedFiscalPeriods =
                    fiscalPeriods |> List.filter(fun fp -> fp |> FiscalPeriod.isOpen = false) |> List.length
                let totalJournalEntryHeaders = journalEntries |> List.length
                let voidedEntries =
                    journalEntries
                    |> List.filter(fun x -> x |> JournalEntry.header |> JournalEntryHeader.voidedAt |> Option.isSome)
                let totalVoidedJournalEntryHeaders = voidedEntries |> List.length
                let totalJournalEntryLines =
                    journalEntries |> List.sumBy(fun x -> x |> JournalEntry.lines |> List.length)
                let totalVoidedJournalEntryLines =
                    voidedEntries |> List.sumBy(fun x -> x |> JournalEntry.lines |> List.length)
                let journalEntryLines = journalEntries |> List.collect JournalEntry.lines
                let totalAccountsWithLines =
                    journalEntryLines |> List.map JournalEntryLine.accountId |> List.distinct |> List.length
                let totalAccountsWithNoLines = totalAccounts - totalAccountsWithLines
                let journalEntryExternalReferences = journalEntries |> List.collect JournalEntry.externalReferences
                let journalEntryComments = journalEntries |> List.collect JournalEntry.comments

                // =============================================================================
                // Return all the data
                // =============================================================================

                return
                    { assets1000Id = assets1000Id
                      liabilities2000Id = liabilities2000Id
                      equity3000Id = equity3000Id
                      revenue4000Id = revenue4000Id
                      expenses5000Id = expenses5000Id
                      rothIra1250Id = rothIra1250Id
                      moneyMarket1270Id = moneyMarket1270Id
                      mortgage2210Id = mortgage2210Id
                      creditCard2220Id = creditCard2220Id
                      retirement3030Id = retirement3030Id
                      personalRevenue4290Id = personalRevenue4290Id
                      food5350Id = food5350Id
                      entertainment5650Id = entertainment5650Id
                      closedBank1290Id = closedBank1290Id
                      closedAccount = updatedClosedBank
                      openFiscalPeriodIds = openFiscalPeriods |> List.map FiscalPeriod.fiscalPeriodId
                      closedFiscalPeriodId = closedFiscalPeriodId
                      closedFiscalPeriod = updatedFiscalPeriod
                      basicJeId = basicJeId
                      jeWithRefId = jeWithRefId
                      jeWithRefExtRefId = jeWithRefExtRefId
                      jeWithLinesRefsAndComments = jeWithLinesRefsAndComments
                      jeWithLinesRefsAndCommentsId = jeWithLinesRefsAndCommentsId
                      voidedJeId = voidedJeId
                      jeInClosedPeriodId = jeInClosedPeriodId
                      fixtureCommentId = fixtureCommentId
                      sharedRefJe1Id = sharedRefJe1Id
                      sharedRefJe2Id = sharedRefJe2Id
                      sharedCommentJe2 = sharedCommentJe2
                      sharedCommentJe1Id = sharedCommentJe1Id
                      sharedCommentJe2Id = sharedCommentJe2Id
                      totalAccounts = totalAccounts
                      totalClosedAccounts = totalClosedAccounts
                      totalFiscalPeriods = totalFiscalPeriods
                      totalClosedFiscalPeriods = totalClosedFiscalPeriods
                      totalJournalEntryHeaders = totalJournalEntryHeaders
                      totalVoidedJournalEntryHeaders = totalVoidedJournalEntryHeaders
                      totalJournalEntryLines = totalJournalEntryLines
                      totalVoidedJournalEntryLines = totalVoidedJournalEntryLines
                      totalAccountsWithLines = totalAccountsWithLines
                      totalAccountsWithNoLines = totalAccountsWithNoLines
                      accounts = accounts
                      fiscalPeriods = fiscalPeriods
                      journalEntries = journalEntries
                      journalEntryLines = journalEntryLines
                      journalEntryExternalReferences = journalEntryExternalReferences
                      journalEntryComments = journalEntryComments }
            }
        stageResult |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    member _.Data = data

[<CollectionDefinition("SharedTestData")>]
type SharedTestDataCollection() =
    interface ICollectionFixture<TestDataFixture>
