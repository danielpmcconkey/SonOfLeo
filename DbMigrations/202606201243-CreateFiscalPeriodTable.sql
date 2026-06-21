-- run in all 3 environments (with different owners) 2026-06-13 12:45

-- DROP TABLE IF EXISTS ledger.fiscal_period;

CREATE TABLE IF NOT EXISTS ledger.fiscal_period
(
    id uuid NOT NULL,
    period_key character varying(7) COLLATE pg_catalog."default" NOT NULL,
    start_date date not null,
    end_date date not null,
    is_open boolean not null, -- REQ-FP-1.8
    created_at timestamptz not null,
    modified_at timestamptz not null,
    CONSTRAINT fiscal_period_pkey PRIMARY KEY (id), -- REQ-FP-1.7
    CONSTRAINT fiscal_period_period_key_unq UNIQUE (period_key) -- REQ-FP-1.3, REQ-FP-2.2
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS ledger.fiscal_period
    OWNER to sonofleo_dev;