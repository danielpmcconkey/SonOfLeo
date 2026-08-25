/*
executed manually in dev 6/13 14:55
executed manually in test 6/14 10:06
executed manually in prod 6/14 10:06

drop schema ledger;
 
 */

create schema ledger authorization sonofleo_{ENV};

GRANT USAGE ON SCHEMA ledger TO leobloom_hobson;

GRANT ALL ON SCHEMA ledger TO sonofleo_{ENV};

GRANT ALL ON SCHEMA ledger TO sonofleo_migrator;

GRANT USAGE, CREATE ON SCHEMA public, ledger TO sonofleo_migrator;
