/*
executed manually in dev 2026-08-22 09:21
executed manually in test not yet 2026-08-22 09:23
executed manually in prod not yet 2026-08-22 09:23
*/

DROP TABLE IF EXISTS ingestion.classification_rule cascade;

CREATE TABLE IF NOT EXISTS ingestion.classification_rule
(
    unique_id uuid NOT NULL,
    rule_name character varying(250) COLLATE pg_catalog."default" NOT NULL,
    account_at_match uuid not null
    REFERENCES ledger.account (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    priority integer NOT NULL,
    rule_groups jsonb NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
        )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.classification_rule
    OWNER to sonofleo_dev;

REVOKE ALL ON TABLE ingestion.classification_rule FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.classification_rule TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.classification_rule TO sonofleo_dev;

