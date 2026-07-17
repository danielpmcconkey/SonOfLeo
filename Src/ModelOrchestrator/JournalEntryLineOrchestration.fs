module ModelOrchestrator.JournalEntryLineOrchestration

    open Model
    open Model.Audit
    open Model.Ledger.Accounts
    open Model.Ledger.Accounts.AccountComponent
    open Model.Ledger.Journaling
    open Model.Ledger.Journaling.JournalEntryComponent
    open Utilities.AppError
    open Utilities.DAL
    open Utilities.ResultCE

    let confirmAmountIsPositive (m:Money) : Result<unit, AppError> =
        if m |> Money.amount <= 0M // REQ-JE-1.24
        then Error (JournalEntryLineNonPositiveAmount (m |> Money.amount))
        else Ok ()
    
    let confirmAccountExists (transaction: DbTransaction option)  (accountId: AccountId) : Result<unit, AppError> =
        match accountId |> Account.fetchById transaction with
        | Error (DalResultantRowsDidntMatchExpectation _ ) -> Error (JournalEntryLineAccountDoesntExist (accountId |> AccountId.value))
        | Error e -> Error e
        | Ok _ -> Ok ()

    let constructNewAndSaveToDb
            (journalEntryId: JournalEntryHeaderId)
            (accountId: AccountId)
            (amount: Money)
            (lineType: JournalEntryLineType)
            (memo: JournalEntryLineMemo option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine, AppError> =
        let journalEntryLineId = JournalEntryLineId.create ()
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        result {
            do! amount |> confirmAmountIsPositive
            do! accountId |> confirmAccountExists transaction //REQ-JE-1.22
            let line = JournalEntryLine.create journalEntryLineId journalEntryId accountId amount lineType memo createdAt modifiedAt
            let! () = line |> JournalEntryLine.insertNewToDb transaction
            return line }
    
    // todo find out why we have no edit functions on JE line
    