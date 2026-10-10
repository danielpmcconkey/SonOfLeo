-- The test fixture empties every table before each run (Tests/Tests.Helpers/TestDataStage.fs), so the test
-- environment's role may truncate the three tables Positions slice 2 adds. No other environment's role can.

DO $$
BEGIN
    IF '{ENV}' = 'test' THEN
        GRANT TRUNCATE ON TABLE positions.account_snapshot_lot, positions.investment_activity,
            positions.pre_ledger_balance TO sonofleo_test;
    END IF;
END $$;
