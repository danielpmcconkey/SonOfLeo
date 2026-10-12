module Business.FinancialServices.Classification.ClassificationComponent

open System
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.Classification.ClassificationError

/// Why a staged line cannot be removed or have its amount, line type or account changed.
type StageLineProtection =
    | LinkedToPaymentAgreement
    | ReferencedByPayment
    | RecordedInClassificationRun

module StageLineProtection =
    let toString protection =
        match protection with
        | LinkedToPaymentAgreement -> "it is linked to a payment agreement; remove the link first"
        | ReferencedByPayment -> "a Payment references it; remove the Payment first"
        | RecordedInClassificationRun -> "a classification run recorded it, and those records are permanent"

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
        match (str: string).Trim() with
        | "GreaterThan" -> Ok GreaterThan
        | "LessThan" -> Ok LessThan
        | "GreaterThanOrEqualTo" -> Ok GreaterThanOrEqualTo
        | "LessThanOrEqualTo" -> Ok LessThanOrEqualTo
        | "ExactlyEqual" -> Ok ExactlyEqual
        | _ -> error (ClassificationInvalidNumericSearchOperator str)
    
type MoneySearchPattern =
    private {
        numericSearchOperator: NumericSearchOperator
        amount: Money.Money
    }

module MoneySearchPattern =
    let numericSearchOperator (pattern: MoneySearchPattern) = pattern.numericSearchOperator
    let amount (pattern: MoneySearchPattern) = pattern.amount
    let create
        (numericSearchOperator: NumericSearchOperator)
        (amount: Money.Money)
        : MoneySearchPattern =
        { numericSearchOperator = numericSearchOperator
          amount = amount }

type ClassificationRuleName = private ClassificationRuleName of string

