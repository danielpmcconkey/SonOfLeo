/*
 executed manually in dev 6/13 15:00
 executed manually in test 6/14 10:08
 executed manually in prod 6/14 10:08
 
 drop table ledger.account;
 
 */
CREATE TABLE IF NOT EXISTS ledger.account
(
    id uuid primary key,
    code character varying(10) collate pg_catalog."default" NOT NULL,
    name character varying(100) collate pg_catalog."default" NOT NULL,
    account_type_id integer NOT NULL,
    active_begin timestamp with time zone NOT NULL,
    active_end timestamp with time zone,
    created_at timestamp with time zone NOT NULL ,                               
    modified_at timestamp with time zone NOT NULL ,                              
    account_subtype character varying(25) collate pg_catalog."default",
    parent_id uuid,
    external_ref character varying(50) collate pg_catalog."default",
    CONSTRAINT account_code_key UNIQUE (code),
    CONSTRAINT account_account_type_id_fkey FOREIGN KEY (account_type_id)
    REFERENCES ledger.account_type (id) MATCH SIMPLE
                           ON UPDATE NO ACTION
                           ON DELETE RESTRICT,
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)
    REFERENCES ledger.account (id) MATCH SIMPLE
                           ON UPDATE NO ACTION
                           ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS ledger.account
    OWNER to sonofleo_dev;