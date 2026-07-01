namespace Tests.Integrated

open System
open Xunit
open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.FiscalPeriods
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction

type FixtureData = {
    assets1000Id: Guid
    liabilities2000Id: Guid
    equity3000Id: Guid
    revenue4000Id: Guid
    expenses5000Id: Guid
    rothIra1250Id: Guid
    moneyMarket1270Id: Guid
    mortgage2210Id: Guid
    creditCard2220Id: Guid
    retirement3030Id: Guid
    personalRevenue4290Id: Guid
    food5350Id: Guid
    entertainment5650Id: Guid
    closedBank1290Id: Guid
    fiscalPeriodIds: Guid list
    closedFiscalPeriodId: Guid
    basicJeId: Guid
    jeWithRefId: Guid
    jeWithRefExtRefId: Guid
    voidedJeId: Guid
    jeInClosedPeriodId: Guid
    fixtureCommentId: Guid
}

type TestDataFixture() =
    let data =
        let today = Calendar.today()
        let lastYear = today.PlusYears(-1)
        let envelope = AuditEnvelope.create AccountCreate
        let twoMonthsAgo = today.PlusMonths(-2)

        let stageResult = result {

            // =============================================================================
            // Create accounts
            // =============================================================================

            let! assets1000 =
                Account.constructNewAndSaveToDb "F-1000" "Assets" "Asset"
                    lastYear None None None None envelope None
            let! liabilities2000 =
                Account.constructNewAndSaveToDb "F-2000" "Liabilities" "Liability"
                    lastYear None None None None envelope None
            let! equity3000 =
                Account.constructNewAndSaveToDb "F-3000" "Equity" "Equity"
                    lastYear None None None None envelope None
            let! revenue4000 =
                Account.constructNewAndSaveToDb "F-4000" "Revenue" "Revenue"
                    lastYear None None None None envelope None
            let! expenses5000 =
                Account.constructNewAndSaveToDb "F-5000" "Expenses" "Expense"
                    lastYear None None None None envelope None

            let assets1000Id = assets1000 |> Account.uniqueId
            let liabilities2000Id = liabilities2000 |> Account.uniqueId
            let equity3000Id = equity3000 |> Account.uniqueId
            let revenue4000Id = revenue4000 |> Account.uniqueId
            let expenses5000Id = expenses5000 |> Account.uniqueId

            let! rothIra1250 =
                Account.constructNewAndSaveToDb "F-1250" "Roth IRA" "Asset"
                    lastYear None (Some "Investment") (Some assets1000Id) None envelope None
            let! moneyMarket1270 =
                Account.constructNewAndSaveToDb "F-1270" "Money Market" "Asset"
                    lastYear None (Some "Cash") (Some assets1000Id) None envelope None
            let! mortgage2210 =
                Account.constructNewAndSaveToDb "F-2210" "Mortgage Payable" "Liability"
                    lastYear None (Some "LongTermLiability") (Some liabilities2000Id) None envelope None
            let! creditCard2220 =
                Account.constructNewAndSaveToDb "F-2220" "Credit Card" "Liability"
                    lastYear None (Some "CurrentLiability") (Some liabilities2000Id) None envelope None
            let! retirement3030 =
                Account.constructNewAndSaveToDb "F-3030" "Retirement Contributions" "Equity"
                    lastYear None None (Some equity3000Id) None envelope None
            let! personalRevenue4290 =
                Account.constructNewAndSaveToDb "F-4290" "Personal Revenue" "Revenue"
                    lastYear None (Some "OperatingRevenue") (Some revenue4000Id) None envelope None
            let! food5350 =
                Account.constructNewAndSaveToDb "F-5350" "Food" "Expense"
                    lastYear None (Some "OperatingExpense") (Some expenses5000Id) None envelope None
            let! entertainment5650 =
                Account.constructNewAndSaveToDb "F-5650" "Entertainment" "Expense"
                    lastYear None (Some "OperatingExpense") (Some expenses5000Id) None envelope None
            let! closedBank1290 =
                Account.constructNewAndSaveToDb "F-1290" "Closed Bank" "Asset"
                    lastYear (Some twoMonthsAgo) (Some "Cash") (Some assets1000Id) None envelope None

            // =============================================================================
            // Create fiscal periods
            // =============================================================================

            let! fiscalPeriods =
                [-4..4]
                |> List.map (fun x ->
                    let date = x |> today.PlusMonths
                    let monthF = date.Month.ToString("D2")
                    let key = $"{date.Year}-{monthF}"
                    FiscalPeriod.constructNewAndSaveToDb key envelope None)
                |> ListHelper.listOfResultsToResultsList

            // =============================================================================
            // Create fiscal period that will be closed (after JE creation)
            // =============================================================================

            let! closedFiscalPeriod =
                let date = today.PlusMonths(-5)
                let monthF = date.Month.ToString("D2")
                let key = $"{date.Year}-{monthF}"
                FiscalPeriod.constructNewAndSaveToDb key envelope None

            let closedFiscalPeriodId = closedFiscalPeriod |> FiscalPeriod.uniqueId

            let moneyMarket1270Id = moneyMarket1270 |> Account.uniqueId
            let food5350Id = food5350 |> Account.uniqueId
            let rothIra1250Id = rothIra1250 |> Account.uniqueId
            let personalRevenue4290Id = personalRevenue4290 |> Account.uniqueId
            let entertainment5650Id = entertainment5650 |> Account.uniqueId
            let creditCard2220Id = creditCard2220 |> Account.uniqueId

            // =============================================================================
            // Create journal entries (component-level, bypassing orchestrateCreation's
            // internal transaction which can't see auto-committed FPs in Npgsql 10.x)
            // =============================================================================

            let jeEnvelope = AuditEnvelope.create JournalEntryPostNew

            let mortgage2210Id = mortgage2210 |> Account.uniqueId

            let! basicJeHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture basic JE" (Some "Test") today None jeEnvelope None
            let basicJeId = basicJeHeader |> JournalEntryHeader.uniqueId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    basicJeId mortgage2210Id 100.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    basicJeId food5350Id 100.00M "Credit" (Some "Grocery run") jeEnvelope None

            let! jeWithRefHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture JE with reference" (Some "TestImport") today None jeEnvelope None
            let jeWithRefId = jeWithRefHeader |> JournalEntryHeader.uniqueId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeWithRefId rothIra1250Id 50.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeWithRefId personalRevenue4290Id 50.00M "Credit" None jeEnvelope None
            let! jeWithRefExtRef =
                JournalEntryExternalReference.constructNewAndSaveToDb
                    jeWithRefId "TestBank" "TXN-001" jeEnvelope None
            let jeWithRefExtRefId =
                jeWithRefExtRef |> JournalEntryExternalReference.uniqueId

            let! jeToVoidHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture voided JE" None today None jeEnvelope None
            let jeToVoidId = jeToVoidHeader |> JournalEntryHeader.uniqueId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeToVoidId entertainment5650Id 75.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeToVoidId creditCard2220Id 75.00M "Credit" None jeEnvelope None

            let closedPeriodEntryDate = today.PlusMonths(-5).PlusDays(14)
            let! jeInClosedPeriodHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture JE in closed period" None closedPeriodEntryDate None jeEnvelope None
            let jeInClosedPeriodId =
                jeInClosedPeriodHeader |> JournalEntryHeader.uniqueId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeInClosedPeriodId mortgage2210Id 25.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeInClosedPeriodId food5350Id 25.00M "Credit" None jeEnvelope None

            // =============================================================================
            // Close the fiscal period (after JE creation)
            // =============================================================================

            let! _ = FiscalPeriod.closeFiscalPeriod closedFiscalPeriodId envelope None

            // =============================================================================
            // Void a journal entry (direct UPDATE, bypassing voidJournalEntryOrchestration)
            // =============================================================================

            let voidEnvelope = AuditEnvelope.create JournalEntryVoid
            let! _ =
                JournalEntryComment.constructNewAndSaveToDb
                    jeToVoidId None "Fixture voiding reason" voidEnvelope None
            let voidQuery = """
                UPDATE ledger.journal_entry
                SET voided_at = @voided_at, modified_at = @modified_at
                WHERE unique_id = @unique_id;"""
            let voidParams = [
                { name = "@voided_at"; value = DbInstant (AuditEnvelope.instant voidEnvelope) }
                { name = "@modified_at"; value = DbInstant (AuditEnvelope.instant voidEnvelope) }
                { name = "@unique_id"; value = UniqueId jeToVoidId } ]
            let! _ = executeNonQuery voidQuery voidParams ExactlyOne None

            // =============================================================================
            // Create fixture comment
            // =============================================================================

            let commentEnvelope = AuditEnvelope.create JournalEntryAddComment
            let! fixtureComment =
                JournalEntryComment.constructNewAndSaveToDb
                    basicJeId None "Fixture comment for testing"
                    commentEnvelope None

            let fixtureCommentId =
                fixtureComment |> JournalEntryComment.uniqueId

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
                retirement3030Id = retirement3030 |> Account.uniqueId
                personalRevenue4290Id = personalRevenue4290Id
                food5350Id = food5350Id
                entertainment5650Id = entertainment5650Id
                closedBank1290Id = closedBank1290 |> Account.uniqueId
                fiscalPeriodIds = fiscalPeriods |> List.map FiscalPeriod.uniqueId
                closedFiscalPeriodId = closedFiscalPeriodId
                basicJeId = basicJeId
                jeWithRefId = jeWithRefId
                jeWithRefExtRefId = jeWithRefExtRefId
                voidedJeId = jeToVoidId
                jeInClosedPeriodId = jeInClosedPeriodId
                fixtureCommentId = fixtureCommentId
            }
        }
        stageResult |> Result.defaultWith failwith

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
