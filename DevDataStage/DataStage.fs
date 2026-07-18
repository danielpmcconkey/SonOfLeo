module DevDataStage.DataStage


open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open ModelOrchestrator.JournalEntryVoiding
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open Model.Ledger.Journaling
open Model.Ledger.JournalEntryPrimitives

/// run this any time you need to add new data to the dev database. This is
/// here purely for convenience and should not be reviewed as part of the
/// main code base
let stageData =
    let today = Calendar.today()
    let lastYear = today.PlusYears(-1)
    let envelope = AuditEnvelope.create AccountCreate
    let twoMonthsAgo = today.PlusMonths(-2)

    result {
        
        // =============================================================================
        // Delete prior data
        // =============================================================================
        
        let deleteQuery = """
                TRUNCATE
                    ledger.journal_entry_comment,
                    ledger.journal_entry_ext_reference,
                    ledger.journal_entry_line,
                    ledger.journal_entry,
                    ledger.account,
                    ledger.fiscal_period
                CASCADE;
        """
        let! _ = executeNonQuery deleteQuery [] AnyQuantityIsAcceptable None
        
        // =============================================================================
        // Create accounts
        // =============================================================================

        let! assets1000 =
            Account.constructNewAndSaveToDb "1000" "Assets" "Asset"
                lastYear None None None None envelope None
        let! liabilities2000 =
            Account.constructNewAndSaveToDb "2000" "Liabilities" "Liability"
                lastYear None None None None envelope None
        let! equity3000 =
            Account.constructNewAndSaveToDb "3000" "Equity" "Equity"
                lastYear None None None None envelope None
        let! revenue4000 =
            Account.constructNewAndSaveToDb "4000" "Revenue" "Revenue"
                lastYear None None None None envelope None
        let! expenses5000 =
            Account.constructNewAndSaveToDb "5000" "Expenses" "Expense"
                lastYear None None None None envelope None

        let assets1000Id = assets1000 |> Account.accountId
        let liabilities2000Id = liabilities2000 |> Account.accountId
        let equity3000Id = equity3000 |> Account.accountId
        let revenue4000Id = revenue4000 |> Account.accountId
        let expenses5000Id = expenses5000 |> Account.accountId

        let! checking1110 =
            Account.constructNewAndSaveToDb "1110" "Checking account" "Asset"
                lastYear None (Some "Cash") (Some (assets1000Id |> AccountId.value)) None envelope None
        let! moneyMarket1270 =
            Account.constructNewAndSaveToDb "1270" "Money Market" "Asset"
                lastYear None (Some "Cash") (Some (assets1000Id |> AccountId.value)) None envelope None
        let! food5350 =
            Account.constructNewAndSaveToDb "5350" "Food" "Expense"
                lastYear None (Some "OperatingExpense") (Some (expenses5000Id |> AccountId.value)) None envelope None
        let! entertainment5410 =
            Account.constructNewAndSaveToDb "5410" "Entertainment" "Expense"
                lastYear None (Some "OperatingExpense") (Some (expenses5000Id |> AccountId.value)) None envelope None
        
        let checking1110Id = checking1110 |> Account.accountId
        let moneyMarket1270Id = moneyMarket1270 |> Account.accountId
        let food5350Id = food5350 |> Account.accountId
        let entertainment5410Id = entertainment5410 |> Account.accountId

        // =============================================================================
        // Create fiscal periods
        // =============================================================================

        let! _ =
            [-4..4]
            |> List.map (fun x ->
                let date = x |> today.PlusMonths
                let monthF = date.Month.ToString("D2")
                let key = $"{date.Year}-{monthF}"
                FiscalPeriod.constructNewAndSaveToDb key envelope None)
            |> ListHelper.listOfResultsToResultsList

        // =============================================================================
        // Create journal entries (component-level, bypassing orchestrateCreation's
        // internal transaction which can't see auto-committed FPs in Npgsql 10.x)
        // =============================================================================
        
        let jeEnvelope = AuditEnvelope.create JournalEntryPostNew
        
        let! je1 =
            {
                header = { description = "Door Dash Bill's Pizza"; source = (Some "Checking Acct Statement"); entryDate = twoMonthsAgo; voidedAt = None }
                lines = [
                    { accountId = checking1110Id |> AccountId.value; amount = 114.31M; lineType = "Debit"; memo = None }
                    { accountId = food5350Id |> AccountId.value; amount = 114.31M; lineType = "Credit"; memo = None }
                ]
                externalReferences = []
                comments = []
            }
            |> orchestrateCreation jeEnvelope
        let je1Id = je1 |> header |> JournalEntryHeader.journalEntryHeaderId
        let! je2 =
            {
                header = { description = "Vons #3126"; source = (Some "Checking Acct Statement"); entryDate = twoMonthsAgo; voidedAt = None }
                lines = [
                    { accountId = checking1110Id |> AccountId.value; amount = 388.19M; lineType = "Debit"; memo = None }
                    { accountId = food5350Id |> AccountId.value; amount = 388.19M; lineType = "Credit"; memo = None }
                ]
                externalReferences = []
                comments = []
            }
            |> orchestrateCreation jeEnvelope 
        let je2Id = je2 |> header |> JournalEntryHeader.journalEntryHeaderId
        
        let voidComment = { secondaryJournalEntryId = None; commentText = "Dan hosed it, eh?" }
        let voidEnvelope = AuditEnvelope.create JournalEntryVoid
        let! _ = voidJournalEntryOrchestration voidEnvelope voidComment je2Id
        
        let! je3 =
            {
                header = { description = "Vons #3126"; source = (Some "Checking Acct Statement"); entryDate = twoMonthsAgo; voidedAt = None }
                lines = [
                    { accountId = checking1110Id |> AccountId.value; amount = 212.88M; lineType = "Debit"; memo = None }
                    { accountId = entertainment5410Id |> AccountId.value; amount = 212.88M; lineType = "Credit"; memo = None }
                ]
                externalReferences = []
                comments = []
            }
            |> orchestrateCreation jeEnvelope 
        let je3Id = je3 |> header |> JournalEntryHeader.journalEntryHeaderId
        
        let voidComment = { secondaryJournalEntryId = None; commentText = "He hosed this one too?" }
        let voidEnvelope = AuditEnvelope.create JournalEntryVoid
        let! _ = voidJournalEntryOrchestration voidEnvelope voidComment je3Id
        
        return $"""
         assets1000Id = {assets1000Id}
         liabilities2000Id = {liabilities2000Id}
         equity3000Id = {equity3000Id}
         revenue4000Id = {revenue4000Id}
         expenses5000Id = {expenses5000Id}
         checking1110Id = {checking1110Id}
         moneyMarket1270Id = {moneyMarket1270Id}
         food5350Id = {food5350Id}
         je1Id = {je1Id}
         je2Id = {je2Id}
        """
    }

