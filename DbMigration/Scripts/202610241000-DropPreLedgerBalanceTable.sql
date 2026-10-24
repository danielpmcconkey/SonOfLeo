-- Pre-ledger balances are withdrawn (design record D40): net worth is always what Positions holds plus the ledger's
-- assets less its liabilities, and cash and debts from before the ledger began are Balance Accounts, created by the
-- next migration. The application no longer reads or writes this table, and no environment holds a row in it, so it is
-- dropped with nothing carried forward. Its grants go with it. The migrations that created it and granted on it stay
-- as they were.

DROP TABLE IF EXISTS positions.pre_ledger_balance;
