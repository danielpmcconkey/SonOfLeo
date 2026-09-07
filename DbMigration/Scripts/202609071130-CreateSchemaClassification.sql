create schema classification authorization sonofleo_{ENV};

GRANT USAGE ON SCHEMA classification TO leobloom_hobson;

GRANT ALL ON SCHEMA classification TO sonofleo_{ENV};

GRANT ALL ON SCHEMA classification TO sonofleo_migrator;

GRANT USAGE, CREATE ON SCHEMA classification TO sonofleo_migrator;
