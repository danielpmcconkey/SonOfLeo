
alter table ingestion.staged_entry_line
    rename classification_rule_id to account_classification_rule_id;

alter table ingestion.staged_entry_line
    add payment_classification_rule_id uuid
        references ingestion.classification_rule (unique_id)
            match simple on update no action on delete restrict;