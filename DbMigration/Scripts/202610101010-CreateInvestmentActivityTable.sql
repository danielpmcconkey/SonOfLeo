-- What an institution reported happening in an Investment Account: contributions, purchases, dividends, fees. The kind
-- is checked by the application, as tax treatment and basis method are. Two activities may agree in every column, so
-- the only unique key is the ordinal that keeps one day's activities in the order supplied; it also serves reads by
-- account and date range.

-- Table: positions.investment_activity

CREATE TABLE IF NOT EXISTS positions.investment_activity
(
    unique_id uuid NOT NULL,
    investment_account_id uuid NOT NULL,
    activity_date date NOT NULL,
    ordinal integer NOT NULL,
    activity_kind character varying(25) COLLATE pg_catalog."default" NOT NULL,
    description character varying(500) COLLATE pg_catalog."default" NOT NULL,
    source character varying(100) COLLATE pg_catalog."default",
    holding_id uuid,
    quantity numeric(16,6),
    price numeric(16,6),
    amount numeric(12,2) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT investment_activity_pkey PRIMARY KEY (unique_id),
    CONSTRAINT investment_activity_account_date_ordinal_key
        UNIQUE (investment_account_id, activity_date, ordinal),
    CONSTRAINT investment_activity_investment_account_id_fkey FOREIGN KEY (investment_account_id)
        REFERENCES positions.investment_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT investment_activity_holding_id_fkey FOREIGN KEY (holding_id)
        REFERENCES positions.holding (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.investment_activity
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.investment_activity FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.investment_activity TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.investment_activity TO sonofleo_{ENV};
