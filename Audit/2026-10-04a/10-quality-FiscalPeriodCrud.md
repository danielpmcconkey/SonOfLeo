# spec-quality:FiscalPeriodCrud

## FP-WAIVE-1 — stale-reference
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md line 67 (Waived table, REQ-FP-1.7)
- **Summary:** REQ-FP-1.7's waiver reason relies on a 'constructNew function' that does not exist, and a spec is not allowed to name source functions at all.
- **Resolution:** fix-spec

The waiver row reads: 'It's an impossible state to test, given the constructNew function always creates the UUID at runtime'. A grep of Src/ finds no function named constructNew. Creation goes through Business.CrossDomainOrchestration/FiscalPeriodCreation.fs `constructNewAndPersist`, which calls `FiscalPeriodId.create()` (Guid.NewGuid) in Business.FinancialServices.Ledger/FiscalPeriodComponent.fs. Specs/README.md 'Linkage rules' says: 'Spec documents never name source files, functions, or tests.' So the reason names a function, against the README, and the name it uses is out of date. The waiver itself still holds. What actually enforces uniqueness is the schema: `CONSTRAINT fiscal_period_pkey PRIMARY KEY (unique_id)` in DbMigration/Scripts/202609071105-CreateLedgerTables.sql. Per the check-schema-before-questioning-waivers article, that is valid grounds for the waiver. Only the stated reason is wrong.

**Action:** Reword the REQ-FP-1.7 waiver reason in operator terms without naming code, for example: 'Enforced by the primary-key constraint, and the system alone generates IDs (REQ-FP-2.1), so a duplicate cannot be produced through the interface to test against.'

**Why:** A waiver reason is the only record of why a requirement has no test. If it points to code that has been renamed, a future auditor cannot check the claim, and it breaks the README rule that keeps specs from going stale when code is refactored.

---

## FP-3STATE-1 — contradiction
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md REQ-FP-1.4, REQ-FP-1.5, REQ-FP-2.3 vs REQ-FP-2.3.1 (Waived table line 69)
- **Summary:** REQ-FP-1.4, 1.5 and 2.3 count as tested, but each repeats a clause that the waiver on REQ-FP-2.3.1 says cannot be tested.
- **Resolution:** dan-decides

REQ-FP-1.4 and REQ-FP-1.5 each end with 'It is not a caller-provided value.' REQ-FP-2.3 ends with 'The caller provides only the key.' REQ-FP-2.3.1 states the same rule on its own ('The system will not allow the creating actor to specify start and end dates'), and it is waived because 'You cannot test for the absence of something.' The single test citing 1.4/1.5/2.3 (Tests/Tests.Integrated/CrossDomainOrchestration/FiscalPeriodCreation.fs, 'REQ-FP-1.4 REQ-FP-1.5 REQ-FP-2.3 fiscal period runs from the first of the keyed month to its last day...') checks only the date derivation. It cannot check the 'not caller-provided' clause, and by the spec's own reasoning in the 2.3.1 waiver, no test could. So three requirements are counted as tested while carrying a clause the same document says is untestable. This breaks the README three-state rule, which is meant to apply to the whole of each requirement. It is the same pattern fixed at HEAD for REQ-POS-3.4/4.9 ('lose unreachable clauses').

**Action:** Remove the 'not a caller-provided value' and 'caller provides only the key' clauses from REQ-FP-1.4, REQ-FP-1.5 and REQ-FP-2.3, and leave that rule only in REQ-FP-2.3.1, which already holds it and is waived.

**Why:** When a requirement is marked tested, every clause in it should have been checked. If an untestable clause is repeated inside tested requirements, the traceability gate and future auditors take the tested status as covering a rule that nothing has verified.

---

