module ModelOrchestrator.CashFlowCompositeFetcher

open System
open DataAccessLayer.ExecuteReader
open Model.CashFlow.CashFlowComponent
open Model.CashFlow.Invoice
open Model.CashFlow.MasterAgreement
open Model.DataIngestion
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.FetchFilters
open DataAccessLayer.QueryParameters
open Utilities
open Utilities.AppError
open Utilities.ResultHelper

type TargetComposite =
    | Agreement
    | Invoice

let paymentsEnrichedCte = """payments_enriched as (
    select 
        pmt.unique_id, pmt.invoice_id, pmt.journal_entry_header_id, pmt.stage_entry_header_id, 
        case when je.unique_id is not null then jel.amount else sel.amount end as amount,
        pmt.posted_to_fi_date, je.entry_date as posted_to_ledger_date, pmt.memo, pmt.created_at, 
        pmt.modified_at
    from cashflow.payment pmt
    left join cashflow.invoice inv on pmt.invoice_id = inv.unique_id
    left join cashflow.payment_agreement pa on inv.payment_agreement_id = pa.unique_id
    left join cashflow.master_agreement ma on pa.master_agreement_id = ma.unique_id
    left join ledger.journal_entry je on pmt.journal_entry_header_id = je.unique_id    
    left join ledger.journal_entry_line jel
        on je.unique_id = jel.journal_entry_id
        and (case 
                when ma.flow_direction = 'Income' then jel.account_id = pa.credit_account and jel.line_type = 'Credit'
                when ma.flow_direction = 'Outgo' then jel.account_id = pa.debit_account and jel.line_type = 'Debit'
            end)
    left join ingestion.staged_entry se on pmt.stage_entry_header_id = se.unique_id
    left join ingestion.staged_entry_line sel 
        on se.unique_id = sel.entry_id
        and (case 
                when ma.flow_direction = 'Income' then sel.account_id = pa.credit_account and sel.line_type = 'Credit'
                when ma.flow_direction = 'Outgo' then sel.account_id = pa.debit_account and sel.line_type = 'Debit'
            end)
)"""

let agreementsSelectAndJoinInsideDistinct = """
    select distinct ma.unique_id
    from cashflow.master_agreement ma
    left join cashflow.payment_agreement pa on ma.unique_id = pa.master_agreement_id
    left join cashflow.instance ins on ma.unique_id = ins.master_agreement_id
    left join cashflow.invoice inv on ins.unique_id = inv.instance_id
    left join payments_enriched pmt on inv.unique_id = pmt.invoice_id"""

let invoicesSelectAndJoinInsideDistinct = """
    select distinct inv.unique_id
    from cashflow.invoice inv
    left join cashflow.instance ins on inv.instance_id = ins.unique_id
    left join cashflow.master_agreement ma on ins.master_agreement_id = ma.unique_id    
    left join cashflow.payment_agreement pa on inv.payment_agreement_id = pa.unique_id    
    left join payments_enriched pmt on inv.unique_id = pmt.invoice_id"""

