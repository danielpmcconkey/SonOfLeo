module Business.CrossDomainOrchestration.PrePostingReview

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent

type PrePostingLine =
    { stageEntryLineId: StageEntryLineId
      lineType: JournalEntryLineType
      amount: Money.Money
      memo: JournalEntryLineMemo option
      accountId: AccountId
      classificationRuleId: ClassificationRuleId option
      agreement: PrePostingAgreement option
      payments: PrePostingPayment list }

type PrePostingEntry =
    { stageEntryHeaderId: StageEntryHeaderId
      entryDate: LocalDate
      description: JournalEntryDescription
      ingestionSourceId: IngestionSourceId
      fiReference: JournalExternalReferenceText
      status: StagedEntryStatus
      lines: PrePostingLine list }

/// Only account-claimant matches with the line's current account count. Of the runs that recorded one, the latest
/// names the rule; within it the lowest priority value wins, then the rule name.
let private ruleIdForLine
    (matchesForLine: RuleMatch.RuleMatch list)
    (rulesById: Map<ClassificationRuleId, ClassificationRule.ClassificationRule>)
    (accountId: AccountId)
    : ClassificationRuleId option =
    let currentAccountMatches =
        matchesForLine
        |> List.choose (fun m ->
            rulesById
            |> Map.tryFind (m |> RuleMatch.classificationRuleId)
            |> Option.bind (fun rule ->
                match rule |> ClassificationRule.classificationClaimant with
                | ClassificationClaimant.Account ruleAccount when ruleAccount = accountId -> Some (m, rule)
                | ClassificationClaimant.Account _
                | ClassificationClaimant.PaymentAgreement _ -> None))
    if currentAccountMatches |> List.isEmpty then None else
    let latestRun =
        currentAccountMatches
        |> List.maxBy (fun (m, _) -> m |> RuleMatch.createdAt)
        |> fun (m, _) -> m |> RuleMatch.runId
    currentAccountMatches
    |> List.filter (fun (m, _) -> m |> RuleMatch.runId = latestRun)
    |> List.map snd
    |> List.sortBy (fun rule ->
        rule |> ClassificationRule.priority,
        rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value)
    |> List.tryHead
    |> Option.map ClassificationRule.classificationRuleId

let private ifAny fetch ids = if ids |> List.isEmpty then Ok [] else fetch ids

