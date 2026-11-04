-- A Master Agreement may carry its own grace period for invoice matching, in days, in place of the one its cadence
-- gives. Null means the cadence's grace applies. Its range (0 to 365) is a business rule and lives in the application,
-- so the column takes no check constraint.
--
-- Existing agreements stay null, which keeps the grace they match with today; there is nothing to back-fill. The
-- table-level grants in the script that created the table already cover the new column.

ALTER TABLE IF EXISTS cashflow.master_agreement
    ADD COLUMN grace_period_days integer;