let createPredicateAndParameters
    (context: Context.Context)
    (filter: AgreementFilter)
    : Result<string * QueryParameter list, AppError> = result {
    let agreementPredicate, agreementParameters =
        filter.agreementIds
        |> createIdPredicateAndParameters<MasterAgreementId> MasterAgreementId.value "agreement_id" ["ma.unique_id"]
    let namePredicate, nameParameters =
        filter.agreementNames
        |> createAgreementNamesPredicateAndParameters
    let directionPredicate, directionParameters =
        filter.direction
        |> createBasicPredicateAndParameters
               (fun x -> CharString(x |> FlowDirection.toString)) "flow_direction" "ma.flow_direction"    
    let activeAgreementPredicate =
        if filter.activeAgreementsOnly then Some "(ma.end_date is null or ma.end_date >= @today)" else None
    let activeAgreementParameters = 
        if filter.activeAgreementsOnly then [{ name = "@today"; value = DbLocalDate (Calendar.today()) }] else []
    let accountPredicate, accountParameters =
        filter.accountIds
        |> createIdPredicateAndParameters<AccountId> AccountId.value "account_id" ["pa.debit_account"; "pa.credit_account"]
    let paExpectedPredicate, paExpectedParameters =
        filter.paymentAgreementExpectedAmount
        |> createAmountPredicateAndParameters "pa_expected_amount" "pa.expected_amount"
    let! instanceTemporalPredicate, instanceTemporalParameters =
        filter.instanceTemporalFilter
        |> createTemporalPredicateAndParameters context "ins_instance_date" "ins.instance_date"
    let externalInvoiceIdPredicate, externalInvoiceIdParameters =
        filter.externalInvoiceId
        |> createBasicPredicateAndParameters
               (fun x -> CharString(x |> ExternalInvoiceId.value)) "inv_external_invoice_id" "inv.external_invoice_id"
    let! invoiceDateTemporalPredicate, invoiceDateTemporalParameters =
        filter.invoiceDateTemporalFilter
        |> createTemporalPredicateAndParameters context "inv_invoice_date" "inv.invoice_date"
    let! invoiceDueTemporalPredicate, invoiceDueTemporalParameters =
        filter.invoiceDueTemporalFilter
        |> createTemporalPredicateAndParameters context "inv_due_date" "inv.due_date"
    let invoiceAmountPredicate, invoiceAmountParameters =
        filter.invoiceAmount
        |> createAmountPredicateAndParameters "inv_amount" "inv.amount"
    let invoiceStatePredicate, invoiceStateParameters =
        filter.invoiceState
        |> createBasicPredicateAndParameters
               (fun x -> CharString(x |> InvoiceState.toString)) "inv_invoice_state" "inv.invoice_state"
    let invoicePaymentStatePredicate, invoicePaymentStateParameters =
        filter.invoicePaymentState
        |> createBasicPredicateAndParameters
               (fun x -> CharString(x |> PaymentState.toString)) "inv_payment_state" "inv.payment_state"
    let invoicePostedStatePredicate, invoicePostedStateParameters =
        filter.invoicePostedState
        |> createBasicPredicateAndParameters
               (fun x -> CharString(x |> PostedState.toString)) "inv_posted_state" "inv.posted_state"
    let invoiceBlockerPredicate, invoiceBlockerParameters =
        filter.invoiceBlocker
        |> createStringLikePredicateAndParameters Blocker.toString "inv_blocker_state" "inv.blocker_state"
    let journalEntryHeaderIdPredicate, journalEntryHeaderIdParameters =
        filter.journalEntryHeaderId
        |> createBasicPredicateAndParameters (fun x ->
            UniqueId(x |> JournalEntryHeaderId.value)) "pmt_journal_entry_header_id" "pmt.journal_entry_header_id"
    let stageEntryHeaderIdPredicate, stageEntryHeaderIdParameters =
        filter.stageEntryHeaderId
        |> createBasicPredicateAndParameters (fun x ->
            UniqueId(x |> StageEntryHeaderId.value)) "pmt_stage_entry_header_id" "pmt.stage_entry_header_id"
    let paymentAmountPredicate, paymentAmountParameters =
        filter.paymentAmount
        |> createAmountPredicateAndParameters "pmt_amount" "pmt.amount"
    let! paymentPostedToLedgerTemporalFilterPredicate, paymentPostedToLedgerTemporalFilterParameters =
        filter.paymentPostedToLedgerTemporalFilter
        |> createTemporalPredicateAndParameters context "pmt_posted_to_ledger_date" "pmt.posted_to_ledger_date"
    let allPredicates =
        [
            agreementPredicate
            namePredicate
            directionPredicate
            activeAgreementPredicate
            accountPredicate
            paExpectedPredicate
            instanceTemporalPredicate
            externalInvoiceIdPredicate
            invoiceDateTemporalPredicate
            invoiceDueTemporalPredicate
            invoiceAmountPredicate
            invoiceStatePredicate
            invoicePaymentStatePredicate
            invoicePostedStatePredicate
            invoiceBlockerPredicate
            journalEntryHeaderIdPredicate
            stageEntryHeaderIdPredicate
            paymentAmountPredicate
            paymentPostedToLedgerTemporalFilterPredicate
        ]
        |> List.choose id
        |> String.concat $"{Environment.NewLine}and "
    let allParameters =
         agreementParameters @ nameParameters @ directionParameters @ activeAgreementParameters @ accountParameters 
         @ paExpectedParameters @ instanceTemporalParameters @ externalInvoiceIdParameters
         @ invoiceDateTemporalParameters @ invoiceDueTemporalParameters @ invoiceAmountParameters
         @ invoiceStateParameters @ invoicePaymentStateParameters @ invoicePostedStateParameters
         @ invoiceBlockerParameters @ journalEntryHeaderIdParameters @ stageEntryHeaderIdParameters
         @ paymentAmountParameters @ paymentPostedToLedgerTemporalFilterParameters
    return allPredicates, allParameters
    }

let distinctCte targetComposite predicates =
    let name, selectAndJoin =
        match targetComposite with
        | Agreement -> "distinct_agreements", agreementsSelectAndJoinInsideDistinct
        | Invoice -> "distinct_invoices", invoicesSelectAndJoinInsideDistinct
    $"""{name} as (
    {selectAndJoin}
    where {predicates} 
    )"""

let fetchCompositeFiltered
    (context: Context.Context)
    (fetchFunc:
        Context.Context -> string list option -> string -> string list option -> string option ->                          
        int option -> string option -> string option -> QueryParameter list -> AcceptableExpectedRows ->               
        Result<'T list, AppError>)
    (target: TargetComposite)
    (filter: AgreementFilter)
    : Result<'T list, AppError> =
    result {
        let! predicates, parameters = filter |> createPredicateAndParameters context
        let distinct = distinctCte target predicates
        let cteList =[paymentsEnrichedCte; distinct] |> Some
        let select = match target with | Agreement -> masterAgreementSelectFields | Invoice -> invoiceSelectFields
        let joinList =
            match target with
            | Agreement -> ["inner join distinct_agreements d on d.unique_id = ma.unique_id"]
            | Invoice -> ["inner join distinct_invoices d on d.unique_id = inv.unique_id"]
            |> Some
        let predicate = None
        let limit = None
        let groupBy = None
        let orderBy = None
        let expectedRows = AnyQuantityIsAcceptable
        return! fetchFunc context cteList select joinList predicate limit groupBy orderBy parameters expectedRows
    }
