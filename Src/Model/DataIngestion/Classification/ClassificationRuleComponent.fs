module Model.DataIngestion.Classification.ClassificationRuleComponent

open System
open Model
open Model.CashFlow
open Model.Ledger.AccountComponent
open Model.Ledger.JournalEntryComponent
open Utilities.AppError
open Model.DataIngestion.StageEntryComponent

type ClassificationRuleId = private ClassificationRuleId of Guid

module ClassificationRuleId =
    let create () : ClassificationRuleId = ClassificationRuleId(Guid.NewGuid())
    let fromGuid g = ClassificationRuleId g
    let value (ClassificationRuleId g) : Guid = g

type NumericSearchOperator =
    | GreaterThan
    | LessThan
    | GreaterThanOrEqualTo
    | LessThanOrEqualTo
    | ExactlyEqual

module NumericSearchOperator =
    let toString o =
        match o with
        | GreaterThan -> "GreaterThan"
        | LessThan -> "LessThan"
        | GreaterThanOrEqualTo -> "GreaterThanOrEqualTo"
        | LessThanOrEqualTo -> "LessThanOrEqualTo"
        | ExactlyEqual -> "ExactlyEqual"
        
    let fromString str =
        match str with
        | "GreaterThan" -> Ok GreaterThan
        | "LessThan" -> Ok LessThan
        | "GreaterThanOrEqualTo" -> Ok GreaterThanOrEqualTo
        | "LessThanOrEqualTo" -> Ok LessThanOrEqualTo
        | "ExactlyEqual" -> Ok ExactlyEqual
        | _ -> Error (IngestionInvalidNumericSearchOperator str)
    
type MoneySearchPattern = {
        numericSearchOperator: NumericSearchOperator
        amount: Money
    }

type ClassificationRuleName = private ClassificationRuleName of string

module ClassificationRuleName =
    let maxLength = 250
    let value (ClassificationRuleName reference) = reference 
    let create (raw: string) : Result<ClassificationRuleName, AppError> =
        let trimmed = raw.Trim()
        if trimmed = String.Empty then
            Error(IngestionClassificationRuleNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionClassificationRuleNameTooLong(raw, maxLength))
        else
            Ok(ClassificationRuleName trimmed)

type StringSearchPattern = private StringSearchPattern of string

module StringSearchPattern =
    let maxLength = 500
    let value (StringSearchPattern reference) = reference 
    let create (raw: string) : Result<StringSearchPattern, AppError> =
        (*
         Note, every other string-to-type create function trims the inbound string. Here, we should not. We use
         StringSearchPattern in a regex string comparison and white space is probably meaningful in that context. 
        *)
        if raw = String.Empty then
            Error(IngestionSearchPatternIsEmpty raw)
        elif raw.Length > maxLength then
            Error(IngestionSearchPatternTooLong(raw, maxLength))
        else
            Ok(StringSearchPattern raw)

    
type ClassificationGroupConnector =
    | And
    | Or

module ClassificationGroupConnector =
    
    let toString c =
        match c with
        | And -> "And"
        | Or -> "Or"
    
    let fromString str =
        match str with
        | "And" -> Ok And
        | "Or" -> Ok Or
        | _ -> Error (IngestionInvalidClassificationGroupConnector str)

type MatchCandidate = {
        headerIdOfCandidate: StageEntryHeaderId
        lineIdOfCandidate: StageEntryLineId
        ingestionSource: JournalRefFinancialInstitution
        description: JournalEntryDescription
        amount: Money
        lineType: JournalEntryLineType
        memo: JournalEntryLineMemo option
}

type PrioritizedMatch = {
    accountId: AccountId option
    paymentAgreementId: CashFlowComponent.PaymentAgreementId option
    ruleId: ClassificationRuleId
    priority: int
}

type ClassifierOutcome =
    | NoMatch
    | OneMatch of PrioritizedMatch
    | ManyMatchesClearWinner of PrioritizedMatch * PrioritizedMatch list
    | ManyMatchesTied of PrioritizedMatch list 

type ClassificationResult = {
        candidate: MatchCandidate
        outcome: ClassifierOutcome
    }

type ClassificationClaimant = // what entity gets to "claim" the Staged Entry at match
    | Account of AccountId // used for classifying staged entities into their appropriate JE line accounts
    | PaymentAgreement of CashFlowComponent.PaymentAgreementId // used for classifying staged entities to identify invoice matches
