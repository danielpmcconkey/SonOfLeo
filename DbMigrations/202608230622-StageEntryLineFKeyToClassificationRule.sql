/*
executed manually in dev 2026-08-23 06:28
executed manually in test 2026-08-23 06:28
executed manually in prod 2026-08-23 06:30
*/


alter table ingestion.staged_entry_line
    add CONSTRAINT staged_entry_line_classification_rule_fkey FOREIGN KEY 
    (classification_rule_id) references ingestion.classification_rule (unique_id)