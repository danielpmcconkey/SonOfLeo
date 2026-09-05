
alter table ingestion.staged_entry_line
    add payment_agreement_id uuid
        references cashflow.payment_agreement (unique_id)
            match simple on update no action on delete restrict;