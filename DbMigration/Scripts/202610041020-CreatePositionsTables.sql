-- The positions tables. Keys carry structural integrity only; every business rule (a security's dimension slots, tax
-- treatment against owner count and basis method, the snapshot tolerance, every date range) is the application's.

-- Table: positions.dimension_value

CREATE TABLE IF NOT EXISTS positions.dimension_value
(
    unique_id uuid NOT NULL,
    dimension character varying(25) COLLATE pg_catalog."default" NOT NULL,
    value_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT dimension_value_pkey PRIMARY KEY (unique_id),
    CONSTRAINT dimension_value_dimension_value_name_key UNIQUE (dimension, value_name)
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.dimension_value
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.dimension_value FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.dimension_value TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.dimension_value TO sonofleo_{ENV};

-- Table: positions.security

CREATE TABLE IF NOT EXISTS positions.security
(
    unique_id uuid NOT NULL,
    security_name character varying(200) COLLATE pg_catalog."default" NOT NULL,
    ticker character varying(20) COLLATE pg_catalog."default",
    investment_type_value_id uuid,
    market_cap_value_id uuid,
    index_type_value_id uuid,
    sector_value_id uuid,
    region_value_id uuid,
    objective_value_id uuid,
    benchmark_value_id uuid,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT security_pkey PRIMARY KEY (unique_id),
    CONSTRAINT security_security_name_key UNIQUE (security_name),
    CONSTRAINT security_ticker_key UNIQUE (ticker),
    CONSTRAINT security_investment_type_value_id_fkey FOREIGN KEY (investment_type_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT security_market_cap_value_id_fkey FOREIGN KEY (market_cap_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT security_index_type_value_id_fkey FOREIGN KEY (index_type_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT security_sector_value_id_fkey FOREIGN KEY (sector_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT security_region_value_id_fkey FOREIGN KEY (region_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT security_objective_value_id_fkey FOREIGN KEY (objective_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT security_benchmark_value_id_fkey FOREIGN KEY (benchmark_value_id)
        REFERENCES positions.dimension_value (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.security
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.security FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.security TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.security TO sonofleo_{ENV};

-- Table: positions.investment_account

CREATE TABLE IF NOT EXISTS positions.investment_account
(
    unique_id uuid NOT NULL,
    account_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    institution character varying(100) COLLATE pg_catalog."default" NOT NULL,
    account_group character varying(100) COLLATE pg_catalog."default" NOT NULL,
    tax_treatment character varying(25) COLLATE pg_catalog."default" NOT NULL,
    active_begin date NOT NULL,
    active_end date,
    ledger_account_id uuid,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT investment_account_pkey PRIMARY KEY (unique_id),
    CONSTRAINT investment_account_account_name_key UNIQUE (account_name),
    CONSTRAINT investment_account_ledger_account_id_key UNIQUE (ledger_account_id),
    CONSTRAINT investment_account_ledger_account_id_fkey FOREIGN KEY (ledger_account_id)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.investment_account
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.investment_account FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.investment_account TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.investment_account TO sonofleo_{ENV};

-- Table: positions.investment_account_owner

CREATE TABLE IF NOT EXISTS positions.investment_account_owner
(
    investment_account_id uuid NOT NULL,
    person_id uuid NOT NULL,
    CONSTRAINT investment_account_owner_pkey PRIMARY KEY (investment_account_id, person_id),
    CONSTRAINT investment_account_owner_investment_account_id_fkey FOREIGN KEY (investment_account_id)
        REFERENCES positions.investment_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT investment_account_owner_person_id_fkey FOREIGN KEY (person_id)
        REFERENCES general.person (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.investment_account_owner
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.investment_account_owner FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.investment_account_owner TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.investment_account_owner TO sonofleo_{ENV};

-- Table: positions.holding

CREATE TABLE IF NOT EXISTS positions.holding
(
    unique_id uuid NOT NULL,
    investment_account_id uuid NOT NULL,
    security_id uuid NOT NULL,
    basis_method character varying(25) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT holding_pkey PRIMARY KEY (unique_id),
    CONSTRAINT holding_investment_account_id_security_id_key UNIQUE (investment_account_id, security_id),
    CONSTRAINT holding_investment_account_id_fkey FOREIGN KEY (investment_account_id)
        REFERENCES positions.investment_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT holding_security_id_fkey FOREIGN KEY (security_id)
        REFERENCES positions.security (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.holding
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.holding FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.holding TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.holding TO sonofleo_{ENV};

-- Table: positions.account_snapshot

CREATE TABLE IF NOT EXISTS positions.account_snapshot
(
    unique_id uuid NOT NULL,
    investment_account_id uuid NOT NULL,
    snapshot_date date NOT NULL,
    provenance character varying(25) COLLATE pg_catalog."default" NOT NULL,
    contribution_basis numeric(12,2),
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT account_snapshot_pkey PRIMARY KEY (unique_id),
    CONSTRAINT account_snapshot_investment_account_id_snapshot_date_key UNIQUE (investment_account_id, snapshot_date),
    CONSTRAINT account_snapshot_investment_account_id_fkey FOREIGN KEY (investment_account_id)
        REFERENCES positions.investment_account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.account_snapshot
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.account_snapshot FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.account_snapshot TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.account_snapshot TO sonofleo_{ENV};

-- Table: positions.account_snapshot_line

CREATE TABLE IF NOT EXISTS positions.account_snapshot_line
(
    unique_id uuid NOT NULL,
    account_snapshot_id uuid NOT NULL,
    holding_id uuid NOT NULL,
    quantity numeric(16,6) NOT NULL,
    price numeric(16,6) NOT NULL,
    market_value numeric(12,2) NOT NULL,
    reported_cost_basis numeric(12,2),
    CONSTRAINT account_snapshot_line_pkey PRIMARY KEY (unique_id),
    CONSTRAINT account_snapshot_line_account_snapshot_id_holding_id_key UNIQUE (account_snapshot_id, holding_id),
    CONSTRAINT account_snapshot_line_account_snapshot_id_fkey FOREIGN KEY (account_snapshot_id)
        REFERENCES positions.account_snapshot (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT account_snapshot_line_holding_id_fkey FOREIGN KEY (holding_id)
        REFERENCES positions.holding (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.account_snapshot_line
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.account_snapshot_line FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.account_snapshot_line TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.account_snapshot_line TO sonofleo_{ENV};

-- Table: positions.property

CREATE TABLE IF NOT EXISTS positions.property
(
    unique_id uuid NOT NULL,
    property_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    property_use character varying(25) COLLATE pg_catalog."default" NOT NULL,
    acquisition_date date NOT NULL,
    disposal_date date,
    purchase_basis numeric(12,2) NOT NULL,
    ledger_asset_account_id uuid,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT property_pkey PRIMARY KEY (unique_id),
    CONSTRAINT property_property_name_key UNIQUE (property_name),
    CONSTRAINT property_ledger_asset_account_id_key UNIQUE (ledger_asset_account_id),
    CONSTRAINT property_ledger_asset_account_id_fkey FOREIGN KEY (ledger_asset_account_id)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.property
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.property FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.property TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.property TO sonofleo_{ENV};

-- Table: positions.property_owner

CREATE TABLE IF NOT EXISTS positions.property_owner
(
    property_id uuid NOT NULL,
    person_id uuid NOT NULL,
    CONSTRAINT property_owner_pkey PRIMARY KEY (property_id, person_id),
    CONSTRAINT property_owner_property_id_fkey FOREIGN KEY (property_id)
        REFERENCES positions.property (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT property_owner_person_id_fkey FOREIGN KEY (person_id)
        REFERENCES general.person (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.property_owner
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.property_owner FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.property_owner TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.property_owner TO sonofleo_{ENV};

-- Table: positions.property_mortgage_account

CREATE TABLE IF NOT EXISTS positions.property_mortgage_account
(
    property_id uuid NOT NULL,
    ledger_account_id uuid NOT NULL,
    CONSTRAINT property_mortgage_account_pkey PRIMARY KEY (property_id, ledger_account_id),
    CONSTRAINT property_mortgage_account_ledger_account_id_key UNIQUE (ledger_account_id),
    CONSTRAINT property_mortgage_account_property_id_fkey FOREIGN KEY (property_id)
        REFERENCES positions.property (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT property_mortgage_account_ledger_account_id_fkey FOREIGN KEY (ledger_account_id)
        REFERENCES ledger.account (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.property_mortgage_account
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.property_mortgage_account FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.property_mortgage_account TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.property_mortgage_account TO sonofleo_{ENV};

-- Table: positions.valuation

CREATE TABLE IF NOT EXISTS positions.valuation
(
    unique_id uuid NOT NULL,
    property_id uuid NOT NULL,
    valuation_date date NOT NULL,
    valuation_value numeric(12,2) NOT NULL,
    basis character varying(100) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT valuation_pkey PRIMARY KEY (unique_id),
    CONSTRAINT valuation_property_id_valuation_date_key UNIQUE (property_id, valuation_date),
    CONSTRAINT valuation_property_id_fkey FOREIGN KEY (property_id)
        REFERENCES positions.property (unique_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS positions.valuation
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE positions.valuation FROM leobloom_hobson;

GRANT SELECT ON TABLE positions.valuation TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE positions.valuation TO sonofleo_{ENV};
