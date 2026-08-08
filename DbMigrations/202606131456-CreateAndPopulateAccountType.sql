/*
 executed manually in dev 6/13 14:57 
 executed manually in test 6/14 10:07
 executed manually in prod 6/14 10:07
 
 drop table ledger.account_type;
 
 */


CREATE TABLE IF NOT EXISTS ledger.account_type
(
    id integer primary key,
    name character varying(20) COLLATE pg_catalog."default" NOT NULL,
    normal_balance character varying(6) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT account_type_name_key UNIQUE (name)
    );

ALTER TABLE IF EXISTS ledger.account_type
    OWNER to sonofleo_dev;

INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (1,'asset','debit');
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (2,'liability','credit');
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (3,'equity','credit');
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (4,'revenue','credit');
INSERT INTO ledger.account_type(id, name, normal_balance) VALUES (5,'expense','debit');
