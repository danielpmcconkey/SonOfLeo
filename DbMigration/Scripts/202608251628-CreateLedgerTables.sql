CREATE TABLE IF NOT EXISTS ledger.account
(
    unique_id uuid NOT NULL,
    code character varying(10) COLLATE pg_catalog."default" NOT NULL,
    account_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    account_type character varying(20) COLLATE pg_catalog."default" NOT NULL,
    active_begin date NOT NULL,
    active_end date,
    account_subtype character varying(25) COLLATE pg_catalog."default",
    parent_id uuid,
    external_ref character varying(50) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT account_pkey PRIMARY KEY (unique_id),
    CONSTRAINT account_code_key UNIQUE (code),
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.account
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ledger.account FROM leobloom_hobson;

GRANT SELECT ON TABLE ledger.account TO leobloom_hobson;

GRANT ALL ON TABLE ledger.account TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ledger.account TO sonofleo_migrator;

CREATE TABLE IF NOT EXISTS ledger.fiscal_period
(
    unique_id uuid NOT NULL,
    period_key character varying(7) COLLATE pg_catalog."default" NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    is_open boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT fiscal_period_pkey PRIMARY KEY (unique_id),
    CONSTRAINT fiscal_period_period_key_unq UNIQUE (period_key)
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.fiscal_period
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ledger.fiscal_period FROM leobloom_hobson;

GRANT SELECT ON TABLE ledger.fiscal_period TO leobloom_hobson;

GRANT ALL ON TABLE ledger.fiscal_period TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ledger.fiscal_period TO sonofleo_migrator;
                              
-- Table: ledger.journal_entry

CREATE TABLE IF NOT EXISTS ledger.journal_entry
(
    unique_id uuid NOT NULL,
    description character varying(1000) COLLATE pg_catalog."default" NOT NULL,
    je_source character varying(50) COLLATE pg_catalog."default",
    entry_date date NOT NULL,
    fiscal_period_id uuid NOT NULL,
    voided_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT journal_entry_pkey PRIMARY KEY (unique_id),
    CONSTRAINT journal_entry_fiscal_period_id_fkey FOREIGN KEY (fiscal_period_id)
    REFERENCES ledger.fiscal_period (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.journal_entry
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ledger.journal_entry FROM leobloom_hobson;

GRANT SELECT ON TABLE ledger.journal_entry TO leobloom_hobson;

GRANT ALL ON TABLE ledger.journal_entry TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ledger.journal_entry TO sonofleo_migrator;

-- Table: ledger.journal_entry_comment

CREATE TABLE IF NOT EXISTS ledger.journal_entry_comment
(
    unique_id uuid NOT NULL,
    journal_primary_entry_id uuid NOT NULL,
    journal_secondary_entry_id uuid,
    comment_text character varying(2000) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT journal_entry_comment_pkey PRIMARY KEY (unique_id),
    CONSTRAINT journal_entry_comment_journal_primary_entry_id_fkey FOREIGN KEY (journal_primary_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT journal_entry_comment_journal_secondary_entry_id_fkey FOREIGN KEY (journal_secondary_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.journal_entry_comment
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ledger.journal_entry_comment FROM leobloom_hobson;

GRANT SELECT ON TABLE ledger.journal_entry_comment TO leobloom_hobson;

GRANT ALL ON TABLE ledger.journal_entry_comment TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ledger.journal_entry_comment TO sonofleo_migrator;

-- Table: ledger.journal_entry_ext_reference

CREATE TABLE IF NOT EXISTS ledger.journal_entry_ext_reference
(
    unique_id uuid NOT NULL,
    journal_entry_id uuid NOT NULL,
    financial_institution character varying(100) COLLATE pg_catalog."default" NOT NULL,
    reference character varying(100) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT journal_entry_ext_reference_pkey PRIMARY KEY (unique_id),
    CONSTRAINT journal_entry_ext_reference_journal_entry_id_fkey FOREIGN KEY (journal_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.journal_entry_ext_reference
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ledger.journal_entry_ext_reference FROM leobloom_hobson;

GRANT SELECT ON TABLE ledger.journal_entry_ext_reference TO leobloom_hobson;

GRANT ALL ON TABLE ledger.journal_entry_ext_reference TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ledger.journal_entry_ext_reference TO sonofleo_migrator;
                              
-- Table: ledger.journal_entry_line

CREATE TABLE IF NOT EXISTS ledger.journal_entry_line
(
    unique_id uuid NOT NULL,
    journal_entry_id uuid NOT NULL,
    account_id uuid NOT NULL,
    amount numeric(12,2) NOT NULL,
    line_type character varying(6) COLLATE pg_catalog."default" NOT NULL,
    memo character varying(1000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT journal_entry_line_pkey PRIMARY KEY (unique_id),
    CONSTRAINT journal_entry_line_account_id_fkey FOREIGN KEY (account_id)
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT journal_entry_line_journal_entry_id_fkey FOREIGN KEY (journal_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.journal_entry_line
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE ledger.journal_entry_line FROM leobloom_hobson;

GRANT SELECT ON TABLE ledger.journal_entry_line TO leobloom_hobson;

GRANT ALL ON TABLE ledger.journal_entry_line TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE ledger.journal_entry_line TO sonofleo_migrator;

