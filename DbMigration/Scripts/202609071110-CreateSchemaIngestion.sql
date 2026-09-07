create schema ingestion authorization sonofleo_{ENV};

GRANT USAGE ON SCHEMA ingestion TO leobloom_hobson;

GRANT ALL ON SCHEMA ingestion TO sonofleo_{ENV};

GRANT ALL ON SCHEMA ingestion TO sonofleo_migrator;

GRANT USAGE, CREATE ON SCHEMA public, ingestion TO sonofleo_migrator;
