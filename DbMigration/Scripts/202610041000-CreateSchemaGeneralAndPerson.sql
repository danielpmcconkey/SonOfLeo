-- Person is a business foundation that every domain may lean on (an account's or a property's owner), so it lives in
-- its own schema beneath them all rather than in positions.

create schema general authorization sonofleo_migrator;

GRANT USAGE ON SCHEMA general TO leobloom_hobson;

GRANT USAGE ON SCHEMA general TO sonofleo_{ENV};

-- Table: general.person

CREATE TABLE IF NOT EXISTS general.person
(
    unique_id uuid NOT NULL,
    person_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    birthdate date NOT NULL,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL,
    CONSTRAINT person_pkey PRIMARY KEY (unique_id),
    CONSTRAINT person_person_name_key UNIQUE (person_name)
)

    TABLESPACE pg_default;

ALTER TABLE IF EXISTS general.person
    OWNER to sonofleo_migrator;

REVOKE ALL ON TABLE general.person FROM leobloom_hobson;

GRANT SELECT ON TABLE general.person TO leobloom_hobson;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE general.person TO sonofleo_{ENV};
