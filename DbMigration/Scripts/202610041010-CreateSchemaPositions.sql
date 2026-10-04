-- Positions: what the household holds, who owns it, and what it was worth on a date. Investments and real estate are
-- peer sub-domains in one schema.

create schema positions authorization sonofleo_migrator;

GRANT USAGE ON SCHEMA positions TO leobloom_hobson;

GRANT USAGE ON SCHEMA positions TO sonofleo_{ENV};
