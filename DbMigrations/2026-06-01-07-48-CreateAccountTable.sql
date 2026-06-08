/*
 executed manually in dev 6/7 07:47 
 
 drop table ledger.account;
 drop table ledger.account_type;
 drop schema ledger;
 drop database sonofleo_dev;
 
 */

CREATE DATABASE sonofleo_dev
    WITH
    OWNER = postgres
    ENCODING = 'UTF8' -- FT-DAL-3.4
    LC_COLLATE = 'en_US.UTF-8' -- FT-DAL-3.5
    LOCALE_PROVIDER = 'libc'
    CONNECTION LIMIT = -1
    IS_TEMPLATE = False;

create schema ledger authorization claude;

CREATE TABLE IF NOT EXISTS ledger.account_type
(
    id integer primary key,                                                      -- FT-AC-1.11–1.15
    name character varying(20) COLLATE pg_catalog."default" NOT NULL,            -- FT-AC-1.10
    normal_balance character varying(6) COLLATE pg_catalog."default" NOT NULL,   -- FT-AC-1.9
    CONSTRAINT account_type_name_key UNIQUE (name)
    );

ALTER TABLE IF EXISTS ledger.account_type
    OWNER to claude;

INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (1,'asset','debit');      -- FT-AC-1.11, FT-AC-1.16
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (2,'liability','credit'); -- FT-AC-1.12, FT-AC-1.17
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (3,'equity','credit');    -- FT-AC-1.13, FT-AC-1.17
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (4,'revenue','credit');   -- FT-AC-1.14, FT-AC-1.17
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (5,'expense','debit');    -- FT-AC-1.15, FT-AC-1.16

CREATE TABLE IF NOT EXISTS ledger.account
(
    id uuid primary key,                                                         -- FT-AC-1.21, FT-AC-1.22, FT-AC-2.8 
    code character varying(10) collate pg_catalog."default" NOT NULL,            -- FT-AC-1.1, FT-AC-1.3
    name character varying(100) collate pg_catalog."default" NOT NULL,           -- FT-AC-1.6, FT-AC-1.8
    account_type_id integer NOT NULL,                                            -- FT-AC-1.23
    active_begin timestamp with time zone NOT NULL DEFAULT now(),                -- FT-AC-1.42, FT-AC-1.44
    active_end timestamp with time zone,                                         -- FT-AC-1.43, FT-AC-1.45
    created_at timestamp with time zone NOT NULL DEFAULT now(),                  -- FT-AC-1.25
    modified_at timestamp with time zone NOT NULL DEFAULT now(),                 -- FT-AC-1.26
    account_subtype character varying(25) collate pg_catalog."default",          -- FT-AC-1.19
    parent_id uuid,                                                              -- FT-AC-1.37
    external_ref character varying(50) collate pg_catalog."default",             -- FT-AC-1.20, FT-AC-1.41
    CONSTRAINT account_code_key UNIQUE (code),                                   -- FT-AC-1.4, FT-AC-2.9
    CONSTRAINT account_account_type_id_fkey FOREIGN KEY (account_type_id)
    REFERENCES ledger.account_type (id) MATCH SIMPLE
                           ON UPDATE NO ACTION
                           ON DELETE RESTRICT,
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)                    -- FT-AC-1.40
    REFERENCES ledger.account (id) MATCH SIMPLE
                           ON UPDATE NO ACTION
                           ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS ledger.account
    OWNER to claude;