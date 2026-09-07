create schema cashflow authorization sonofleo_{ENV};

GRANT USAGE ON SCHEMA cashflow TO leobloom_hobson;

GRANT ALL ON SCHEMA cashflow TO sonofleo_{ENV};

GRANT ALL ON SCHEMA cashflow TO sonofleo_migrator;

GRANT USAGE, CREATE ON SCHEMA cashflow TO sonofleo_migrator;
