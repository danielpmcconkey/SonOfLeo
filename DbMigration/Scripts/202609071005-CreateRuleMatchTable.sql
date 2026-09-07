-- Table: classification.rule_match

CREATE TABLE IF NOT EXISTS classification.rule_match
(
    unique_id uuid NOT NULL,
    run_id uuid NOT NULL,
    stage_entry_line_id uuid NOT NULL,
    classification_rule_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT rule_match_pkey PRIMARY KEY (unique_id),
    CONSTRAINT rule_match_run_line_rule_unique UNIQUE (run_id, stage_entry_line_id, classification_rule_id),
    CONSTRAINT rule_match_stage_entry_line_id_fkey FOREIGN KEY (stage_entry_line_id)
    REFERENCES ingestion.staged_entry_line (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT,
    CONSTRAINT rule_match_classification_rule_id_fkey FOREIGN KEY (classification_rule_id)
    REFERENCES classification.classification_rule (unique_id) MATCH SIMPLE
    ON UPDATE NO ACTION
    ON DELETE RESTRICT
    )

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS classification.rule_match
    OWNER to sonofleo_{ENV};

REVOKE ALL ON TABLE classification.rule_match FROM leobloom_hobson;

GRANT SELECT ON TABLE classification.rule_match TO leobloom_hobson;

GRANT ALL ON TABLE classification.rule_match TO sonofleo_{ENV};

GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE classification.rule_match TO sonofleo_migrator;

-- Index: ix_rule_match_run_id_stage_entry_line_id

CREATE INDEX IF NOT EXISTS ix_rule_match_run_id_stage_entry_line_id
    ON classification.rule_match USING btree
    (run_id ASC NULLS LAST, stage_entry_line_id ASC NULLS LAST)
    TABLESPACE pg_default;
