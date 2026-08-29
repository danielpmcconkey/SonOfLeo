module ModelOrchestrator.CashFlowQueryBuilder

open Model.CashFlow

type TargetComposite =
    | Agreement
    | Invoice

let createSelect
    (target: TargetComposite)
    : string =
    match target with
    | Agreement -> MasterAgreement.masterAgreementSelectFields
    | Invoice -> Invoice.invoiceSelectFields

let ma_query = """
with payments_enriched as (
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
), distinct_agreements as (
    select distinct ma.unique_id
    from cashflow.master_agreement ma
    left join cashflow.payment_agreement pa on ma.unique_id = pa.master_agreement_id
    left join cashflow.instance ins on ma.unique_id = ins.master_agreement_id
    left join cashflow.invoice inv on ins.unique_id = inv.instance_id
    left join payments_enriched pmt on inv.unique_id = pmt.invoice_id
    where 
    1=1
    and ma.unique_id in ('40000000-0000-0000-0000-000000000001')
    and ma.agreement_name in ('Test Mortgage - 123 Main St')
    and ma.flow_direction = 'Outgo'
    and (ma.end_date is null or ma.end_date >= CURRENT_DATE) -- if active only
    and (
        pa.debit_account in (
            '10000000-0000-0000-0000-000000000001',
            '10000000-0000-0000-0000-000000000002',
            '10000000-0000-0000-0000-000000000003',
            '10000000-0000-0000-0000-000000000004')
        or pa.credit_account in (
            '10000000-0000-0000-0000-000000000001',
            '10000000-0000-0000-0000-000000000002',
            '10000000-0000-0000-0000-000000000003',
            '10000000-0000-0000-0000-000000000004'))
    --and pa.expected_amount = 800.00 -- AmountFilter.ExactAmount
    and (pa.expected_amount >= 300.00 and pa.expected_amount <= 1200.00) -- AmountFilter.AmountRange
    and (ins.instance_date >= '2026-07-01' and ins.instance_date <= '2026-07-31')
    and inv.external_invoice_id = '8675309'
    and (inv.invoice_date >= '2026-06-25' and inv.invoice_date <= '2026-06-25')
    and (inv.due_date >= '2026-07-01' and inv.due_date <= '2026-07-01')
    --and inv.amount = 800.00 -- AmountFilter.ExactAmount
    and (inv.amount >= 300.00 and inv.amount <= 1200.00) -- AmountFilter.AmountRange
    and inv.invoice_state = 'InvoiceReceived'
    and inv.payment_state = 'FullyPaid'
    and inv.posted_state = 'PostedToLedger'
    --and inv.blocker_state = 'NoFunds'
    and pmt.journal_entry_header_id = '60000000-0000-0000-0000-000000000001'
    and pmt.stage_entry_header_id = '50000000-0000-0000-0000-000000000001'
    --and pmt.amount = 800.00 -- AmountFilter.ExactAmount
    and (pmt.amount >= 300.00 and pmt.amount <= 1200.00) -- AmountFilter.AmountRange
    and (pmt.posted_to_ledger_date >= '2026-06-25' and pmt.posted_to_ledger_date <= '2026-07-03')
)
select
    ma.unique_id, ma.agreement_name, ma.flow_direction, ma.cadence, ma.cadence_week_day,
    ma.cadence_date_in_month, ma.cadence_week_in_month, ma.cadence_month, ma.counterparty,
    ma.start_date, ma.end_date, ma.memo, ma.created_at, ma.modified_at
from distinct_agreements d
join cashflow.master_agreement ma on d.unique_id = ma.unique_id
;
""" 
