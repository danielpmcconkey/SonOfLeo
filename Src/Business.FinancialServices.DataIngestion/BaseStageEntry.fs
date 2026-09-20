module Business.FinancialServices.DataIngestion.BaseStageEntry

open System
open NodaTime
open App.Utility.AppError
open Business.FinancialServices.Money
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent

type BaseStageEntryGroupId = private BaseStageEntryGroupId of string

module BaseStageEntryGroupId =
    let maxLength = 36 // accommodates a uuid if needed
    let value (BaseStageEntryGroupId gid) = gid 
    let create (raw: string) : Result<BaseStageEntryGroupId, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(IngestionBaseStageEntryGroupIdIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionBaseStageEntryGroupIdTooLong(raw, maxLength))
        else
            Ok(BaseStageEntryGroupId trimmed)
            
type BaseStageRawRow = {
    baseStageEntryGroupId : BaseStageEntryGroupId
    entryDate : LocalDate
    description: JournalEntryDescription
    fiSource: JournalRefFinancialInstitution
    fiReference: JournalExternalReferenceText
    amount : Money
    entryType : JournalEntryLineType
    accountId: AccountId option
    memo: JournalEntryLineMemo option
}
