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
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent

/// One Payment referencing a staged line, with the Invoice it pays and that Invoice's Instance (REQ-RPT-7.4).
type PrePostingPayment =
    { paymentAmount: Money.Money
      invoiceDate: LocalDate
      dueDate: LocalDate
      invoiceAmount: Money.Money
      paymentState: PaymentState
      instanceDate: LocalDate }

/// The Payment Agreement a staged line is linked to (REQ-RPT-7.4).
type PrePostingAgreement =
    { paymentAgreementName: PaymentAgreementName
      masterAgreementName: AgreementName }

type PrePostingLine =
    { stageEntryLineId: StageEntryLineId
      lineType: JournalEntryLineType
      amount: Money.Money
      memo: JournalEntryLineMemo option
      accountCode: AccountCode
      accountName: AccountName
      ruleName: ClassificationRuleName option
      agreement: PrePostingAgreement option
      payments: PrePostingPayment list }

type PrePostingEntry =
    { stageEntryHeaderId: StageEntryHeaderId
      entryDate: LocalDate
      description: JournalEntryDescription
      sourceName: JournalRefFinancialInstitution
      fiReference: JournalExternalReferenceText
      status: StagedEntryStatus
      lines: PrePostingLine list }

/// REQ-RPT-7.3. Only runs that matched a line leave a record, so "the most recent run that evaluated it" is the most
/// recent run that recorded an account rule against it. Payment agreement runs are not account classification and are
/// ignored. Within that run, the rule naming the line's current account is used; if more than one does, the highest
/// priority wins, then the rule name.
let private ruleNameForLine
    (matchesForLine: RuleMatch.RuleMatch list)
    (rulesById: Map<ClassificationRuleId, ClassificationRule.ClassificationRule>)
    (accountId: AccountId)
    : ClassificationRuleName option =
    let accountMatches =
        matchesForLine
        |> List.choose (fun m ->
            rulesById
            |> Map.tryFind (m |> RuleMatch.classificationRuleId)
            |> Option.bind (fun rule ->
                match rule |> ClassificationRule.classificationClaimant with
                | ClassificationClaimant.Account ruleAccount -> Some (m, rule, ruleAccount)
                | ClassificationClaimant.PaymentAgreement _ -> None))
    if accountMatches |> List.isEmpty then None else
    let latestRun =
        accountMatches
        |> List.maxBy (fun (m, _, _) -> m |> RuleMatch.createdAt)
        |> fun (m, _, _) -> m |> RuleMatch.runId
    accountMatches
    |> List.filter (fun (m, _, ruleAccount) -> m |> RuleMatch.runId = latestRun && ruleAccount = accountId)
    |> List.map (fun (_, rule, _) -> rule)
    |> List.sortBy (fun rule ->
        -(rule |> ClassificationRule.priority),
        rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value)
    |> List.tryHead
    |> Option.map ClassificationRule.classificationRuleName

let private ifAny fetch ids = if ids |> List.isEmpty then Ok [] else fetch ids

/// fetchPrePostingReview returns every postable staged entry (REQ-RPT-7.1), line by line (REQ-RPT-7.2 to 7.4), in
/// entry date, source name, fi_reference order (REQ-RPT-7.6). A line with no account fails it (REQ-RPT-7.5).
let fetchPrePostingReview (context: Context.Context) : Result<PrePostingEntry list, IAppError> =
    result {
        let! entries = StageEntryOrchestration.fetchAllForPosting context
        let allLines = entries |> List.collect StageEntryOrchestration.seLines
        // REQ-RPT-7.5: the first line with no account, in the order the report would show it, fails the review
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
        let! accounts = Account.fetchAll context false
        let accountsById = accounts |> List.map (fun a -> a |> Account.accountId, a) |> Map.ofList

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
        let! masterAgreements =
            paymentAgreements |> List.map PaymentAgreement.masterAgreementID |> List.distinct
            |> ifAny (MasterAgreement.fetchByMasterAgreementIdList context)
        let masterAgreementsById = masterAgreements |> List.map (fun m -> m |> MasterAgreement.agreementID, m) |> Map.ofList
        let agreementsByPaymentAgreementId =
            paymentAgreements
            |> List.map (fun pa ->
                pa |> PaymentAgreement.paymentAgreementId,
                { paymentAgreementName = pa |> PaymentAgreement.paymentAgreementName
                  masterAgreementName =
                    masterAgreementsById[pa |> PaymentAgreement.masterAgreementID] |> MasterAgreement.agreementName })
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
                | Staged lineId -> Some (lineId, p)
                | Posted _ -> None)
            |> List.groupBy fst
            |> List.map (fun (lineId, ps) ->
                lineId,
                ps
                |> List.map snd
                |> List.map (fun p ->
                    let invoice = invoicesById[p |> Payment.invoiceId]
                    let instance = instancesById[invoice |> Invoice.instanceId]
                    { paymentAmount = (p |> Payment.amount).money
                      invoiceDate = (invoice |> Invoice.invoiceDate).localDate
                      dueDate = (invoice |> Invoice.dueDate).localDate
                      invoiceAmount = (invoice |> Invoice.amount).money
                      paymentState = (invoice |> Invoice.invoiceLifeCycleState).paymentState
                      instanceDate = instance |> Instance.instanceDate })
                |> List.sortBy (fun p -> p.instanceDate, p.dueDate))
            |> Map.ofList

        let toLine (l: StageEntryLine.StageEntryLine) =
            let lineId = l |> StageEntryLine.stageEntryLineId
            // REQ-RPT-7.5 was checked above, so every line has an account here
            let accountId = l |> StageEntryLine.accountId |> Option.get
            let account = accountsById[accountId]
            { stageEntryLineId = lineId
              lineType = l |> StageEntryLine.lineType
              amount = l |> StageEntryLine.amount
              memo = l |> StageEntryLine.memo
              accountCode = account |> Account.code
              accountName = account |> Account.accountName
              ruleName =
                ruleNameForLine (matchesByLine |> Map.tryFind lineId |> Option.defaultValue []) rulesById accountId
              agreement = agreementByLine |> Map.tryFind lineId
              payments = paymentsByLine |> Map.tryFind lineId |> Option.defaultValue [] }

        let lineTypeOrder = function Debit -> 0 | Credit -> 1
        return
            entries
            |> List.map (fun se ->
                let h = se |> StageEntryOrchestration.stageEntryHeader
                { stageEntryHeaderId = h |> StageEntryHeader.stageEntryHeaderId
                  entryDate = h |> StageEntryHeader.entryDate
                  description = h |> StageEntryHeader.description
                  sourceName = h |> StageEntryHeader.ingestionSource |> IngestionSource.name
                  fiReference = h |> StageEntryHeader.fiReference
                  // fetchAllForPosting only returns entries with a current status
                  status = h |> StageEntryHeader.currentStatus |> Option.get
                  lines =
                    se
                    |> StageEntryOrchestration.seLines
                    |> List.sortBy (fun l -> l |> StageEntryLine.lineType |> lineTypeOrder)
                    |> List.map toLine })
            |> List.sortBy (fun e ->
                e.entryDate,
                e.sourceName |> JournalRefFinancialInstitution.value,
                e.fiReference |> JournalExternalReferenceText.value)
    }
