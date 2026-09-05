
alter table cashflow.payment_agreement
    add payment_agreement_name varchar(250) COLLATE pg_catalog."default" not null unique;
