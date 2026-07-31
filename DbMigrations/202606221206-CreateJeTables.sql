/*
 executed manually in all 3 envs 6/22 12:38
 */
drop table if exists ledger.journal_entry_comment;
drop table if exists ledger.journal_entry_ext_reference;
drop table if exists ledger.journal_entry_line;
drop table if exists ledger.journal_entry;
CREATE TABLE IF NOT EXISTS ledger.journal_entry
(
    unique_id uuid primary key,
    description character varying(1000) collate pg_catalog."default" NOT NULL,
    je_source character varying(50) collate pg_catalog."default",
    entry_date date NOT NULL,
    fiscal_period_id uuid not null,
    voided_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
                              CONSTRAINT journal_entry_fiscal_period_id_fkey FOREIGN KEY (fiscal_period_id)
    REFERENCES ledger.fiscal_period (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ledger.journal_entry_line
(
    unique_id uuid primary key,
    journal_entry_id uuid not null,
    account_id uuid not null,
    amount numeric(12,2) not null,
    line_type character varying(6) not null,
    memo character varying(1000) collate pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
        CONSTRAINT journal_entry_line_journal_entry_id_fkey
        FOREIGN KEY (journal_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT journal_entry_line_account_id_fkey FOREIGN KEY (account_id)
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ledger.journal_entry_ext_reference
(
    unique_id uuid primary key,
    journal_entry_id uuid not null,
    financial_institution character varying(100)
    not null collate pg_catalog."default",
    reference character varying(100) not null collate pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
        CONSTRAINT journal_entry_ext_reference_journal_entry_id_fkey
        FOREIGN KEY (journal_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ledger.journal_entry_comment
(
    unique_id uuid primary key,
    journal_primary_entry_id uuid not null,
    journal_secondary_entry_id uuid,
    comment character varying(2000) not null collate pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
        CONSTRAINT journal_entry_comment_journal_primary_entry_id_fkey
        FOREIGN KEY (journal_primary_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT journal_entry_comment_journal_secondary_entry_id_fkey
    FOREIGN KEY (journal_secondary_entry_id)
    REFERENCES ledger.journal_entry (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS ledger.journal_entry                OWNER to sonofleo_dev;
ALTER TABLE IF EXISTS ledger.journal_entry_line           OWNER to sonofleo_dev;
ALTER TABLE IF EXISTS ledger.journal_entry_ext_reference  OWNER to sonofleo_dev;
ALTER TABLE IF EXISTS ledger.journal_entry_comment        OWNER to sonofleo_dev;