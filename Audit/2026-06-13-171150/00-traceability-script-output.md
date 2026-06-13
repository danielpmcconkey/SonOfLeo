# Traceability Script Output

```
=== Invariant 1: phantom references (code/tests -> nonexistent or withdrawn requirement) ===
WITHDRAWN: REQ-AC-1.25 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:100:            let createdAt =  now // REQ-AC-1.25, REQ-AC-2.11
    DbMigrations/2026-06-01-07-48-CreateAccountTable.sql:47:    created_at timestamp with time zone NOT NULL DEFAULT now(),                  -- REQ-AC-1.25
WITHDRAWN: REQ-AC-1.26 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:101:            let modifiedAt = now // REQ-AC-1.26, REQ-AC-2.12
    DbMigrations/2026-06-01-07-48-CreateAccountTable.sql:48:    modified_at timestamp with time zone NOT NULL DEFAULT now(),                 -- REQ-AC-1.26
WITHDRAWN: REQ-AC-2.1 is referenced but withdrawn:
    Src/Model/Ledger/AccountComponent.fs:33:            let trimmed = raw.Trim() // REQ-AC-2.1
    Src/Model/Ledger/AccountComponent.fs:46:            let trimmed = raw.Trim() // REQ-AC-2.1
WITHDRAWN: REQ-AC-2.11 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:100:            let createdAt =  now // REQ-AC-1.25, REQ-AC-2.11
WITHDRAWN: REQ-AC-2.12 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:101:            let modifiedAt = now // REQ-AC-1.26, REQ-AC-2.12
WITHDRAWN: REQ-AC-2.15 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:227:                insert into ledger.account( -- REQ-AC-2.15
    Src/Model/Ledger/Account.fs:239:                values ( --  REQ-DAL-2.1, REQ-AC-2.15
WITHDRAWN: REQ-AC-3.1 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:157:                let! validCode = codeResult // REQ-AC-3.1
    Src/Model/Ledger/Account.fs:158:                let! validName = nameResult // REQ-AC-3.1
    Src/Model/Ledger/Account.fs:159:                let! validType = typeResult // REQ-AC-3.1
    Src/Model/Ledger/Account.fs:160:                let! validSubType = subTypeResult // REQ-AC-3.1
    Src/Model/Ledger/Account.fs:161:                let! validRef = referenceResult // REQ-AC-3.1
WITHDRAWN: REQ-AC-4.16 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:324:                 * to already have descendents. And, since requirement REQ-AC-4.16 explicitly forbids
WITHDRAWN: REQ-AC-4.21 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:472:                let! validAccountName = AccountName.create newName // REQ-AC-4.21
    Src/Model/Ledger/Account.fs:483:                let! validRef = // REQ-AC-4.21
WITHDRAWN: REQ-AC-4.7 is referenced but withdrawn:
    Src/Model/Ledger/Account.fs:371:                { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-AC-4.7 
    Src/Model/Ledger/Account.fs:397:	                        modified_at = @modified -- REQ-AC-4.7
UNKNOWN: REQ-AC-001 is referenced but defined nowhere:
    Tests/Tests.Ledger/AccountCrud.fs:6:let ``REQ-AC-001 creating an account with valid data succeeds`` () =

=== Invariant 2: active requirements with no test and no waiver ===
REQ-AC-1.1
REQ-AC-1.10
REQ-AC-1.11
REQ-AC-1.12
REQ-AC-1.13
REQ-AC-1.14
REQ-AC-1.15
REQ-AC-1.16
REQ-AC-1.17
REQ-AC-1.18
REQ-AC-1.19
REQ-AC-1.2
REQ-AC-1.20
REQ-AC-1.21
REQ-AC-1.22
REQ-AC-1.23
REQ-AC-1.28
REQ-AC-1.29
REQ-AC-1.3
REQ-AC-1.30
REQ-AC-1.31
REQ-AC-1.32
REQ-AC-1.33
REQ-AC-1.34
REQ-AC-1.35
REQ-AC-1.36
REQ-AC-1.37
REQ-AC-1.39
REQ-AC-1.4
REQ-AC-1.40
REQ-AC-1.41
REQ-AC-1.42
REQ-AC-1.43
REQ-AC-1.44
REQ-AC-1.45
REQ-AC-1.46
REQ-AC-1.47
REQ-AC-1.48
REQ-AC-1.48.1
REQ-AC-1.49
REQ-AC-1.5
REQ-AC-1.6
REQ-AC-1.7
REQ-AC-1.8
REQ-AC-1.9
REQ-AC-2.10
REQ-AC-2.13
REQ-AC-2.14
REQ-AC-2.16
REQ-AC-2.18
REQ-AC-2.4
REQ-AC-2.6
REQ-AC-2.7
REQ-AC-2.8
REQ-AC-2.9
REQ-AC-3.2
REQ-AC-3.3
REQ-AC-3.4
REQ-AC-3.5
REQ-AC-3.6
REQ-AC-4.1
REQ-AC-4.19
REQ-AC-4.2
REQ-AC-4.3
REQ-AC-4.4
REQ-AC-4.5
REQ-AC-4.6
REQ-AC-4.8
REQ-AC-4.9
REQ-DAL-1.1
REQ-DAL-1.10
REQ-DAL-1.11
REQ-DAL-1.12
REQ-DAL-1.13
REQ-DAL-1.2
REQ-DAL-1.3
REQ-DAL-1.4
REQ-DAL-1.5
REQ-DAL-1.6
REQ-DAL-1.7
REQ-DAL-1.8
REQ-DAL-1.9
REQ-DAL-2.1
REQ-DAL-2.2
REQ-DAL-2.3
REQ-DAL-3.1
REQ-DAL-3.2
REQ-DAL-3.2.1
REQ-DAL-3.2.2
REQ-DAL-3.3
REQ-DAL-3.4
REQ-DAL-3.5
REQ-DAL-3.6
REQ-SYS-1.1
REQ-SYS-1.2
REQ-SYS-1.3
REQ-SYS-2.1
REQ-SYS-2.2
REQ-SYS-3.1
REQ-SYS-3.2
REQ-SYS-3.3
REQ-SYS-5.1
(102 of 105 active requirements)

=== Invariant 3: active requirements with no code annotation (spec precedes code; FYI) ===
REQ-AC-1.47
REQ-AC-1.5
REQ-AC-4.19
REQ-AC-4.22
REQ-AC-5.1
REQ-DAL-1.8
REQ-DAL-3.2.1
REQ-DAL-3.2.2
REQ-DAL-3.6
REQ-SYS-1.1
REQ-SYS-1.2
REQ-SYS-1.3
REQ-SYS-2.1
REQ-SYS-2.2
REQ-SYS-3.2
REQ-SYS-3.3
REQ-SYS-5.1
(17 of 105 active requirements)

=== Invariant 4: test annotations per requirement, descending ===
      1 REQ-AC-001
```
