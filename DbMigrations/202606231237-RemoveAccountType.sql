/*
 executed manually in all 3 envs 2026-06-23 12:37
 */
alter table ledger.account drop constraint account_account_type_id_fkey;
alter table ledger.account alter account_type_id type varchar(20);
alter table ledger.account rename column account_type_id to account_type;
drop table ledger.account_type;