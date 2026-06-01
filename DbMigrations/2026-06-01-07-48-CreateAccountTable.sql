/*
 executed manually in dev 6/1 07:50 
 */
 
create schema sonofledger authorization claude;

CREATE TABLE IF NOT EXISTS sonofledger.account_type
(
    id integer primary key,
    name character varying(20) COLLATE pg_catalog."default" NOT NULL,
    normal_balance character varying(6) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT account_type_name_key UNIQUE (name)
    );

ALTER TABLE IF EXISTS sonofledger.account_type
    OWNER to claude;

INSERT INTO sonofledger.account_type(id, name, normal_balance) VALUES (1,'asset','debit');
INSERT INTO sonofledger.account_type(id, name, normal_balance) VALUES (2,'liability','credit');
INSERT INTO sonofledger.account_type(id, name, normal_balance) VALUES (3,'equity','credit');
INSERT INTO sonofledger.account_type(id, name, normal_balance) VALUES (4,'revenue','credit');
INSERT INTO sonofledger.account_type(id, name, normal_balance) VALUES (5,'expense','debit');

CREATE TABLE IF NOT EXISTS sonofledger.account
(
    id uuid primary key,
    code character varying(10) COLLATE pg_catalog."default" NOT NULL,
    name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    account_type_id integer NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    modified_at timestamp with time zone NOT NULL DEFAULT now(),
    account_subtype character varying(25) COLLATE pg_catalog."default",
    parent_id uuid,
    external_ref character varying(50) COLLATE pg_catalog."default",
    CONSTRAINT account_code_key UNIQUE (code),
    CONSTRAINT account_account_type_id_fkey FOREIGN KEY (account_type_id)
    REFERENCES sonofledger.account_type (id) MATCH SIMPLE
                         ON UPDATE NO ACTION
                         ON DELETE RESTRICT,
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)
    REFERENCES sonofledger.account (id) MATCH SIMPLE
                         ON UPDATE NO ACTION
                         ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS sonofledger.account
    OWNER to claude;