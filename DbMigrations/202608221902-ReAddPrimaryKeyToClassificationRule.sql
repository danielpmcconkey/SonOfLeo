/*
executed manually in dev 2026-08-22 18:50
executed manually in test 2026-08-22 19:04
executed manually in prod 2026-08-22 19:04
*/


alter table ingestion.classification_rule
    add CONSTRAINT classification_rule_pkey PRIMARY KEY (unique_id)