-- The test fixture empties every table before each run (Tests/Tests.Helpers/TestDataStage.fs), so the test
-- environment's role may truncate the general and positions tables too. No other environment's role can.

DO $$
BEGIN
    IF '{ENV}' = 'test' THEN
        GRANT TRUNCATE ON ALL TABLES IN SCHEMA general, positions TO sonofleo_test;
    END IF;
END $$;
