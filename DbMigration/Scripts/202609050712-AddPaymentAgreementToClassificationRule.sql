alter table ingestion.classification_rule
    alter column account_at_match drop not null;

alter table ingestion.classification_rule
    add payment_agreement_at_match uuid
        references cashflow.payment_agreement (unique_id)
            match simple on update no action on delete restrict;