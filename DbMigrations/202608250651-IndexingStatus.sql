/*
executed manually in dev 2026-08-25 06:52
executed manually in test 2026-08-25 06:52
executed manually in prod 2026-08-25 06:52
*/


create index ix_staged_entry_audit_entry_id_modified_at
    on ingestion.staged_entry_audit (entry_id, modified_at desc);
