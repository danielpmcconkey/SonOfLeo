-- A Property links a set of FixedAsset ledger accounts, not at most one (REQ-POS-9.7 as amended). The set is a
-- component row like the mortgage set: no timestamps. The unique key on the ledger account keeps it linked to at most
-- one Property. Every existing link is carried into the new table before the old column goes.

CREATE TABLE IF NOT EXISTS positions.property_asset_account
(
    property_id uuid NOT NULL,
    ledger_account_id uuid NOT NULL,
    CONSTRAINT property_asset_account_pkey PRIMARY KEY (property_id, ledger_account_id),
    CONSTRAINT property_asset_account_ledger_account_id_key UNIQUE (ledger_account_id),
    CONSTRAINT property_asset_account_property_id_fkey FOREIGN KEY (property_id)
        REFERENCES positions.property (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT property_asset_account_ledger_account_id_fkey FOREIGN KEY (ledger_account_id)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.property_asset_account
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.property_asset_account FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.property_asset_account TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.property_asset_account TO sonofleo_{ENV};

DO $$
BEGIN
    IF '{ENV}' = 'test' THEN
        GRANT TRUNCATE ON TABLE positions.property_asset_account TO sonofleo_test;
    END IF;
END $$;

INSERT INTO positions.property_asset_account (property_id, ledger_account_id)
SELECT unique_id, ledger_asset_account_id
FROM positions.property
WHERE ledger_asset_account_id IS NOT NULL;

ALTER TABLE IF EXISTS positions.property
    DROP COLUMN ledger_asset_account_id;