/// fetchPrePostingReview returns every postable staged entry, line by line, in
/// entry date, source name, fi_reference order. A line with no account fails it.
let fetchPrePostingReview (context: Context.Context) : Result<PrePostingEntry list, IAppError> =
    result {
        let! entries = StageEntryOrchestration.fetchAllForPosting context
        let allLines = entries |> List.collect StageEntryOrchestration.seLines
        // the first line with no account, in the order the report would show it, fails the review
        let! _ =
            entries
            |> List.sortBy (fun se ->
                let h = se |> StageEntryOrchestration.stageEntryHeader
                h |> StageEntryHeader.entryDate,
                h |> StageEntryHeader.ingestionSource |> IngestionSource.name |> JournalRefFinancialInstitution.value,
                h |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value)
            |> List.collect (fun se -> se |> StageEntryOrchestration.seLines)
            |> List.tryFind (fun l -> l |> StageEntryLine.accountId |> Option.isNone)
            |> function
                | None -> Ok ()
                | Some l ->
                    DataIngestionError.error (
                        DataIngestionError.IngestionPrePostingReviewLineHasNoAccount (
                            l |> StageEntryLine.stageEntryHeaderId |> StageEntryHeaderId.value,
                            l |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value))
        let lineIds = allLines |> List.map StageEntryLine.stageEntryLineId

        let! matches = RuleMatch.fetchByStageEntryLineIdList context lineIds
        let! rules =
            matches |> List.map RuleMatch.classificationRuleId |> List.distinct
            |> ifAny (ClassificationRule.fetchByIdList context)
        let rulesById = rules |> List.map (fun r -> r |> ClassificationRule.classificationRuleId, r) |> Map.ofList
        let matchesByLine = matches |> List.groupBy RuleMatch.stageEntryLineId |> Map.ofList

        let! links = lineIds |> ifAny (PaymentAgreementLink.fetchByStageEntryLineIdList context)
        let! paymentAgreements =
            links |> List.map PaymentAgreementLink.paymentAgreementId |> List.distinct
            |> ifAny (PaymentAgreement.fetchByPaymentAgreementIdList context)
        let agreementsByPaymentAgreementId =
            paymentAgreements
            |> List.map (fun pa ->
                pa |> PaymentAgreement.paymentAgreementId,
                { paymentAgreementId = pa |> PaymentAgreement.paymentAgreementId
                  masterAgreementId = pa |> PaymentAgreement.masterAgreementID })
            |> Map.ofList
        let agreementByLine =
            links
            |> List.map (fun l ->
                l |> PaymentAgreementLink.stageEntryLineId,
                agreementsByPaymentAgreementId[l |> PaymentAgreementLink.paymentAgreementId])
            |> Map.ofList

        let! payments = lineIds |> ifAny (Payment.fetchByStageEntryLineIdList context)
        let! invoices = payments |> List.map Payment.invoiceId |> List.distinct |> ifAny (Invoice.fetchByIdList context)
        let invoicesById = invoices |> List.map (fun i -> i |> Invoice.invoiceId, i) |> Map.ofList
        let! instances =
            invoices
            |> List.map Invoice.instanceId
            |> List.distinct
            |> List.map (Instance.fetchById context)
            |> convertListOfResultsToResultsList
        let instancesById = instances |> List.map (fun i -> i |> Instance.instanceId, i) |> Map.ofList
        let paymentsByLine =
            payments
            |> List.choose (fun p ->
                match p |> Payment.transactionPointer with
                | TransactionPointer.Staged lineId -> Some (lineId, p)
                | TransactionPointer.Posted _ -> None)
            |> List.groupBy fst
            |> List.map (fun (lineId, ps) ->
                lineId,
                ps
                |> List.map snd
                |> List.map (fun p ->
                    let invoice = invoicesById[p |> Payment.invoiceId]
                    let instance = instancesById[invoice |> Invoice.instanceId]
                    { paymentId = p |> Payment.paymentId
                      invoiceId = invoice |> Invoice.invoiceId
                      paymentAmount = p |> Payment.amount
                      invoiceDate = invoice |> Invoice.invoiceDate
                      dueDate = invoice |> Invoice.dueDate
                      invoiceAmount = invoice |> Invoice.amount
                      paymentState = ((invoice |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState)
                      instanceDate = instance |> Instance.instanceDate })
                |> List.sortBy (fun p -> p.instanceDate, p.dueDate))
            |> Map.ofList

        let toLine (l: StageEntryLine.StageEntryLine) =
            let lineId = l |> StageEntryLine.stageEntryLineId
            // a line with no account was refused above, so every line has an account here
            let accountId = l |> StageEntryLine.accountId |> Option.get
            { stageEntryLineId = lineId
              lineType = l |> StageEntryLine.lineType
              amount = l |> StageEntryLine.amount
              memo = l |> StageEntryLine.memo
              accountId = accountId
              classificationRuleId =
                ruleIdForLine (matchesByLine |> Map.tryFind lineId |> Option.defaultValue []) rulesById accountId
              agreement = agreementByLine |> Map.tryFind lineId
              payments = paymentsByLine |> Map.tryFind lineId |> Option.defaultValue [] }

        let lineTypeOrder = function JournalEntryLineType.Debit -> 0 | JournalEntryLineType.Credit -> 1
        return
            entries
            |> List.map (fun se ->
                let h = se |> StageEntryOrchestration.stageEntryHeader
                h |> StageEntryHeader.ingestionSource |> IngestionSource.name,
                { stageEntryHeaderId = h |> StageEntryHeader.stageEntryHeaderId
                  entryDate = h |> StageEntryHeader.entryDate
                  description = h |> StageEntryHeader.description
                  ingestionSourceId = h |> StageEntryHeader.ingestionSource |> IngestionSource.ingestionSourceId
                  fiReference = h |> StageEntryHeader.fiReference
                  // fetchAllForPosting only returns entries with a current status
                  status = h |> StageEntryHeader.currentStatus |> Option.get
                  lines =
                    se
                    |> StageEntryOrchestration.seLines
                    |> List.sortBy (fun l -> l |> StageEntryLine.lineType |> lineTypeOrder)
                    |> List.map toLine })
            |> List.sortBy (fun (sourceName, e) ->
                e.entryDate,
                sourceName |> JournalRefFinancialInstitution.value,
                e.fiReference |> JournalExternalReferenceText.value)
            |> List.map snd
    }
