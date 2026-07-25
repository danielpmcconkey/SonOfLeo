module DevDataStage.DataStage

open System
open Model
open Model.Ledger.Accounts
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open NodaTime
open Utilities.ResultHelper
open Model.Audit
open Model.Ledger.FiscalPeriods
open Utilities
open Utilities.DAL
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Utilities.AppError

/// run this any time you need to add new data to the dev database. This is
/// here purely for convenience and should not be reviewed as part of the
/// main code base

let createTestAccountFromPrimitives code name actType activeBegin activeEnd subtype parentId reference envelope  transaction : Result<(Account * AccountId), AppError> =
    result {
        let! account =
            AccountCreation.constructNewAndSaveToDb
                (code |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                (name |> AccountName.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                (actType |> AccountType.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                (AccountActivityPeriod.create activeBegin activeEnd |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                (subtype |> convertOptionToDesiredTypeWithFallibleConverter AccountSubtype.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                parentId
                (reference |> convertOptionToDesiredTypeWithFallibleConverter AccountExternalReference.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                envelope
                transaction
        return (account, account |> Account.accountId) }
let createTestJournalEntryFromPrimitives
            (description: string)
            (source: string option)
            (entryDate: LocalDate)
            (lines: (AccountId * decimal * string * string option) list)
            (references: (string * string) list)
            (comments: (JournalEntryHeaderId option * string) list)
            (auditEnvelope: AuditEnvelope)
            : Result<JournalEntry * JournalEntryHeaderId, AppError> =
    let convertLines (linesIn : (AccountId * decimal * string * string option) list) : Result<(AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list, AppError> =
        linesIn
        |> List.map(fun l ->
            let id, amountDec, lineTypeSt, memoSt = l 
            result {
                let! amount = amountDec |> Money.fromDecimal
                let! lineType = lineTypeSt |> JournalEntryLineType.fromString
                let! memo = memoSt |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
                return id, amount, lineType, memo
            })
        |> convertListOfResultsToResultsList
    let convertRefs (refsIn : (string * string) list) : Result<(JournalRefFinancialInstitution * JournalExternalReferenceText) list, AppError> =
        refsIn
        |> List.map(fun r ->
            let fiSt, refSt = r 
            result {
                let! fi = fiSt |> JournalRefFinancialInstitution.create
                let! ref = refSt |> JournalExternalReferenceText.create
                return fi, ref
            })
        |> convertListOfResultsToResultsList
    let convertComments (commentsIn : (JournalEntryHeaderId option * string) list) : Result<(JournalEntryHeaderId option * CommentText) list, AppError> =
        commentsIn
        |> List.map(fun c ->
            let id, textSt = c
            result {
                let! text = textSt |> CommentText.create
                return id, text
            })
        |> convertListOfResultsToResultsList
    result {
        let! description = description |> JournalEntryDescription.create
        let! source = source |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
        let! entryDate = entryDate |> EntryDate.create None
        let! linesConverted = lines |> convertLines
        let! refsConverted = references |> convertRefs
        let! commentsConverted = comments |> convertComments
        let! journalEntry =
            JournalEntry.constructNewAndSaveToDb
                description source entryDate linesConverted refsConverted commentsConverted auditEnvelope
        let headerId = journalEntry |> JournalEntry.header |> JournalEntryHeader.journalEntryHeaderId
        return (journalEntry, headerId) }

let stageData =
    let today = Calendar.today()
    let yesterday = today.PlusDays(-1)
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

        let! assets1000, assets1000Id = createTestAccountFromPrimitives "F-1000" "Assets" "Asset" lastYear None None None None envelope None
        let! liabilities2000, liabilities2000Id = createTestAccountFromPrimitives "F-2000" "Liabilities" "Liability" lastYear None None None None envelope None
        let! equity3000, equity3000Id = createTestAccountFromPrimitives "F-3000" "Equity" "Equity" lastYear None None None None envelope None
        let! revenue4000, revenue4000Id = createTestAccountFromPrimitives "F-4000" "Revenue" "Revenue" lastYear None None None None envelope None
        let! expenses5000, expenses5000Id = createTestAccountFromPrimitives "F-5000" "Expenses" "Expense" lastYear None None None None envelope None
        let! rothIra1250, rothIra1250Id = createTestAccountFromPrimitives "F-1250" "Roth IRA" "Asset" lastYear None (Some "Investment") (Some assets1000Id) None envelope None
        let! moneyMarket1270, moneyMarket1270Id = createTestAccountFromPrimitives "F-1270" "Money Market" "Asset" lastYear None (Some "Cash") (Some assets1000Id) None envelope None
        let! mortgage2210, mortgage2210Id = createTestAccountFromPrimitives "F-2210" "Mortgage Payable" "Liability" lastYear None (Some "LongTermLiability") (Some liabilities2000Id) None envelope None
        let! creditCard2220, creditCard2220Id = createTestAccountFromPrimitives "F-2220" "Credit Card" "Liability" lastYear None (Some "CurrentLiability") (Some liabilities2000Id) None envelope None
        let! retirement3030, retirement3030Id = createTestAccountFromPrimitives "F-3030" "Retirement Contributions" "Equity" lastYear None None (Some equity3000Id) None envelope None
        let! personalRevenue4290, personalRevenue4290Id = createTestAccountFromPrimitives "F-4290" "Personal Revenue" "Revenue" lastYear None (Some "OperatingRevenue") (Some revenue4000Id) None envelope None
        let! food5350, food5350Id = createTestAccountFromPrimitives "F-5350" "Food" "Expense" lastYear None (Some "OperatingExpense") (Some expenses5000Id) None envelope None
        let! entertainment5650, entertainment5650Id = createTestAccountFromPrimitives "F-5650" "Entertainment" "Expense" lastYear None (Some "OperatingExpense") (Some expenses5000Id ) None envelope None
        
        // create an account that will be closed after we add an entry to it
        let! _, closedBank1290Id = createTestAccountFromPrimitives "F-1290" "Closed Bank" "Asset" lastYear None (Some "Cash") (Some assets1000Id ) None envelope None
        // note: don't add it yet. only after it's been closed
        
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
        // note: don't add it to the FP list until after you've closed it

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

        let! _, jeToVoidId =
            createTestJournalEntryFromPrimitives "Fixture voided JE" None yesterday 
                [ (entertainment5650Id, 75.00M, "Debit", None)
                  (creditCard2220Id, 75.00M, "Credit", None) ]
                [  ]
                []
                jeEnvelope
        // note: don't add jeToVoid to the list because we later update it by voiding
        
        let! jeToNotVoid, _ = // this is here to ensure we have an account with both voided and not-voided JEs
            createTestJournalEntryFromPrimitives "Fixture voided JE" None yesterday 
                [ (entertainment5650Id, 86.04M, "Debit", None)
                  (creditCard2220Id, 86.04M, "Credit", None) ]
                [  ]
                []
                jeEnvelope

        let closedPeriodEntryDate = (closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)

        let! jeInClosedPeriod, jeInClosedPeriodId =
            createTestJournalEntryFromPrimitives "Fixture JE in closed period" None closedPeriodEntryDate 
                [ (mortgage2210Id, 25.00M, "Debit", None)
                  (food5350Id, 25.00M, "Credit", None) ]
                [  ]
                []
                jeEnvelope

        let! jeInClosedAccount, _ =
            createTestJournalEntryFromPrimitives "Journal entry in closed account" None closedPeriodEntryDate
                [ (closedBank1290Id, 71.38M, "Debit", None)
                  (food5350Id, 71.38M, "Credit", (Some "Grocery run")) ]
                []
                []
                jeEnvelope

        // need to offset the transaction so it has a zero balance
        let! jeInClosedAccount2, _ =
            createTestJournalEntryFromPrimitives "Journal entry in closed account" None (closedPeriodEntryDate.PlusWeeks(1))
                [ (closedBank1290Id, 71.38M, "Credit", None)
                  (food5350Id, 71.38M, "Debit", (Some "Grocery refund")) ]
                []
                []
                jeEnvelope

        let! jeWithLinesRefsAndComments, jeWithLinesRefsAndCommentsId =
            createTestJournalEntryFromPrimitives "Basic journal entry" None today 
                [ (mortgage2210Id, 100.00M, "Debit", None)
                  (food5350Id, 100.00M, "Credit", (Some "Grocery run")) ]
                [ ("TestBank", "TXN-001") ]
                [ (None, "Fixture comment for testing") ]
                jeEnvelope

        // =============================================================================
        // Close the fiscal period and account (after JE creation)
        // =============================================================================

        let! updatedFiscalPeriod = FiscalPeriod.closeFiscalPeriod closedFiscalPeriodId envelope None
        
        let! closedBank1290 = closedBank1290Id |> Account.fetchById None
        let! updatedClosedBank = closedBank1290 |> AccountDeactivation.deactivateAccount None envelope (Some twoMonthsAgo)

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
                [ ("TestBank", "F-SHARED-001") ]
                []
                jeEnvelope

        let! sharedRefJe2, sharedRefJe2Id =
            createTestJournalEntryFromPrimitives "Fixture shared-ref JE 2" (Some "Test") today 
                [ (mortgage2210Id, 20.00M, "Debit", None)
                  (food5350Id, 20.00M, "Credit", None) ]
                [ ("TestBank", "F-SHARED-001") ]
                []
                jeEnvelope
        
        return () }
