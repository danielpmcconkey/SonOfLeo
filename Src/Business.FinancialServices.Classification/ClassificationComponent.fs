module Business.FinancialServices.Classification.ClassificationComponent

open System
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow

type ClassificationRuleId = private ClassificationRuleId of Guid

module ClassificationRuleId =
    let create () : ClassificationRuleId = ClassificationRuleId(Guid.NewGuid())
    let fromGuid g = ClassificationRuleId g
    let value (ClassificationRuleId g) : Guid = g

type ClassificationRunId = private ClassificationRunId of Guid

module ClassificationRunId =
    let create () : ClassificationRunId = ClassificationRunId(Guid.NewGuid())
    let fromGuid g = ClassificationRunId g
    let value (ClassificationRunId g) : Guid = g

type ClassificationMatchId = private ClassificationMatchId of Guid

module ClassificationMatchId =
    let create () : ClassificationMatchId = ClassificationMatchId(Guid.NewGuid())
    let fromGuid g = ClassificationMatchId g
    let value (ClassificationMatchId g) : Guid = g

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
        | _ -> error (IngestionInvalidNumericSearchOperator str)
    
type MoneySearchPattern = {
        numericSearchOperator: NumericSearchOperator
        amount: Money.Money
    }

type ClassificationRuleName = private ClassificationRuleName of string

module ClassificationRuleName =
    let maxLength = 250
    let value (ClassificationRuleName reference) = reference 
    let create (raw: string) : Result<ClassificationRuleName, IAppError> =
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
    let create (raw: string) : Result<StringSearchPattern, IAppError> =
        // Note, every other string-to-type create function trims the inbound string. Here, we should not. We use
        // StringSearchPattern in a regex string comparison and white space is probably meaningful in that context.
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
        | _ -> error (IngestionInvalidClassificationGroupConnector str)

type MatchCandidate = {
        headerIdOfCandidate: StageEntryHeaderId
        lineIdOfCandidate: StageEntryLineId
        ingestionSource: JournalRefFinancialInstitution
        description: JournalEntryDescription
        amount: Money.Money
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

type ClassificationRun = {
        runId: ClassificationRunId
        results: ClassificationResult list
    }

type PaymentAgreementClaimCluster = {
    paymentAgreementId: CashFlowComponent.PaymentAgreementId
    claimants: ClassificationResult list
    // a tied claimant had no linkage written for it at all, so resolving it means adding the right link rather than
    // removing a wrong one. A cluster can hold both kinds of claimant at once.
    containsUnwrittenTies: bool
}

type PaymentAgreementDecisionOutcome =
    | Linked
    | ContestedAgreement
    | TiedClaimants
    | NoLineOnAgreementAccounts
    | ManyLinesOnAgreementAccount

module PaymentAgreementDecisionOutcome =
    let toString outcome =
        match outcome with
        | Linked -> "Linked"
        | ContestedAgreement -> "ContestedAgreement"
        | TiedClaimants -> "TiedClaimants"
        | NoLineOnAgreementAccounts -> "NoLineOnAgreementAccounts"
        | ManyLinesOnAgreementAccount -> "ManyLinesOnAgreementAccount"

type PaymentAgreementDecision = {
    stageEntryLineId: StageEntryLineId
    paymentAgreementId: CashFlowComponent.PaymentAgreementId option
    ruleIds: ClassificationRuleId list
    outcome: PaymentAgreementDecisionOutcome
}

type ClassificationClaimant = // what entity gets to "claim" the Staged Entry at match
    | Account of AccountId // used for classifying staged entities into their appropriate JE line accounts
    | PaymentAgreement of CashFlowComponent.PaymentAgreementId // used for classifying staged entities to identify invoice matches

type ClassificationClaimantType = // ClassificationClaimant without the ID, for asking which kind of rule you want
    | AccountClaimant
    | PaymentAgreementClaimant

module ClassificationClaimantType =

    let toString c =
        match c with
        | AccountClaimant -> "AccountClaimant"
        | PaymentAgreementClaimant -> "PaymentAgreementClaimant"

    let fromString str =
        match str with
        | "AccountClaimant" -> Ok AccountClaimant
        | "PaymentAgreementClaimant" -> Ok PaymentAgreementClaimant
        | _ -> Error (IngestionInvalidClassificationClaimantType str)
