-- No two status transitions for one staged entry share an instant (REQ-STG-4.1.2). A staged entry's current status is
-- its latest transition (REQ-STG-4.1.1), so two transitions at one instant would tie and the status would be picked
-- arbitrarily. With this constraint, a write that would tie fails instead.
--
-- Existing ties are not resolved here: which of the tied transitions is the entry's real status is a decision for a
-- person. If any exist, this script stops and names the entries and instants.

DO $$
DECLARE
    tied text;
BEGIN
    SELECT string_agg(entry_id::text || ' at ' || modified_at::text, ', ' ORDER BY entry_id, modified_at)
    INTO tied
    FROM (SELECT entry_id, modified_at FROM ingestion.staged_entry_audit GROUP BY entry_id, modified_at HAVING count(*) > 1) d;

    IF tied IS NOT NULL THEN
        RAISE EXCEPTION 'Cannot make staged entry transition instants unique: these staged entries have more than one transition at one instant: %. Decide which transition stands for each, delete the others, then run this migration again.', tied;
    END IF;
END $$;

ALTER TABLE IF EXISTS ingestion.staged_entry_audit
    ADD CONSTRAINT staged_entry_audit_entry_id_modified_at_key UNIQUE (entry_id, modified_at);
