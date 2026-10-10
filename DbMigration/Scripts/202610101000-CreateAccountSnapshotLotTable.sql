-- A snapshot line may carry the open lots its institution reported. A lot is a component of its line, like the line
-- is of its snapshot: no timestamps. Replacing a snapshot deletes its lines, and their lots go with them. The ordinal
-- keeps the order supplied; two lots may agree in every other column, so nothing else is unique.

-- Table: positions.account_snapshot_lot

CREATE TABLE IF NOT EXISTS positions.account_snapshot_lot
(
    unique_id uuid NOT NULL,
    account_snapshot_line_id uuid NOT NULL,
    ordinal integer NOT NULL,
    acquired_date date NOT NULL,
    quantity numeric(16,6) NOT NULL,
    reported_cost_basis numeric(12,2),
    CONSTRAINT account_snapshot_lot_pkey PRIMARY KEY (unique_id),
    CONSTRAINT account_snapshot_lot_account_snapshot_line_id_ordinal_key UNIQUE (account_snapshot_line_id, ordinal),
    CONSTRAINT account_snapshot_lot_account_snapshot_line_id_fkey FOREIGN KEY (account_snapshot_line_id)
        REFERENCES positions.account_snapshot_line (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.account_snapshot_lot
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.account_snapshot_lot FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.account_snapshot_lot TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.account_snapshot_lot TO sonofleo_{ENV};
