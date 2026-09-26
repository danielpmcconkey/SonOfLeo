module Business.FinancialServices.Ledger.LedgerAuditableAction

open App.Operation.IAuditableAction

type LedgerAuditableAction = 
    | AccountCreate
    | AccountUpdateName
    | AccountUpdateExtReference
    | AccountDeactivate
    | FiscalPeriodCreate
    | FiscalPeriodClose
    | FiscalPeriodReopen
    | JournalEntryPostNew
    | JournalEntryVoid
    | JournalEntryUpdateExternalReference
    | JournalEntryAddExternalReference
    | JournalEntryAddComment
    | JournalEntryUpdateComment
    
    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this

