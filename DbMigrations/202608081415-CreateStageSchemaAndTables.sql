/*
executed manually in dev 2026-08-08 15:20
executed manually in test 2026-08-08 15:21
executed manually in prod 2026-08-08 15:23

drop schema ingestion cascade;
 
 */

create schema ingestion authorization claude;

CREATE TABLE IF NOT EXISTS ingestion.source
(
    unique_id uuid primary key,
    source_name character varying(100) not null collate pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
                              );

CREATE TABLE IF NOT EXISTS ingestion.staged_entry
(
    unique_id uuid primary key,
    entry_date date NOT NULL,
    description character varying(1000) collate pg_catalog."default" NOT NULL,
    source_id uuid not null
    REFERENCES ingestion.source (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    fi_reference character varying(100) not null collate pg_catalog."default",
    source_file character varying(150) not null collate pg_catalog."default",
    status character varying(25) not null collate pg_catalog."default"
    );

CREATE TABLE IF NOT EXISTS ingestion.classification_rule
(
    unique_id uuid primary key,
    source_id uuid not null
    REFERENCES ingestion.source (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    rule_name character varying(250) not null collate pg_catalog."default",
    -- todo: figure out what the rest of the ingestion.classification_rule table needs to look like. that'll be an alter table script in a separate migration
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
        );

CREATE TABLE IF NOT EXISTS ingestion.staged_entry_line
(
    unique_id uuid primary key,
    entry_id uuid not null
    REFERENCES ingestion.staged_entry (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    amount numeric(12,2) not null,
    line_type character varying(6) not null,
    code character varying(10) collate pg_catalog."default", -- nullable until classified. note this intentionally doesn't enforce the reference as account codes can change over time and they aren't the primary key
    memo character varying(1000) collate pg_catalog."default", -- nullable
    classification_rule_id uuid -- nullable unless classified
    REFERENCES ingestion.classification_rule (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT
    );

CREATE TABLE IF NOT EXISTS ingestion.staged_entry_audit
(
    unique_id uuid primary key,
    entry_id uuid not null
    REFERENCES ingestion.staged_entry (unique_id)
    MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    from_status varchar(25), -- nullable on initial creation
    to_status varchar(25) not null,
    modified_at timestamp with time zone NOT NULL,
        change_mechanism varchar(25) not null
    );







ALTER TABLE IF EXISTS ingestion.source
    OWNER to sonofleo_prod;

ALTER TABLE IF EXISTS ingestion.staged_entry
    OWNER to sonofleo_prod;

ALTER TABLE IF EXISTS ingestion.classification_rule
    OWNER to sonofleo_prod;

ALTER TABLE IF EXISTS ingestion.staged_entry_line
    OWNER to sonofleo_prod;

ALTER TABLE IF EXISTS ingestion.staged_entry_audit
    OWNER to sonofleo_prod;