module ClassificationRuleName =
    let maxLength = 250
    let value (ClassificationRuleName reference) = reference 
    let create (raw: string) : Result<ClassificationRuleName, IAppError> =
        let trimmed = raw.Trim()
        if trimmed = String.Empty then
            Error(ClassificationRuleNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(ClassificationRuleNameTooLong(raw, maxLength))
        else
            Ok(ClassificationRuleName trimmed)

type RulePriority = private RulePriority of int

module RulePriority =
    let value (RulePriority i) = i
    let create (i: int) : RulePriority = RulePriority i
    let winner (first: RulePriority) (second: RulePriority) : RulePriority =
        if value first <= value second then first else second

type StringSearchPattern = private StringSearchPattern of string

module StringSearchPattern =
    let maxLength = 500
    let value (StringSearchPattern reference) = reference 
    let create (raw: string) : Result<StringSearchPattern, IAppError> =
        // Note, every other string-to-type create function trims the inbound string. Here, we should not. We use
        // StringSearchPattern in a regex string comparison and white space is probably meaningful in that context.
        if raw = String.Empty then
            Error(ClassificationSearchPatternIsEmpty raw)
        elif raw.Length > maxLength then
            Error(ClassificationSearchPatternTooLong(raw, maxLength))
        else
            // a pattern that won't compile would otherwise be accepted here and fail mid-run, aborting classification
            // for every entry
            try
                Text.RegularExpressions.Regex(raw) |> ignore
                Ok(StringSearchPattern raw)
            with :? ArgumentException as ex ->
                Error(ClassificationSearchPatternInvalidRegex(raw, ex.Message))

/// SourcePatternFilter is the text a rule search looks for, literally, within the rule's Source patterns. It is a
/// fragment of a pattern, so it is bounded by a pattern's length; unlike a pattern it is trimmed and need not compile.
type SourcePatternFilter = private SourcePatternFilter of string

module SourcePatternFilter =
    let maxLength = StringSearchPattern.maxLength
    let value (SourcePatternFilter filter) = filter
    let create (raw: string) : Result<SourcePatternFilter, IAppError> =
        let trimmed = raw.Trim()
        if trimmed = String.Empty then
            Error(ClassificationSourcePatternFilterIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(ClassificationSourcePatternFilterTooLong(raw, maxLength))
        else
            Ok(SourcePatternFilter trimmed)

    
type ClassificationGroupConnector =
    | And
    | Or

module ClassificationGroupConnector =
    
    let toString c =
        match c with
        | And -> "And"
        | Or -> "Or"
    
    let fromString str =
        match (str: string).Trim() with
        | "And" -> Ok And
        | "Or" -> Ok Or
        | _ -> error (ClassificationInvalidGroupConnector str)

type MatchCandidate =
    private {
        headerIdOfCandidate: StageEntryHeaderId
        lineIdOfCandidate: StageEntryLineId
        ingestionSource: JournalRefFinancialInstitution
        description: JournalEntryDescription
        amount: Money.Money
        lineType: JournalEntryLineType
        memo: JournalEntryLineMemo option
    }

module MatchCandidate =
    let headerIdOfCandidate (candidate: MatchCandidate) = candidate.headerIdOfCandidate
    let lineIdOfCandidate (candidate: MatchCandidate) = candidate.lineIdOfCandidate
    let ingestionSource (candidate: MatchCandidate) = candidate.ingestionSource
    let description (candidate: MatchCandidate) = candidate.description
    let amount (candidate: MatchCandidate) = candidate.amount
    let lineType (candidate: MatchCandidate) = candidate.lineType
    let memo (candidate: MatchCandidate) = candidate.memo
    let create
        (headerIdOfCandidate: StageEntryHeaderId)
        (lineIdOfCandidate: StageEntryLineId)
        (ingestionSource: JournalRefFinancialInstitution)
        (description: JournalEntryDescription)
        (amount: Money.Money)
        (lineType: JournalEntryLineType)
        (memo: JournalEntryLineMemo option)
        : MatchCandidate =
        { headerIdOfCandidate = headerIdOfCandidate
          lineIdOfCandidate = lineIdOfCandidate
          ingestionSource = ingestionSource
          description = description
          amount = amount
          lineType = lineType
          memo = memo }

type ClassificationClaimant = // what entity gets to "claim" the Staged Entry at match
    | Account of AccountId // used for classifying staged entities into their appropriate JE line accounts
    | PaymentAgreement of CashFlowComponent.PaymentAgreementId // used for classifying staged entities to identify invoice matches

type PrioritizedMatch =
    private {
        claimant: ClassificationClaimant
        ruleId: ClassificationRuleId
        priority: RulePriority
    }

module PrioritizedMatch =
    let claimant (prioritizedMatch: PrioritizedMatch) = prioritizedMatch.claimant
    let ruleId (prioritizedMatch: PrioritizedMatch) = prioritizedMatch.ruleId
    let priority (prioritizedMatch: PrioritizedMatch) = prioritizedMatch.priority
    let create
        (claimant: ClassificationClaimant)
        (ruleId: ClassificationRuleId)
        (priority: RulePriority)
        : PrioritizedMatch =
        { claimant = claimant
          ruleId = ruleId
          priority = priority }

type ClassifierOutcome =
    | NoMatch
    | OneMatch of PrioritizedMatch
    | ManyMatchesClearWinner of PrioritizedMatch * PrioritizedMatch list
    | ManyMatchesTied of PrioritizedMatch list 

type ClassificationResult =
    private {
        candidate: MatchCandidate
        outcome: ClassifierOutcome
    }

module ClassificationResult =
    let candidate (classificationResult: ClassificationResult) = classificationResult.candidate
    let outcome (classificationResult: ClassificationResult) = classificationResult.outcome
    let create
        (candidate: MatchCandidate)
        (outcome: ClassifierOutcome)
        : ClassificationResult =
        { candidate = candidate
          outcome = outcome }

type ClassificationRun =
    private {
        runId: ClassificationRunId
        results: ClassificationResult list
    }

module ClassificationRun =
    let runId (run: ClassificationRun) = run.runId
    let results (run: ClassificationRun) = run.results
    let create
        (runId: ClassificationRunId)
        (results: ClassificationResult list)
        : ClassificationRun =
        { runId = runId
          results = results }

type PaymentAgreementClaimCluster =
    private {
        paymentAgreementId: CashFlowComponent.PaymentAgreementId
        claimants: ClassificationResult list
        // a tied claimant had no linkage written for it at all, so resolving it means adding the right link rather than
        // removing a wrong one. A cluster can hold both kinds of claimant at once.
        containsUnwrittenTies: bool
    }

module PaymentAgreementClaimCluster =
    let paymentAgreementId (cluster: PaymentAgreementClaimCluster) = cluster.paymentAgreementId
    let claimants (cluster: PaymentAgreementClaimCluster) = cluster.claimants
    let containsUnwrittenTies (cluster: PaymentAgreementClaimCluster) = cluster.containsUnwrittenTies
    let create
        (paymentAgreementId: CashFlowComponent.PaymentAgreementId)
        (claimants: ClassificationResult list)
        (containsUnwrittenTies: bool)
        : PaymentAgreementClaimCluster =
        { paymentAgreementId = paymentAgreementId
          claimants = claimants
          containsUnwrittenTies = containsUnwrittenTies }

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

type PaymentAgreementDecision =
    private {
        stageEntryLineId: StageEntryLineId
        paymentAgreementId: CashFlowComponent.PaymentAgreementId option
        // the link the decision produced, when it produced one
        paymentAgreementLinkId: CashFlowComponent.PaymentAgreementLinkId option
        ruleIds: ClassificationRuleId list
        outcome: PaymentAgreementDecisionOutcome
    }

module PaymentAgreementDecision =
    let stageEntryLineId (decision: PaymentAgreementDecision) = decision.stageEntryLineId
    let paymentAgreementId (decision: PaymentAgreementDecision) = decision.paymentAgreementId
    let paymentAgreementLinkId (decision: PaymentAgreementDecision) = decision.paymentAgreementLinkId
    let ruleIds (decision: PaymentAgreementDecision) = decision.ruleIds
    let outcome (decision: PaymentAgreementDecision) = decision.outcome
    let create
        (stageEntryLineId: StageEntryLineId)
        (paymentAgreementId: CashFlowComponent.PaymentAgreementId option)
        (paymentAgreementLinkId: CashFlowComponent.PaymentAgreementLinkId option)
        (ruleIds: ClassificationRuleId list)
        (outcome: PaymentAgreementDecisionOutcome)
        : PaymentAgreementDecision =
        { stageEntryLineId = stageEntryLineId
          paymentAgreementId = paymentAgreementId
          paymentAgreementLinkId = paymentAgreementLinkId
          ruleIds = ruleIds
          outcome = outcome }

type ClassificationClaimantType = // ClassificationClaimant without the ID, for asking which kind of rule you want
    | AccountClaimant
    | PaymentAgreementClaimant

module ClassificationClaimantType =

    let toString c =
        match c with
        | AccountClaimant -> "AccountClaimant"
        | PaymentAgreementClaimant -> "PaymentAgreementClaimant"

    let fromString str =
        match (str: string).Trim() with
        | "AccountClaimant" -> Ok AccountClaimant
        | "PaymentAgreementClaimant" -> Ok PaymentAgreementClaimant
        | _ -> Error (ClassificationInvalidClaimantType str)
