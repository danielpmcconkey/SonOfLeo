/*
executed manually in dev 2026-08-11 08:21
executed manually in test not yet 2026-08-11 08:24
executed manually in prod not yet 2026-08-11 08:24

drop schema ingestion cascade;
 
 */
alter table ingestion.classification_rule drop column source_id;

alter table ingestion.classification_rule
    add column code_at_match varchar (10) not null
        REFERENCES ledger.account (code)
            MATCH SIMPLE
            ON UPDATE NO ACTION
            ON DELETE RESTRICT ;

alter table ingestion.classification_rule
    add column priority int not null;


alter table ingestion.classification_rule
    add column rule_groups jsonb not null;

alter table ingestion.classification_rule
    add column is_active bool not null;

