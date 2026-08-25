-- Table: ingestion.source


CREATE TABLE IF NOT EXISTS ingestion.source
(
    unique_id uuid NOT NULL,
    source_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT source_pkey PRIMARY KEY (unique_id)
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.source
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ingestion.source FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.source TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.source TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ingestion.source TO sonofleo_migrator;
                              
-- Table: ingestion.staged_entry

CREATE TABLE IF NOT EXISTS ingestion.staged_entry
(
    unique_id uuid NOT NULL,
    entry_date date NOT NULL,
    description character varying(1000) COLLATE pg_catalog."default" NOT NULL,
    source_id uuid NOT NULL,
    fi_reference character varying(100) COLLATE pg_catalog."default" NOT NULL,
    source_file character varying(150) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT staged_entry_pkey PRIMARY KEY (unique_id),
    CONSTRAINT staged_entry_source_id_fkey FOREIGN KEY (source_id)
    REFERENCES ingestion.source (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.staged_entry
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ingestion.staged_entry FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.staged_entry TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.staged_entry TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ingestion.staged_entry TO sonofleo_migrator;

-- Table: ingestion.classification_rule

CREATE TABLE IF NOT EXISTS ingestion.classification_rule
(
    unique_id uuid NOT NULL,
    rule_name character varying(250) COLLATE pg_catalog."default" NOT NULL,
    account_at_match uuid NOT NULL,
    priority integer NOT NULL,
    rule_groups jsonb NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT classification_rule_pkey PRIMARY KEY (unique_id),
    CONSTRAINT rule_name_unique UNIQUE (rule_name),
    CONSTRAINT classification_rule_account_at_match_fkey FOREIGN KEY (account_at_match)
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.classification_rule
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ingestion.classification_rule FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.classification_rule TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.classification_rule TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ingestion.classification_rule TO sonofleo_migrator;

-- Table: ingestion.staged_entry_line

CREATE TABLE IF NOT EXISTS ingestion.staged_entry_line
(
    unique_id uuid NOT NULL,
    entry_id uuid NOT NULL,
    amount numeric(12,2) NOT NULL,
    line_type character varying(6) COLLATE pg_catalog."default" NOT NULL,
    account_id uuid,
    memo character varying(1000) COLLATE pg_catalog."default",
    classification_rule_id uuid,
    CONSTRAINT staged_entry_line_pkey PRIMARY KEY (unique_id),
    CONSTRAINT staged_entry_line_account_id_fkey FOREIGN KEY (account_id)
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    CONSTRAINT staged_entry_line_classification_rule_fkey FOREIGN KEY (classification_rule_id)
    REFERENCES ingestion.classification_rule (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE NO ACTION,
    CONSTRAINT staged_entry_line_entry_id_fkey FOREIGN KEY (entry_id)
    REFERENCES ingestion.staged_entry (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.staged_entry_line
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ingestion.staged_entry_line FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.staged_entry_line TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.staged_entry_line TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ingestion.staged_entry_line TO sonofleo_migrator;

-- Table: ingestion.staged_entry_audit

-- DROP TABLE IF EXISTS ingestion.staged_entry_audit;

CREATE TABLE IF NOT EXISTS ingestion.staged_entry_audit
(
    unique_id uuid NOT NULL,
    entry_id uuid NOT NULL,
    from_status character varying(25) COLLATE pg_catalog."default",
    to_status character varying(25) COLLATE pg_catalog."default" NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              change_mechanism character varying(25) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT staged_entry_audit_pkey PRIMARY KEY (unique_id),
    CONSTRAINT staged_entry_audit_entry_id_fkey FOREIGN KEY (entry_id)
    REFERENCES ingestion.staged_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ingestion.staged_entry_audit
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ingestion.staged_entry_audit FROM leobloom_hobson;

GRANT SELECT ON TABLE ingestion.staged_entry_audit TO leobloom_hobson;

GRANT ALL ON TABLE ingestion.staged_entry_audit TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ingestion.staged_entry_audit TO sonofleo_migrator;

-- Index: ix_staged_entry_audit_entry_id_modified_at

CREATE INDEX IF NOT EXISTS ix_staged_entry_audit_entry_id_modified_at
    ON ingestion.staged_entry_audit USING btree
    (entry_id ASC NULLS LAST, modified_at DESC NULLS FIRST)
    TABLESPACE pg_default;

