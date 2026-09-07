-- cashflow.payment's transaction pointer moves from header level to line level on both sides. The derived amount
-- used to pick its line by flow direction and account, which relied on there being exactly one line per leg per
-- entry. Pointing at the line directly removes that assumption.

alter table cashflow.payment
    drop constraint payment_journal_entry_header_id_fkey;

alter table cashflow.payment
    drop constraint payment_stage_entry_header_id_fkey;

alter table cashflow.payment
    rename column journal_entry_header_id to journal_entry_line_id;

alter table cashflow.payment
    rename column stage_entry_header_id to stage_entry_line_id;

alter table cashflow.payment
    add constraint payment_journal_entry_line_id_fkey foreign key (journal_entry_line_id)
        references ledger.journal_entry_line (unique_id) match simple on update no action on delete restrict;

alter table cashflow.payment
    add constraint payment_stage_entry_line_id_fkey foreign key (stage_entry_line_id)
        references ingestion.staged_entry_line (unique_id) match simple on update no action on delete restrict;
