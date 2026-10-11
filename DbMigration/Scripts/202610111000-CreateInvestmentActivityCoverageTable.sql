-- The date ranges for which an Investment Account's activity has been supplied, kept merged: recording activity for a
-- range deletes the rows it overlaps or adjoins and inserts one row spanning them all. Without it, a stretch never
-- supplied looks exactly like a stretch where nothing happened. Date order and overlap are checked by the application,
-- as every other business rule is; the schema holds the key, the account reference and one row per account and begin.
--
-- Activity recorded before this migration has no coverage. There are no rows to carry forward: a range is covered only
-- once it is supplied again.

-- Table: positions.investment_activity_coverage

CREATE TABLE IF NOT EXISTS positions.investment_activity_coverage
(
    unique_id uuid NOT NULL,
    investment_account_id uuid NOT NULL,
    begin_date date NOT NULL,
    end_date date NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT investment_activity_coverage_pkey PRIMARY KEY (unique_id),
    CONSTRAINT investment_activity_coverage_account_begin_key UNIQUE (investment_account_id, begin_date),
    CONSTRAINT investment_activity_coverage_investment_account_id_fkey FOREIGN KEY (investment_account_id)
        REFERENCES positions.investment_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.investment_activity_coverage
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.investment_activity_coverage FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.investment_activity_coverage TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.investment_activity_coverage TO sonofleo_{ENV};

-- The test fixture empties every table before each run (Tests/Tests.Helpers/TestDataStage.fs), so the test
-- environment's role may truncate this one too. No other environment's role can.

DO $$
BEGIN
    IF '{ENV}' = 'test' THEN
        GRANT TRUNCATE ON TABLE positions.investment_activity_coverage TO sonofleo_test;
    END IF;
END $$;
