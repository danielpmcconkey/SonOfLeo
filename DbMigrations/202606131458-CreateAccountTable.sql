/*
 executed manually in dev 6/13 15:00
 
 drop table ledger.account;
 
 */
CREATE TABLE IF NOT EXISTS ledger.account
(
    id uuid primary key,                                                         -- REQ-AC-1.21, REQ-AC-1.22, REQ-AC-2.8 
    code character varying(10) collate pg_catalog."default" NOT NULL,            -- REQ-AC-1.1, REQ-AC-1.3
    name character varying(100) collate pg_catalog."default" NOT NULL,           -- REQ-AC-1.6, REQ-AC-1.8
    account_type_id integer NOT NULL,                                            -- REQ-AC-1.23
    active_begin timestamp with time zone NOT NULL,                              -- REQ-AC-1.42, REQ-AC-1.44
    active_end timestamp with time zone,                                         -- REQ-AC-1.43, REQ-AC-1.45
    created_at timestamp with time zone NOT NULL ,                               
    modified_at timestamp with time zone NOT NULL ,                              
    account_subtype character varying(25) collate pg_catalog."default",          -- REQ-AC-1.19
    parent_id uuid,                                                              -- REQ-AC-1.37
    external_ref character varying(50) collate pg_catalog."default",             -- REQ-AC-1.20, REQ-AC-1.41
    CONSTRAINT account_code_key UNIQUE (code),                                   -- REQ-AC-1.4, REQ-AC-2.9
    CONSTRAINT account_account_type_id_fkey FOREIGN KEY (account_type_id)
    REFERENCES ledger.account_type (id) MATCH SIMPLE
                           ON UPDATE NO ACTION
                           ON DELETE RESTRICT,
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)                    -- REQ-AC-1.40
    REFERENCES ledger.account (id) MATCH SIMPLE
                           ON UPDATE NO ACTION
                           ON DELETE RESTRICT
    );

ALTER TABLE IF EXISTS ledger.account
    OWNER to claude;