-- Table: cashflow.master_agreement

CREATE TABLE IF NOT EXISTS cashflow.master_agreement
(
    unique_id uuid NOT NULL,
    agreement_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    flow_direction character varying(6) COLLATE pg_catalog."default" NOT NULL,
    cadence character varying(25) COLLATE pg_catalog."default" NOT NULL,
    cadence_week_day character varying(10) COLLATE pg_catalog."default", -- Weekly, EveryOtherWeek, Monthly (MonthDay.NthWeekDay), Annually (MonthDay.NthWeekDay)
    cadence_date_in_month integer, -- Monthly (MonthDay.DateInMonthNumber), Annually (MonthDay.DateInMonthNumber)
    cadence_week_in_month integer, -- Monthly (MonthDay.NthWeekDay), Annually (MonthDay.NthWeekDay)
    cadence_month character varying(10) COLLATE pg_catalog."default", -- Annually.Month
    counterparty character varying(250) COLLATE pg_catalog."default" NOT NULL,
    start_date date NOT NULL,
    end_date date,
    next_instance date NOT NULL,
    memo character varying(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT master_agreement_pkey PRIMARY KEY (unique_id),
    CONSTRAINT master_agreement_agreement_name_key UNIQUE (agreement_name)
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.master_agreement
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.master_agreement FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.master_agreement TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.master_agreement TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.master_agreement TO sonofleo_migrator;

-- Table: cashflow.payment_agreement

CREATE TABLE IF NOT EXISTS cashflow.payment_agreement
(
    unique_id uuid NOT NULL,
    master_agreement_id uuid NOT NULL,
    payment_agreement_name character varying(250) COLLATE pg_catalog."default" NOT NULL,
    debit_account uuid NOT NULL,
    credit_account uuid NOT NULL,
    expected_amount numeric(12,2),
    days_due_after_invoice integer,
    memo character varying(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT payment_agreement_pkey PRIMARY KEY (unique_id),
    CONSTRAINT payment_agreement_payment_agreement_name_key UNIQUE (payment_agreement_name),
    CONSTRAINT payment_agreement_master_agreement_id_fkey FOREIGN KEY (master_agreement_id)
        REFERENCES cashflow.master_agreement (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT payment_agreement_debit_account_fkey FOREIGN KEY (debit_account)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT payment_agreement_credit_account_fkey FOREIGN KEY (credit_account)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.payment_agreement
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.payment_agreement FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.payment_agreement TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.payment_agreement TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.payment_agreement TO sonofleo_migrator;

-- Table: cashflow.instance

CREATE TABLE IF NOT EXISTS cashflow.instance
(
    unique_id uuid NOT NULL,
    master_agreement_id uuid NOT NULL,
    instance_date date NOT NULL,
    is_fulfilled boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT instance_pkey PRIMARY KEY (unique_id),
    CONSTRAINT instance_master_agreement_id_fkey FOREIGN KEY (master_agreement_id)
        REFERENCES cashflow.master_agreement (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.instance
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.instance FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.instance TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.instance TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.instance TO sonofleo_migrator;

-- Table: cashflow.invoice

CREATE TABLE IF NOT EXISTS cashflow.invoice
(
    unique_id uuid NOT NULL,
    instance_id uuid NOT NULL,
    payment_agreement_id uuid NOT NULL,
    external_invoice_id character varying(100) COLLATE pg_catalog."default",
    invoice_date date NOT NULL,
    due_date date NOT NULL,
    amount numeric(12,2) NOT NULL,
    invoice_state character varying(25) COLLATE pg_catalog."default" NOT NULL,
    payment_state character varying(25) COLLATE pg_catalog."default" NOT NULL,
    posted_state character varying(25) COLLATE pg_catalog."default" NOT NULL,
    blocker_state character varying(25) COLLATE pg_catalog."default",
    blocker_note character varying(500) COLLATE pg_catalog."default",
    memo character varying(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT invoice_pkey PRIMARY KEY (unique_id),
    CONSTRAINT invoice_instance_id_fkey FOREIGN KEY (instance_id)
        REFERENCES cashflow.instance (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT invoice_payment_agreement_id_fkey FOREIGN KEY (payment_agreement_id)
        REFERENCES cashflow.payment_agreement (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.invoice
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.invoice FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.invoice TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.invoice TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.invoice TO sonofleo_migrator;

-- Table: cashflow.payment

CREATE TABLE IF NOT EXISTS cashflow.payment
(
    unique_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    journal_entry_line_id uuid,
    stage_entry_line_id uuid,
    posted_to_fi_date date,
    memo character varying(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT payment_pkey PRIMARY KEY (unique_id),
    CONSTRAINT payment_invoice_id_fkey FOREIGN KEY (invoice_id)
        REFERENCES cashflow.invoice (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT payment_journal_entry_line_id_fkey FOREIGN KEY (journal_entry_line_id)
        REFERENCES ledger.journal_entry_line (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT payment_stage_entry_line_id_fkey FOREIGN KEY (stage_entry_line_id)
        REFERENCES ingestion.staged_entry_line (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.payment
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.payment FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.payment TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.payment TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.payment TO sonofleo_migrator;

-- Table: cashflow.payment_agreement_link

CREATE TABLE IF NOT EXISTS cashflow.payment_agreement_link
(
    unique_id uuid NOT NULL,
    payment_agreement_id uuid NOT NULL,
    stage_entry_line_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT payment_agreement_link_pkey PRIMARY KEY (unique_id),
    CONSTRAINT payment_agreement_link_line_unique UNIQUE (stage_entry_line_id),
    CONSTRAINT payment_agreement_link_payment_agreement_id_fkey FOREIGN KEY (payment_agreement_id)
        REFERENCES cashflow.payment_agreement (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT payment_agreement_link_stage_entry_line_id_fkey FOREIGN KEY (stage_entry_line_id)
        REFERENCES ingestion.staged_entry_line (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.payment_agreement_link
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.payment_agreement_link FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.payment_agreement_link TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.payment_agreement_link TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.payment_agreement_link TO sonofleo_migrator;
