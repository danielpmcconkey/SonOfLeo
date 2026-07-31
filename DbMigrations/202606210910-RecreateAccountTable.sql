/*
 executed manually in all 3 envs 6/21 09:14
 */
drop table ledger.account;
CREATE TABLE IF NOT EXISTS ledger.account
(
    unique_id uuid primary key,
    code character varying(10) collate pg_catalog."default" NOT NULL,
    account_name character varying(100) collate pg_catalog."default" NOT NULL,
    account_type_id integer NOT NULL,
    active_begin timestamp with time zone NOT NULL,
    active_end timestamp with time zone,
    account_subtype character varying(25) collate pg_catalog."default",
    parent_id uuid,
    external_ref character varying(50) collate pg_catalog."default",
    created_at timestamp with time zone NOT NULL ,
    modified_at timestamp with time zone NOT NULL ,
    CONSTRAINT account_code_key UNIQUE (code),
    CONSTRAINT account_account_type_id_fkey FOREIGN KEY (account_type_id)
    REFERENCES ledger.account_type (id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT,
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)
    REFERENCES ledger.account (unique_id) MATCH SIMPLE
                          ON UPDATE NO ACTION
                          ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS ledger.account
    OWNER to sonofleo_dev;