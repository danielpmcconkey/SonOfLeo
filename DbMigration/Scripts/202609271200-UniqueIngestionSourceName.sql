-- Ingestion source names must be unique (REQ-STG-2.28). Records resolve their source by name, so two sources with one
-- name make every file from that institution unresolvable.
--
-- Existing duplicates are not resolved here: choosing which source survives means re-pointing staged entries, and that
-- is a decision for a person. If any exist, this script stops and names them.

DO $$
DECLARE
    duplicated text;
BEGIN
    SELECT string_agg(source_name, ', ' ORDER BY source_name)
    INTO duplicated
    FROM (SELECT source_name FROM ingestion.source GROUP BY source_name HAVING count(*) > 1) d;

    IF duplicated IS NOT NULL THEN
        RAISE EXCEPTION 'Cannot make ingestion source names unique: these names are held by more than one source: %. Re-point their staged entries to one source and delete the others, then run this migration again.', duplicated;
    END IF;
END $$;

ALTER TABLE IF EXISTS ingestion.source
    ADD CONSTRAINT source_source_name_key UNIQUE (source_name);
