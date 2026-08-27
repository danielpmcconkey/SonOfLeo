/*
drop table cashflow.payment;
drop table cashflow.invoice;
drop table cashflow.instance;
drop table cashflow.payment_agreement;
drop table cashflow.master_agreement;
*/
create table cashflow.master_agreement
(
    unique_id uuid NOT NULL primary key,
    agreement_name varchar(100) unique COLLATE pg_catalog."default" not null,
    flow_direction varchar(6) COLLATE pg_catalog."default" not null,
    cadence varchar(25) COLLATE pg_catalog."default" not null,
    cadence_week_day varchar(10) COLLATE pg_catalog."default", -- Weekly, EveryOtherWeek, Monthly (MonthDay.NthWeekDay), Annually (MonthDay.NthWeekDay)
    cadence_date_in_month int, -- Monthly (MonthDay.DateInMonthNumber), Annually (MonthDay.DateInMonthNumber)
    cadence_week_in_month int, -- Monthly (MonthDay.NthWeekDay), Annually (MonthDay.NthWeekDay)
    cadence_month varchar(10) COLLATE pg_catalog."default", -- Annually.Month,
    counterparty varchar(250) COLLATE pg_catalog."default" not null,
    start_date date NOT NULL,
    end_date date,
    memo varchar(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
) tablespace pg_default;
ALTER TABLE cashflow.master_agreement OWNER to sonofleo_dev;
REVOKE ALL ON TABLE cashflow.master_agreement FROM leobloom_hobson;
GRANT SELECT ON TABLE cashflow.master_agreement TO leobloom_hobson;
GRANT ALL ON TABLE cashflow.master_agreement TO sonofleo_dev;
GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.master_agreement TO sonofleo_migrator;

create table cashflow.payment_agreement
(
    unique_id uuid NOT NULL primary key,
    master_agreement_id uuid not null references cashflow.master_agreement(unique_id) match simple on update no action on delete restrict,
    debit_account uuid not null references ledger.account(unique_id) match simple on update no action on delete restrict,
    credit_account uuid not null references ledger.account(unique_id) match simple on update no action on delete restrict,
    expected_amount numeric(12,2),
    memo varchar(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
) tablespace pg_default;
ALTER TABLE cashflow.payment_agreement OWNER to sonofleo_dev;
REVOKE ALL ON TABLE cashflow.payment_agreement FROM leobloom_hobson;
GRANT SELECT ON TABLE cashflow.payment_agreement TO leobloom_hobson;
GRANT ALL ON TABLE cashflow.payment_agreement TO sonofleo_dev;
GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.payment_agreement TO sonofleo_migrator;

create table cashflow.instance
(
    unique_id uuid NOT NULL primary key,
    master_agreement_id uuid not null references cashflow.master_agreement(unique_id) match simple on update no action on delete restrict,
    instance_date date not null,
    is_fulfilled bool not null,
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
) tablespace pg_default;
ALTER TABLE cashflow.instance OWNER to sonofleo_dev;
REVOKE ALL ON TABLE cashflow.instance FROM leobloom_hobson;
GRANT SELECT ON TABLE cashflow.instance TO leobloom_hobson;
GRANT ALL ON TABLE cashflow.instance TO sonofleo_dev;
GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.instance TO sonofleo_migrator;

create table cashflow.invoice
(
    unique_id uuid NOT NULL primary key,
    instance_id uuid not null references cashflow.instance(unique_id) match simple on update no action on delete restrict,
    payment_agreement_id uuid not null references cashflow.payment_agreement(unique_id) match simple on update no action on delete restrict,
    invoice_date date not null,
    due_date date not null,
    amount numeric(12,2) not null,
    invoice_state varchar(25) COLLATE pg_catalog."default" not null,
    payment_state varchar(25) COLLATE pg_catalog."default" not null,
    posted_state varchar(25) COLLATE pg_catalog."default" not null,
    blocker_state varchar(25) COLLATE pg_catalog."default",
    blocker_note varchar(500) COLLATE pg_catalog."default",
    memo varchar(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
) tablespace pg_default;
ALTER TABLE cashflow.invoice OWNER to sonofleo_dev;
REVOKE ALL ON TABLE cashflow.invoice FROM leobloom_hobson;
GRANT SELECT ON TABLE cashflow.invoice TO leobloom_hobson;
GRANT ALL ON TABLE cashflow.invoice TO sonofleo_dev;
GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.invoice TO sonofleo_migrator;

create table cashflow.payment
(
    unique_id uuid NOT NULL primary key,
    invoice_id uuid not null references cashflow.invoice(unique_id) match simple on update no action on delete restrict,
    journal_entry_header_id uuid not null references ledger.journal_entry(unique_id) match simple on update no action on delete restrict,
    posted_to_fi_date date,
    memo varchar(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL,
    modified_at timestamp with time zone NOT NULL
) tablespace pg_default;
ALTER TABLE cashflow.payment OWNER to sonofleo_dev;
REVOKE ALL ON TABLE cashflow.payment FROM leobloom_hobson;
GRANT SELECT ON TABLE cashflow.payment TO leobloom_hobson;
GRANT ALL ON TABLE cashflow.payment TO sonofleo_dev;
GRANT TRUNCATE, INSERT, DELETE, SELECT, TRIGGER, UPDATE, REFERENCES ON TABLE cashflow.payment TO sonofleo_migrator;