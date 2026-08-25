/*
executed manually in dev 2026-08-25 06:30
executed manually in test 2026-08-25 06:34
executed manually in prod 2026-08-25 06:34
*/


alter table ingestion.classification_rule
    add CONSTRAINT rule_name_unique UNIQUE (rule_name)