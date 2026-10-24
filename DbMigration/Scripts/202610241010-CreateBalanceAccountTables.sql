-- Balance Accounts (design record D40) replace pre-ledger balances: cash held and debts owed outside any Investment
-- Account, recorded as dated balances, so net worth can reach back before the ledger began without the code knowing
-- when it began. A Balance Account has owners; a Balance is what it held or owed on one date, one per account and date;
-- a Property may name Debt Balance Accounts as its mortgages, each the mortgage of at most one Property. Kind, sign and
-- date rules are the application's, as every other business rule is; the schema holds the keys, the references and
-- the uniqueness the rules need.

-- Table: positions.balance_account

CREATE TABLE IF NOT EXISTS positions.balance_account
(
    unique_id uuid NOT NULL,
    balance_account_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    kind character varying(25) COLLATE pg_catalog."default" NOT NULL,
    institution character varying(100) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT balance_account_pkey PRIMARY KEY (unique_id),
    CONSTRAINT balance_account_balance_account_name_key UNIQUE (balance_account_name)
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.balance_account
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.balance_account FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.balance_account TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.balance_account TO sonofleo_{ENV};

-- Table: positions.balance_account_owner

CREATE TABLE IF NOT EXISTS positions.balance_account_owner
(
    balance_account_id uuid NOT NULL,
    person_id uuid NOT NULL,
    CONSTRAINT balance_account_owner_pkey PRIMARY KEY (balance_account_id, person_id),
    CONSTRAINT balance_account_owner_balance_account_id_fkey FOREIGN KEY (balance_account_id)
        REFERENCES positions.balance_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT balance_account_owner_person_id_fkey FOREIGN KEY (person_id)
        REFERENCES general.person (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.balance_account_owner
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.balance_account_owner FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.balance_account_owner TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.balance_account_owner TO sonofleo_{ENV};

-- Table: positions.balance_account_balance

CREATE TABLE IF NOT EXISTS positions.balance_account_balance
(
    unique_id uuid NOT NULL,
    balance_account_id uuid NOT NULL,
    balance_date date NOT NULL,
    amount numeric(12,2) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT balance_account_balance_pkey PRIMARY KEY (unique_id),
    CONSTRAINT balance_account_balance_balance_account_id_balance_date_key UNIQUE (balance_account_id, balance_date),
    CONSTRAINT balance_account_balance_balance_account_id_fkey FOREIGN KEY (balance_account_id)
        REFERENCES positions.balance_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.balance_account_balance
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.balance_account_balance FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.balance_account_balance TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.balance_account_balance TO sonofleo_{ENV};

-- Table: positions.property_mortgage_balance_account

CREATE TABLE IF NOT EXISTS positions.property_mortgage_balance_account
(
    property_id uuid NOT NULL,
    balance_account_id uuid NOT NULL,
    CONSTRAINT property_mortgage_balance_account_pkey PRIMARY KEY (property_id, balance_account_id),
    CONSTRAINT property_mortgage_balance_account_balance_account_id_key UNIQUE (balance_account_id),
    CONSTRAINT property_mortgage_balance_account_property_id_fkey FOREIGN KEY (property_id)
        REFERENCES positions.property (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT property_mortgage_balance_account_balance_account_id_fkey FOREIGN KEY (balance_account_id)
        REFERENCES positions.balance_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.property_mortgage_balance_account
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.property_mortgage_balance_account FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.property_mortgage_balance_account TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.property_mortgage_balance_account TO sonofleo_{ENV};

-- The test fixture empties every table before each run (Tests/Tests.Helpers/TestDataStage.fs), so the test
-- environment's role may truncate these too. No other environment's role can.

DO $$
BEGIN
    IF '{ENV}' = 'test' THEN
        GRANT TRUNCATE ON TABLE positions.balance_account TO sonofleo_test;
        GRANT TRUNCATE ON TABLE positions.balance_account_owner TO sonofleo_test;
        GRANT TRUNCATE ON TABLE positions.balance_account_balance TO sonofleo_test;
        GRANT TRUNCATE ON TABLE positions.property_mortgage_balance_account TO sonofleo_test;
    END IF;
END $$;
