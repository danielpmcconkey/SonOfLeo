# SystemWide spec auditor

## SYS-TS-1 — contradiction
- **Location:** Specs/Behavioral/SystemWide.md REQ-SYS-3.1 (waived); Specs/Definitions.md Entity, Insert-only log records; DbMigration/Scripts/202610041020-CreatePositionsTables.sql
- **Summary:** Four Positions tables that user actions write have no created_at or modified_at column. Under the Definitions' per-table Entity test they are entities, so this breaks REQ-SYS-3.1, which is waived and so has no test to catch it.
- **Resolution:** dan-decides

REQ-SYS-3.1: "Every persisted entity must carry a 'created at' and a 'modified at' timestamp." Definitions.md defines Entity table by table: "Two litmus questions for any table: (1) does any user action ever insert or update a row? Yes -> entity." The only exemptions are named ones: staged entry, staged line, and 'Insert-only log records', which lists exactly classification.rule_match and ingestion.staged_entry_audit.

The new migration creates four tables without either timestamp column:
- positions.account_snapshot_line (columns: unique_id, account_snapshot_id, holding_id, quantity, price, market_value, reported_cost_basis)
- positions.investment_account_owner
- positions.property_owner
- positions.property_mortgage_account

User operations write all four: snapshot record/replace (REQ-POS-7.1/7.2) and owner/mortgage set updates (REQ-POS-11.3/11.6). By the per-table test they are entities, and none is on the exemption list.

Earlier precedent in the repo goes the other way. ledger.journal_entry_line has its own created_at/modified_at. So does cashflow.payment_agreement_link, which is a link table.

Positions.md §1 lists 'Account Snapshot' as the entity and treats its lines as part of it. A developer reading Positions.md would leave timestamps off the line table, as the code does. A developer applying the Definitions test, or following the journal_entry_line and payment_agreement_link precedent, would add them. Definitions.md ranks above Positions.md and the code, so the code is out of step with the higher authority unless Dan amends one or the other.

REQ-SYS-3.1 is waived ('you can't test that there isn't a violation'). Nothing has flagged this, although a schema-level check would have found it.

Not the same point as DB-STAGE-1 in resolved-findings.md. That ruling rests on staged entries and lines being explicitly defined as non-entities in Definitions.md. No such definition exists for the Positions tables.

**Action:** Dan decides one of two things. Either add created_at/modified_at to the four tables (a new migration). Or amend Definitions.md: say that rows holding a component of an entity (lines that are replaced together with their parent, owner/link sets given as a complete set) are part of the parent entity, not entities in their own right, and so outside REQ-SYS-3.1. Taking the second route also means deciding whether journal_entry_line and payment_agreement_link keep their timestamps by choice or by rule.

**Why:** REQ-SYS-3.1 is meant to hold for every entity, and the Entity definition is what decides which tables it covers. If the two disagree in silence, each new domain will settle the question differently. This slice already departs from the journal-line and agreement-link precedent, and the waiver means no test will show it.

---

## SYS-NF-1 — ambiguity
- **Location:** Specs/Behavioral/SystemWide.md REQ-SYS-6.2 (and REQ-SYS-6.1); Positions.md REQ-POS-11.8, Addressing note; Person.md REQ-PER-2.2/2.4
- **Summary:** REQ-SYS-6.2 limits the system-wide not-found rule to records 'identified by ID'. The new domains address records by name or by name plus date, so it is unclear whether the rule covers them, and the one test of a natural-key miss cites REQ-SYS-6.1 instead.
- **Resolution:** fix-spec

REQ-SYS-6.2: "An operation that updates or deletes a record identified by ID, where no record has that ID, fails with a typed not-found error naming the kind of record and the ID."

The slice addresses records differently:
- Person.md and Positions.md ('Addressing') address Persons, Dimension Values, Securities, Investment Accounts and Properties by name at the boundary.
- Account Snapshots are addressed by account plus date (REQ-POS-7.4).
- Valuations are addressed by property plus date (REQ-POS-11.8, 'a means to delete a Valuation').

REQ-PER-2.4, REQ-POS-11.9 and REQ-POS-7.4 each restate a not-found rule for their own case, which suggests the authors did not read REQ-SYS-6.2 as reaching natural keys. No requirement covers deleting a Valuation for a date the Property has no Valuation on:
- REQ-POS-11.9 covers only an unknown Property name.
- REQ-SYS-6.2 covers only a missing ID.

The code still returns a typed error, PositionsValuationDoesntExist (Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs, deleteValuation). Its test, Tests/Tests.Integrated/CrossDomainOrchestration/PropertyMaintenance.fs:472, is named 'REQ-POS-11.8 REQ-SYS-6.1 deleting a Valuation for a date on which the Property has none fails…'. It cites REQ-SYS-6.1, the no-op rule for state transitions, rather than REQ-SYS-6.2. So the implementer had to choose which system-wide rule applies to a missing natural-key record, and chose a different one from the not-found rule that exists for exactly this behaviour.

Two reasonable developers would differ on whether REQ-SYS-6.2 applies to natural-key addressing.

**Action:** Amend REQ-SYS-6.2 to say 'a record identified by ID or by the key the interface addresses it by (a name, or a parent and a date)', with the error naming that key. Then move the Valuation-delete test's citation from REQ-SYS-6.1 to REQ-SYS-6.2.

**Why:** REQ-SYS-6.2 exists so that every missing-record case surfaces as a typed, actionable error. Since this slice, the interface addresses most records by name. Scoped to IDs, the rule no longer covers how the system is actually used, and each domain has to restate it or leave the gap open, as Valuation delete does now.

---

## SYS-WAIVE-1 — other
- **Location:** Specs/Behavioral/SystemWide.md Waived table, REQ-SYS-2.1.2
- **Summary:** The waiver for REQ-SYS-2.1.2 was approved on 2026-07-06. The requirement was amended on 2026-10-03 to add a typed not-found clause, which the approval and its reason do not cover.
- **Resolution:** dan-decides

The waiver row reads: 'REQ-SYS-2.1.2 | it's too general for a test and you can't test that there isn't a violation | Dan, 2026-07-06'. The requirement now ends with: 'The deliberate exceptions are a missing record (REQ-SYS-6.2) and a missing referent (REQ-SYS-6.3), which fail with typed not-found errors. (Amended 2026-10-03)'. That added sentence is concrete and testable; REQ-SYS-6.2 and 6.3 are tested in many places, for example CashFlowMaintenance.fs and ClassificationRuleCrud.fs. The waiver reason ('too general', 'can't test that there isn't a violation') fits the original permissive first sentence. It does not fit the amended exception clause. No re-approval dated after the amendment is recorded.

**Action:** Dan either re-dates and re-words the REQ-SYS-2.1.2 waiver to say the exception clause is verified through the REQ-SYS-6.2/6.3 tests, or moves the exception clause out of REQ-SYS-2.1.2, leaving it as a cross-reference to 6.2/6.3, so the waived text stays purely general.

**Why:** Every active requirement must be exactly one of tested, waived or unenforceable, and a waiver is an approval of specific text. Amending waived text without re-approving the waiver leaves a testable obligation sitting under a waiver nobody granted for it.

---


