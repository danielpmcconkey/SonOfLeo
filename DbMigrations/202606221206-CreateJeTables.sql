/*
 executed manually in all 3 envs 6/22 12:38
 */
drop table if exists ledger.journal_entry_comment;
drop table if exists ledger.journal_entry_ext_reference;
drop table if exists ledger.journal_entry_line;
drop table if exists ledger.journal_entry;
CREATE TABLE IF NOT EXISTS ledger.journal_entry
(
    unique_id uuid primary key,                                                  -- REQ-JE-1.1, REQ-JE-1.2
    description character varying(1000) collate pg_catalog."default" NOT NULL,   -- REQ-JE-1.3, REQ-JE-1.5
    je_source character varying(50) collate pg_catalog."default",                -- REQ-JE-1.6, REQ-JE-1.8
    entry_date date NOT NULL,                                                    -- REQ-JE-1.9, REQ-JE-1.10
    fiscal_period_id uuid not null,
    voided_at timestamp with time zone,                                          -- REQ-JE-1.14
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT journal_entry_fiscal_period_id_fkey FOREIGN KEY (fiscal_period_id)
    REFERENCES ledger.fiscal_period (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ledger.journal_entry_line
(
    unique_id uuid primary key,                                                  -- REQ-JE-1.20, REQ-JE-1.21
    journal_entry_id uuid not null,                                              -- REQ-JE-1.29
    account_id uuid not null,                                                    -- REQ-JE-1.22
    amount numeric(12,2) not null,                                               -- REQ-JE-1.23
    line_type character varying(6) not null,                                     -- REQ-JE-1.25
    memo character varying(1000) collate pg_catalog."default",                   -- REQ-JE-1.26, REQ-JE-1.28
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
        CONSTRAINT journal_entry_line_journal_entry_id_fkey                      -- REQ-JE-1.29
        FOREIGN KEY (journal_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT journal_entry_line_account_id_fkey FOREIGN KEY (account_id)       -- REQ-JE-1.22
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ledger.journal_entry_ext_reference
(
    unique_id uuid primary key,                                                  -- REQ-JE-1.40
    journal_entry_id uuid not null,                                              -- REQ-JE-1.41
    financial_institution character varying(100)                                 -- REQ-JE-1.42
    not null collate pg_catalog."default",
    reference character varying(100) not null collate pg_catalog."default",      -- REQ-JE-1.45
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
        CONSTRAINT journal_entry_ext_reference_journal_entry_id_fkey             -- REQ-JE-1.41
        FOREIGN KEY (journal_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ledger.journal_entry_comment
(
    unique_id uuid primary key,                                                  -- REQ-JE-1.50
    journal_primary_entry_id uuid not null,                                      -- REQ-JE-1.51
    journal_secondary_entry_id uuid,                                             -- REQ-JE-1.52
    comment character varying(2000) not null collate pg_catalog."default",       -- REQ-JE-1.54
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
        CONSTRAINT journal_entry_comment_journal_primary_entry_id_fkey           -- REQ-JE-1.51
        FOREIGN KEY (journal_primary_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT journal_entry_comment_journal_secondary_entry_id_fkey             -- REQ-JE-1.52
    FOREIGN KEY (journal_secondary_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS ledger.journal_entry                OWNER to sonofleo_dev;
ALTER TABLE IF EXISTS ledger.journal_entry_line           OWNER to sonofleo_dev;
ALTER TABLE IF EXISTS ledger.journal_entry_ext_reference  OWNER to sonofleo_dev;
ALTER TABLE IF EXISTS ledger.journal_entry_comment        OWNER to sonofleo_dev;