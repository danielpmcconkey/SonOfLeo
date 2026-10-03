-- An Instance or Invoice may be cancelled (REQ-CF-4.11, REQ-CF-5.17). It is cancelled exactly when its cancellation
-- reason note is present. The note's rules (required, trimmed, at most 500 characters) are business rules and live in
-- the application (REQ-DAL-3.6); the column length matches the note's maximum.

ALTER TABLE IF EXISTS cashflow.instance
    ADD COLUMN cancellation_reason_note character varying(500);

ALTER TABLE IF EXISTS cashflow.invoice
    ADD COLUMN cancellation_reason_note character varying(500);
