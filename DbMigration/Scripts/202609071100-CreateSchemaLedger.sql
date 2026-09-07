create schema ledger authorization sonofleo_{ENV};

GRANT USAGE ON SCHEMA ledger TO leobloom_hobson;

GRANT ALL ON SCHEMA ledger TO sonofleo_{ENV};

GRANT ALL ON SCHEMA ledger TO sonofleo_migrator;

GRANT USAGE, CREATE ON SCHEMA ledger TO sonofleo_migrator;
