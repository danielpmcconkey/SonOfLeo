-- Table: cashflow.payment_agreement_link

CREATE TABLE IF NOT EXISTS cashflow.payment_agreement_link
(
    unique_id uuid NOT NULL,
    payment_agreement_id uuid NOT NULL,
    stage_entry_line_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT payment_agreement_link_pkey PRIMARY KEY (unique_id),
    -- one line claims exactly one payment agreement. Automatic resolution never revisits a claimed line, so a second
    -- row here means something bypassed that check.
    CONSTRAINT payment_agreement_link_line_unique UNIQUE (stage_entry_line_id),
    CONSTRAINT payment_agreement_link_payment_agreement_id_fkey FOREIGN KEY (payment_agreement_id)
    REFERENCES cashflow.payment_agreement (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    CONSTRAINT payment_agreement_link_stage_entry_line_id_fkey FOREIGN KEY (stage_entry_line_id)
    REFERENCES ingestion.staged_entry_line (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS cashflow.payment_agreement_link
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE cashflow.payment_agreement_link FROM leobloom_hobson;

GRANT SELECT ON TABLE cashflow.payment_agreement_link TO leobloom_hobson;

GRANT ALL ON TABLE cashflow.payment_agreement_link TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.payment_agreement_link TO sonofleo_migrator;
