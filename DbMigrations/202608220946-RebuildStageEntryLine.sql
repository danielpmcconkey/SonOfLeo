/*
executed manually in dev 2026-08-22 09:47
executed manually in test not yet 2026-08-22 09:48
executed manually in prod not yet 2026-08-22 09:48
*/
-- Table: ingestion.staged_entry_line

DROP TABLE IF EXISTS ingestion.staged_entry_line cascade;

CREATE TABLE IF NOT EXISTS ingestion.staged_entry_line
(
    unique_id uuid NOT NULL,
    entry_id uuid NOT NULL,
    amount numeric(12,2) NOT NULL,
    line_type character varying(6) COLLATE pg_catalog."default" NOT NULL,
    account_id uuid -- nullable
    REFERENCES ledger.account (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    memo character varying(1000) COLLATE pg_catalog."default",
    classification_rule_id uuid,
    CONSTRAINT staged_entry_line_pkey PRIMARY KEY (unique_id),
    CONSTRAINT staged_entry_line_entry_id_fkey FOREIGN KEY (entry_id)
    REFERENCES ingestion.staged_entry (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.staged_entry_line
    OWNER to sonofleo_dev;

REVOKE ALL ON TABLE ingestion.staged_entry_line FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.staged_entry_line TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.staged_entry_line TO sonofleo_dev;