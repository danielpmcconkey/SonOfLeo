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
open Model.Ledger.Journaling

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
    fiscalPeriodIds: FiscalPeriodId list
    closedFiscalPeriodId: FiscalPeriodId
    basicJeId: Guid
    jeWithRefId: Guid
    jeWithRefExtRefId: Guid
    voidedJeId: Guid
    jeInClosedPeriodId: Guid
    fixtureCommentId: Guid
    sharedRefJe1Id: Guid
    sharedRefJe2Id: Guid
    voidVictim1Id: Guid
    voidVictim2Id: Guid
    voidVictim3Id: Guid
    cliVoidVictimId: Guid
    cliUpdateVictimExtRefId: Guid
    cliUpdateVictimCommentId: Guid
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

            let assets1000Id = assets1000 |> Account.accountId
            let liabilities2000Id = liabilities2000 |> Account.accountId
            let equity3000Id = equity3000 |> Account.accountId
            let revenue4000Id = revenue4000 |> Account.accountId
            let expenses5000Id = expenses5000 |> Account.accountId

            let! rothIra1250 =
                Account.constructNewAndSaveToDb "F-1250" "Roth IRA" "Asset"
                    lastYear None (Some "Investment") (Some (assets1000Id |> AccountId.value)) None envelope None
            let! moneyMarket1270 =
                Account.constructNewAndSaveToDb "F-1270" "Money Market" "Asset"
                    lastYear None (Some "Cash") (Some (assets1000Id |> AccountId.value)) None envelope None
            let! mortgage2210 =
                Account.constructNewAndSaveToDb "F-2210" "Mortgage Payable" "Liability"
                    lastYear None (Some "LongTermLiability") (Some (liabilities2000Id |> AccountId.value)) None envelope None
            let! creditCard2220 =
                Account.constructNewAndSaveToDb "F-2220" "Credit Card" "Liability"
                    lastYear None (Some "CurrentLiability") (Some (liabilities2000Id |> AccountId.value)) None envelope None
            let! retirement3030 =
                Account.constructNewAndSaveToDb "F-3030" "Retirement Contributions" "Equity"
                    lastYear None None (Some (equity3000Id |> AccountId.value)) None envelope None
            let! personalRevenue4290 =
                Account.constructNewAndSaveToDb "F-4290" "Personal Revenue" "Revenue"
                    lastYear None (Some "OperatingRevenue") (Some (revenue4000Id |> AccountId.value)) None envelope None
            let! food5350 =
                Account.constructNewAndSaveToDb "F-5350" "Food" "Expense"
                    lastYear None (Some "OperatingExpense") (Some (expenses5000Id |> AccountId.value)) None envelope None
            let! entertainment5650 =
                Account.constructNewAndSaveToDb "F-5650" "Entertainment" "Expense"
                    lastYear None (Some "OperatingExpense") (Some (expenses5000Id |> AccountId.value)) None envelope None
            let! closedBank1290 =
                Account.constructNewAndSaveToDb "F-1290" "Closed Bank" "Asset"
                    lastYear (Some twoMonthsAgo) (Some "Cash") (Some (assets1000Id |> AccountId.value)) None envelope None

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

            let closedFiscalPeriodId = closedFiscalPeriod |> FiscalPeriod.fiscalPeriodId

            let moneyMarket1270Id = moneyMarket1270 |> Account.accountId
            let food5350Id = food5350 |> Account.accountId
            let rothIra1250Id = rothIra1250 |> Account.accountId
            let personalRevenue4290Id = personalRevenue4290 |> Account.accountId
            let entertainment5650Id = entertainment5650 |> Account.accountId
            let creditCard2220Id = creditCard2220 |> Account.accountId

            // =============================================================================
            // Create journal entries (component-level, bypassing orchestrateCreation's
            // internal transaction which can't see auto-committed FPs in Npgsql 10.x)
            // =============================================================================

            let jeEnvelope = AuditEnvelope.create JournalEntryPostNew

            let mortgage2210Id = mortgage2210 |> Account.accountId

            let! basicJeHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture basic JE" (Some "Test") today None jeEnvelope None
            let basicJeId = basicJeHeader |> JournalEntryHeader.journalEntryId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    basicJeId (mortgage2210Id |> AccountId.value) 100.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    basicJeId (food5350Id |> AccountId.value) 100.00M "Credit" (Some "Grocery run") jeEnvelope None

            let! jeWithRefHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture JE with reference" (Some "TestImport") today None jeEnvelope None
            let jeWithRefId = jeWithRefHeader |> JournalEntryHeader.journalEntryId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeWithRefId (rothIra1250Id |> AccountId.value) 50.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeWithRefId (personalRevenue4290Id |> AccountId.value) 50.00M "Credit" None jeEnvelope None
            let! jeWithRefExtRef =
                JournalEntryExternalReference.constructNewAndSaveToDb
                    jeWithRefId "TestBank" "TXN-001" jeEnvelope None
            let jeWithRefExtRefId =
                jeWithRefExtRef |> JournalEntryExternalReference.uniqueId

            let! jeToVoidHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture voided JE" None today None jeEnvelope None
            let jeToVoidId = jeToVoidHeader |> JournalEntryHeader.journalEntryId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeToVoidId (entertainment5650Id |> AccountId.value) 75.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeToVoidId (creditCard2220Id |> AccountId.value) 75.00M "Credit" None jeEnvelope None

            let closedPeriodEntryDate = today.PlusMonths(-5).PlusDays(14)
            let! jeInClosedPeriodHeader =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture JE in closed period" None closedPeriodEntryDate None jeEnvelope None
            let jeInClosedPeriodId =
                jeInClosedPeriodHeader |> JournalEntryHeader.journalEntryId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeInClosedPeriodId (mortgage2210Id |> AccountId.value) 25.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    jeInClosedPeriodId (food5350Id |> AccountId.value) 25.00M "Credit" None jeEnvelope None

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
            // Create shared-reference JE pair (two entries, one shared ext ref)
            // =============================================================================

            let! sharedRefJe1Header =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture shared-ref JE 1" (Some "Test") today None jeEnvelope None
            let sharedRefJe1Id = sharedRefJe1Header |> JournalEntryHeader.journalEntryId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    sharedRefJe1Id (mortgage2210Id |> AccountId.value) 10.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    sharedRefJe1Id (food5350Id |> AccountId.value) 10.00M "Credit" None jeEnvelope None
            let! _ =
                JournalEntryExternalReference.constructNewAndSaveToDb
                    sharedRefJe1Id "SharedBank" "F-SHARED-001" jeEnvelope None

            let! sharedRefJe2Header =
                JournalEntryHeader.constructNewAndSaveToDb
                    "Fixture shared-ref JE 2" (Some "Test") today None jeEnvelope None
            let sharedRefJe2Id = sharedRefJe2Header |> JournalEntryHeader.journalEntryId
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    sharedRefJe2Id (mortgage2210Id |> AccountId.value) 20.00M "Debit" None jeEnvelope None
            let! _ =
                JournalEntryLine.constructNewAndSaveToDb
                    sharedRefJe2Id (food5350Id |> AccountId.value) 20.00M "Credit" None jeEnvelope None
            let! _ =
                JournalEntryExternalReference.constructNewAndSaveToDb
                    sharedRefJe2Id "SharedBank" "F-SHARED-001" jeEnvelope None

            // =============================================================================
            // Create consumable update victim — the CLI UpdateExternalReference test
            // commits its mutation (no transaction across a subprocess), so it gets a
            // dedicated ext ref whose end-state doesn't matter.
            // =============================================================================

            let! cliUpdateVictimExtRef =
                JournalEntryExternalReference.constructNewAndSaveToDb
                    basicJeId "CliUpdateVictimBank" "CLI-UPDVIC-001" jeEnvelope None
            let cliUpdateVictimExtRefId =
                cliUpdateVictimExtRef |> JournalEntryExternalReference.uniqueId

            // =============================================================================
            // Create consumable void victims — one per voiding happy-path test
            // (three orchestrator tests plus the CLI Void route test).
            // Their voided end-state after a test run is by design.
            // =============================================================================

            let! voidVictims =
                [1..4]
                |> List.map (fun x ->
                    result {
                        let! victimHeader =
                            JournalEntryHeader.constructNewAndSaveToDb
                                $"Fixture void victim {x}" None today None jeEnvelope None
                        let victimId = victimHeader |> JournalEntryHeader.journalEntryId
                        let! _ =
                            JournalEntryLine.constructNewAndSaveToDb
                                victimId (entertainment5650Id |> AccountId.value) 33.00M "Debit" None jeEnvelope None
                        let! _ =
                            JournalEntryLine.constructNewAndSaveToDb
                                victimId (creditCard2220Id |> AccountId.value) 33.00M "Credit" None jeEnvelope None
                        return victimId
                    })
                |> ListHelper.listOfResultsToResultsList

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

            // consumable victim — the CLI UpdateComment test commits its mutation
            // (no transaction across a subprocess); end-state doesn't matter
            let! cliUpdateVictimComment =
                JournalEntryComment.constructNewAndSaveToDb
                    basicJeId None "CLI update victim comment"
                    commentEnvelope None

            let cliUpdateVictimCommentId =
                cliUpdateVictimComment |> JournalEntryComment.uniqueId

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
                retirement3030Id = retirement3030 |> Account.accountId
                personalRevenue4290Id = personalRevenue4290Id
                food5350Id = food5350Id
                entertainment5650Id = entertainment5650Id
                closedBank1290Id = closedBank1290 |> Account.accountId
                fiscalPeriodIds = fiscalPeriods |> List.map FiscalPeriod.fiscalPeriodId
                closedFiscalPeriodId = closedFiscalPeriodId
                basicJeId = basicJeId
                jeWithRefId = jeWithRefId
                jeWithRefExtRefId = jeWithRefExtRefId
                voidedJeId = jeToVoidId
                jeInClosedPeriodId = jeInClosedPeriodId
                fixtureCommentId = fixtureCommentId
                sharedRefJe1Id = sharedRefJe1Id
                sharedRefJe2Id = sharedRefJe2Id
                voidVictim1Id = voidVictims[0]
                voidVictim2Id = voidVictims[1]
                voidVictim3Id = voidVictims[2]
                cliVoidVictimId = voidVictims[3]
                cliUpdateVictimExtRefId = cliUpdateVictimExtRefId
                cliUpdateVictimCommentId = cliUpdateVictimCommentId
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
