/*
 executed manually in all 3 envs 6/22 08:51
 */
alter table ledger.account
    alter column active_begin type date, -- REQ-AC-1.42
    alter column active_end type date -- REQ-AC-1.43
    ;