-- What a ledger account held on a date before the ledger's first fiscal period, from older records, so net worth can
-- reach back before the ledger began. It lives with Positions, which already sits above the ledger. Which accounts
-- and dates may carry one is the application's rule; the key only keeps one per account per date.

-- Table: positions.pre_ledger_balance

CREATE TABLE IF NOT EXISTS positions.pre_ledger_balance
(
    unique_id uuid NOT NULL,
    ledger_account_id uuid NOT NULL,
    balance_date date NOT NULL,
    balance numeric(12,2) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT pre_ledger_balance_pkey PRIMARY KEY (unique_id),
    CONSTRAINT pre_ledger_balance_ledger_account_id_balance_date_key UNIQUE (ledger_account_id, balance_date),
    CONSTRAINT pre_ledger_balance_ledger_account_id_fkey FOREIGN KEY (ledger_account_id)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.pre_ledger_balance
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.pre_ledger_balance FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.pre_ledger_balance TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.pre_ledger_balance TO sonofleo_{ENV};
