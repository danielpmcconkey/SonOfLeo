namespace Tests.Integrated

open System
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open Tests.Integrated.GenericTestProperties
open Utilities.ResultHelper
open Xunit
open Model.Audit
open Model.Ledger.FiscalPeriods
open Utilities
open Utilities.DAL
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Utilities.AppError

/// This data represents a known data state to stage at the beginning of test
/// runs. It should be used to test any read functions in the system. It can be
/// used to test any write functions so long as those write operations are
/// wrapped in a transaction and rolled back. The idea is to allow tests to
/// focus on what they're testing instead of staging the prerequisites. 
type FixtureData = {
    assets1000Id: AccountId
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
    openFiscalPeriodIds: FiscalPeriodId list
    closedFiscalPeriodId: FiscalPeriodId
    basicJeId: JournalEntryHeaderId
    jeWithRefId: JournalEntryHeaderId
    jeWithRefExtRefId: JournalEntryExternalReferenceId
    voidedJeId: JournalEntryHeaderId
    jeInClosedPeriodId: JournalEntryHeaderId
    fixtureCommentId: JournalEntryCommentId
    sharedRefJe1Id: JournalEntryHeaderId
    sharedRefJe2Id: JournalEntryHeaderId
}

type TestDataFixture() =
    let data =
        let today = Calendar.today()
        let yesterday = today.PlusDays(-1)
        let lastYear = today.PlusYears(-1)
        let envelope = AuditEnvelope.create AccountCreate
        let twoMonthsAgo = today.PlusMonths(-2)
        
        let stageResult = result {
 
            // =============================================================================
            // Create accounts
            // =============================================================================
 
            let! assets1000Id = createTestAccountFromPrimitives "F-1000" "Assets" "Asset" lastYear None None None None envelope None
            let! liabilities2000Id = createTestAccountFromPrimitives "F-2000" "Liabilities" "Liability" lastYear None None None None envelope None
            let! equity3000Id = createTestAccountFromPrimitives "F-3000" "Equity" "Equity" lastYear None None None None envelope None
            let! revenue4000Id = createTestAccountFromPrimitives "F-4000" "Revenue" "Revenue" lastYear None None None None envelope None
            let! expenses5000Id = createTestAccountFromPrimitives "F-5000" "Expenses" "Expense" lastYear None None None None envelope None
 
            let! rothIra1250Id = createTestAccountFromPrimitives "F-1250" "Roth IRA" "Asset" lastYear None (Some "Investment") (Some assets1000Id) None envelope None
            let! moneyMarket1270Id = createTestAccountFromPrimitives "F-1270" "Money Market" "Asset" lastYear None (Some "Cash") (Some assets1000Id) None envelope None
            let! mortgage2210Id = createTestAccountFromPrimitives "F-2210" "Mortgage Payable" "Liability" lastYear None (Some "LongTermLiability") (Some liabilities2000Id) None envelope None
            let! creditCard2220Id = createTestAccountFromPrimitives "F-2220" "Credit Card" "Liability" lastYear None (Some "CurrentLiability") (Some liabilities2000Id) None envelope None
            let! retirement3030Id = createTestAccountFromPrimitives "F-3030" "Retirement Contributions" "Equity" lastYear None None (Some equity3000Id) None envelope None
            let! personalRevenue4290Id = createTestAccountFromPrimitives "F-4290" "Personal Revenue" "Revenue" lastYear None (Some "OperatingRevenue") (Some revenue4000Id) None envelope None
            let! food5350Id = createTestAccountFromPrimitives "F-5350" "Food" "Expense" lastYear None (Some "OperatingExpense") (Some expenses5000Id) None envelope None
            let! entertainment5650Id = createTestAccountFromPrimitives "F-5650" "Entertainment" "Expense" lastYear None (Some "OperatingExpense") (Some expenses5000Id ) None envelope None
            let! closedBank1290Id = createTestAccountFromPrimitives "F-1290" "Closed Bank" "Asset" lastYear (Some twoMonthsAgo) (Some "Cash") (Some assets1000Id ) None envelope None
 
            // =============================================================================
            // Create fiscal periods
            // =============================================================================
 
            let! openFiscalPeriods =
                [-4..4]
                |> List.map (fun x ->
                    let date = x |> today.PlusMonths
                    let monthF = date.Month.ToString("D2")
                    let key = $"{date.Year}-{monthF}" |> FiscalPeriodKey.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
                    FiscalPeriodCreation.constructNewAndSaveToDb key envelope None)
                |> convertListOfResultsToResultsList
 
            // =============================================================================
            // Create fiscal period that will be closed (after JE creation)
            // =============================================================================
 
            let! closedFiscalPeriod =
               let date = today.PlusMonths(-5)
               let monthF = date.Month.ToString("D2")
               let key = $"{date.Year}-{monthF}" |> FiscalPeriodKey.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
               FiscalPeriodCreation.constructNewAndSaveToDb key envelope None
 
            let closedFiscalPeriodId = closedFiscalPeriod |> FiscalPeriod.fiscalPeriodId
 
            // =============================================================================
            // Create journal entries (component-level, bypassing orchestrateCreation's
            // internal transaction which can't see auto-committed FPs in Npgsql 10.x)
            // =============================================================================
 
            let jeEnvelope = AuditEnvelope.create JournalEntryPostNew
 
            let! basicJe, basicJeId =
                createTestJournalEntryFromPrimitives "Basic journal entry" None today 
                    [ (mortgage2210Id, 100.00M, "Debit", None)
                      (food5350Id, 100.00M, "Credit", (Some "Grocery run")) ]
                    [  ]
                    [ (None, "Fixture comment for testing") ]
                    jeEnvelope
 
            let fixtureCommentId = // todo: figure out why we need this
               basicJe
               |> JournalEntry.comments
               |> List.head
               |> JournalEntryComment.journalEntryCommentId
 
            let! jeWithRef, jeWithRefId =
                createTestJournalEntryFromPrimitives "Fixture JE with reference" (Some "TestImport") yesterday 
                    [ (rothIra1250Id, 50.00M, "Debit", None)
                      (personalRevenue4290Id, 50.00M, "Credit", None) ]
                    [ ("TestBank", "TXN-001") ]
                    []
                    jeEnvelope
 
            let jeWithRefExtRefId = // todo: figure out why we need this
               jeWithRef
               |> JournalEntry.externalReferences
               |> List.head
               |> JournalEntryExternalReference.journalEntryExternalReferenceId
 
            let! jeToVoid, jeToVoidId =
                createTestJournalEntryFromPrimitives "Fixture voided JE" None yesterday 
                    [ (entertainment5650Id, 75.00M, "Debit", None)
                      (creditCard2220Id, 75.00M, "Credit", None) ]
                    [  ]
                    []
                    jeEnvelope
 
            let closedPeriodEntryDate = today.PlusMonths(-5).PlusDays(14)
 
            let! jeInClosedPeriod, jeInClosedPeriodId =
                createTestJournalEntryFromPrimitives "Fixture JE in closed period" None closedPeriodEntryDate 
                    [ (mortgage2210Id, 25.00M, "Debit", None)
                      (food5350Id, 25.00M, "Credit", None) ]
                    [  ]
                    []
                    jeEnvelope
 
            // =============================================================================
            // Close the fiscal period (after JE creation)
            // =============================================================================
 
            let! _ = FiscalPeriod.closeFiscalPeriod closedFiscalPeriodId envelope None
 
            // =============================================================================
            // Void a journal entry (after JE creation)
            // =============================================================================
 
            let voidEnvelope = AuditEnvelope.create JournalEntryVoid
            let! commentText = "Fixture voiding reason" |> CommentText.create
            let! voidedJe = jeToVoidId |> JournalEntryVoiding.voidJournalEntry voidEnvelope None commentText
            let voidedJeId = voidedJe |> JournalEntry.header |> JournalEntryHeader.journalEntryHeaderId
 
            // =============================================================================
            // Create shared-reference JE pair (two entries, one shared ext ref)
            // =============================================================================
 
            let! sharedRefJe1, sharedRefJe1Id =
                createTestJournalEntryFromPrimitives "Fixture shared-ref JE 1" (Some "Test") today 
                    [ (mortgage2210Id, 10.00M, "Debit", None)
                      (food5350Id, 10.00M, "Credit", None) ]
                    [ ("SharedBank", "F-SHARED-001") ]
                    []
                    jeEnvelope
 
            let! sharedRefJe2, sharedRefJe2Id =
                createTestJournalEntryFromPrimitives "Fixture shared-ref JE 2" (Some "Test") today 
                    [ (mortgage2210Id, 20.00M, "Debit", None)
                      (food5350Id, 20.00M, "Credit", None) ]
                    [ ("SharedBank", "F-SHARED-001") ]
                    []
                    jeEnvelope
 
            
 
            return {
               assets1000Id = assets1000Id
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
               openFiscalPeriodIds = openFiscalPeriods |> List.map FiscalPeriod.fiscalPeriodId
               closedFiscalPeriodId = closedFiscalPeriodId
               basicJeId = basicJeId
               jeWithRefId = jeWithRefId
               jeWithRefExtRefId = jeWithRefExtRefId
               voidedJeId = voidedJeId
               jeInClosedPeriodId = jeInClosedPeriodId
               fixtureCommentId = fixtureCommentId
               sharedRefJe1Id = sharedRefJe1Id
               sharedRefJe2Id = sharedRefJe2Id
            }
        }
        stageResult |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
 
    member _.Data = data
 
    interface IDisposable with
       member _.Dispose() =
          let query = """
             TRUNCATE
                ledger.journal_entry_comment,
                ledger.journal_entry_ext_reference,
                ledger.journal_entry_line,
                ledger.journal_entry,
                ledger.account,
                ledger.fiscal_period
             CASCADE;"""
          executeNonQuery query [] AnyQuantityIsAcceptable None |> ignore
 
[<CollectionDefinition("SharedTestData")>]
type SharedTestDataCollection() =
    interface ICollectionFixture<TestDataFixture>
