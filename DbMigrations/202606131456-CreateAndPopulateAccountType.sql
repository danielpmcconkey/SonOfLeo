/*
 executed manually in dev 6/13 14:57 
 
 drop table ledger.account_type;
 
 */


CREATE TABLE IF NOT EXISTS ledger.account_type
(
    id integer primary key,                                                      -- REQ-AC-1.11–1.15
    name character varying(20) COLLATE pg_catalog."default" NOT NULL,            -- REQ-AC-1.10
    normal_balance character varying(6) COLLATE pg_catalog."default" NOT NULL,   -- REQ-AC-1.9
    CONSTRAINT account_type_name_key UNIQUE (name)
    );

ALTER TABLE IF EXISTS ledger.account_type
    OWNER to claude;

INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (1,'asset','debit');      -- REQ-AC-1.11, REQ-AC-1.16
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (2,'liability','credit'); -- REQ-AC-1.12, REQ-AC-1.17
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (3,'equity','credit');    -- REQ-AC-1.13, REQ-AC-1.17
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (4,'revenue','credit');   -- REQ-AC-1.14, REQ-AC-1.17
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (5,'expense','debit');    -- REQ-AC-1.15, REQ-AC-1.16
