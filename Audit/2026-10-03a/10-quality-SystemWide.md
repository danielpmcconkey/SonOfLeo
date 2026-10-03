# SystemWide spec auditor

## SYS-3STATE-1 — enforcement-gap
- **Location:** Specs/Behavioral/SystemWide.md line 48, REQ-SYS-6.1.1
- **Summary:** REQ-SYS-6.1.1 is active but has no citing test, no waiver and no Unenforceable entry, so it breaks the three-state rule and is the only cause of the check-traceability failure.
- **Resolution:** dan-decides

REQ-SYS-6.1.1 says: 'Any exception to REQ-SYS-6.1 ... must be stated explicitly in the relevant entity spec; absent such a statement, the no-op rejection applies.' Running grep for 'REQ-SYS-6.1.1' across Tests/ finds nothing. The Waived table (lines 69-75) lists 3.1, 2.1.1, 2.1.2, 6.1 and 2.1, not 6.1.1. The Unenforceable table (lines 81-83) is an empty placeholder row. Checks/run-all.sh check-traceability fails Invariant 2 on exactly this ID. The first half of the requirement tells spec authors what to write (entity specs such as REQ-FP-2.7 and REQ-STG-4.6 do state their exceptions). The second half ('absent such a statement, the no-op rejection applies') only repeats REQ-SYS-6.1, which is waived.

**Action:** Add REQ-SYS-6.1.1 to the Unenforceable table, with the reason that it binds spec authors rather than code. Alternatively, have Dan approve it as a waiver.

**Why:** Under Specs/README.md every active REQ must be tested, waived or unenforceable. One unclassified REQ makes the mechanical baseline fail, and that baseline is what Dan relies on to accept agent-written work.

---

## SYS-6.1-NOOP-1 — contradiction
- **Location:** Specs/Behavioral/SystemWide.md line 47 (REQ-SYS-6.1) vs CashFlow.md REQ-CF-14.2 (l.326-327), DataIngestion.md REQ-STG-6.3.2 (l.215-216), ClassificationRuleCrud.md REQ-CR-6.2, JournalEntryCrud.md REQ-JE-5.7
- **Summary:** The wording of REQ-SYS-6.1 no longer matches how the entity specs apply it to updates: SYS-6.1 rejects any request that 'would change nothing', but the entity specs reject only requests that name no field, and they do not call this an exception under REQ-SYS-6.1.1.
- **Resolution:** fix-spec

REQ-SYS-6.1 applies to 'state-transition operation[s]' and says: 'When a requested operation would change nothing ... the operation must produce an error.' Read literally, an update that sets every field to the value it already holds changes nothing and must be rejected. The 2026-10-03 amendments go the other way. REQ-CF-14.2 says 'An update that names no field to change is rejected (REQ-SYS-6.1); a field set to the value it already holds counts as named', and its *Why* says 'an unchanged value is not a no-op'. REQ-STG-6.3.2 repeats this 'per REQ-SYS-6.1'. REQ-CR-6.2 and REQ-JE-5.7 likewise define a no-op as all fields NoChange. So the entity specs cite SYS-6.1 for a narrower rule than SYS-6.1 states. They also apply it to plain field updates, which the 'state-transition operation' scope does not obviously cover, and none of them presents the narrowing as a REQ-SYS-6.1.1 exception. The rationale exists only in CF-14.2. Entity specs that do not state the narrower rule fall back to REQ-SYS-6.1, and for them two developers would build different behaviour. For example, for an Account or journal-entry header update where every field equals the stored value, one developer rejects (literal SYS-6.1) and another accepts (following CF-14.2 by analogy).

**Action:** Amend REQ-SYS-6.1 to say what counts as a no-op for an update: an update that names no field to change. Say that re-sending a stored value is a named change, and that field updates are in scope. Move the CF-14.2 rationale into SystemWide.md so the entity specs can cite it instead of redefining it.

**Why:** The system-wide policy is the default for every entity that does not state its own rule. If the policy's text and its accepted reading differ, each agent developer has to guess which one applies, and the answer can be observed through the CLI (an error or a success).

---

## SYS-3.4-SCOPE-1 — ambiguity
- **Location:** Specs/Behavioral/SystemWide.md line 30, REQ-SYS-3.4; Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs:45; Src/Ui.InterfaceBridge/ReportVisualizationAssets/ReportFooter.fs:7; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:89
- **Summary:** REQ-SYS-3.4 does not say whether 'every timestamp the operation writes' and 'every current date it derives' cover output that is not a database record, such as report dates, report footers and processed-file names. The code reads the clock separately in all three places.
- **Resolution:** dan-decides

REQ-SYS-3.4 says every operation has one initiation instant and that 'Every timestamp the operation writes, and every "current date" it derives ... uses that instant.' Three places derive time outside the operation's Context/AuditEnvelope. (1) The prePostingReview route builds a Context, yet ReportRoutes.fs:45 passes `Calendar.today()` to PrePostingReviewWriter.write instead of the date of `Context.getInitiationInstant context`. (2) ReportFooter.fs:7 `createReportFooter ()` calls `Clock.now()` for the 'created' timestamp printed on every report (it is used by all four writers). (3) IngestionRoutes.fs:89 names the processed file with `Clock.now()`, not the ingest operation's initiation instant. If 'writes' means database writes only, (2) and (3) are allowed. Even then, (1) is a 'current date' the operation derives, and that clause is not limited to persistence. Reasonable developers read this differently, and the code already does both: most operations use the envelope instant, while these routes do not. No test cites REQ-SYS-3.4 for report or file output (4 citing tests in total).

**Action:** Dan decides whether REQ-SYS-3.4 covers file names and report output. Then either narrow the wording ('every timestamp the operation persists') or keep it broad and have the route and writer code take the date and instant from the Context.

**Why:** The REQ's stated purpose is 'date-dependent logic cannot straddle midnight partway through a run'. A pre-posting review whose header date comes from a second clock read can show a different date from the operation it reports on, which is exactly that straddle.

---

## SYS-3.1-RULEMATCH-1 — contradiction
- **Location:** Specs/Behavioral/SystemWide.md line 27, REQ-SYS-3.1 (waived); DbMigration/Scripts/202609071135-CreateClassificationTables.sql (classification.rule_match)
- **Summary:** classification.rule_match has created_at but no modified_at. By the Definitions.md litmus test it is an entity, so it does not meet REQ-SYS-3.1, which is waived on the grounds that schema and code review enforce it.
- **Resolution:** dan-decides

REQ-SYS-3.1: 'Every persisted entity must carry a "created at" and a "modified at" timestamp.' The waiver relies on non-test enforcement (the Waived table header says 'enforced (by type system, code review, schema, or construction pattern)'). The migration defines classification.rule_match with unique_id, run_id, stage_entry_line_id, classification_rule_id and created_at only. Every other entity table in the ledger, cashflow and classification schemas, plus ingestion.source, has both columns. Definitions.md, Entity: 'does any user action ever insert or update a row? Yes → entity.' Rule-match rows are inserted by user-triggered classification runs (REQ-STG-5.10), and the table cannot be regenerated from spec and code alone. Definitions.md explicitly exempts staged entries and staged lines from entity-level policies (precedent DB-STAGE-1). It has no such exemption for rule_match, so the waiver's assumed enforcement missed this table.

**Action:** Dan decides one of two options: (a) add a Definitions.md carve-out that treats rule_match (an append-only classification run record) as a non-entity, as was done for staged entries; or (b) add modified_at to classification.rule_match through a migration.

**Why:** A waiver is acceptable only while the enforcement it names actually holds. Here schema review let a gap through, and no test exists to catch it.

---

## SYS-2.1.2-ERR-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/SystemWide.md lines 21-22 (REQ-SYS-2.1.1, 2.1.2), line 89 (Withdrawn REQ-SYS-2.2), lines 49-51 (REQ-SYS-6.2, 6.3)
- **Summary:** When REQ-SYS-2.2 was withdrawn 'for clarity', its 'must produce a meaningful error message' clause was dropped. REQ-SYS-2.1.2 now lets database-state rejections fall through to raw database constraints with no requirement on the error, while REQ-SYS-6.3 forbids exactly that for missing referents.
- **Resolution:** dan-decides

Withdrawn REQ-SYS-2.2: 'Rejections under REQ-SYS-2.1 must occur before any database write, and must produce a meaningful error message.' The withdrawal reason is 'replaced with 2.1.1 and 2.1.2 for better clarity'. 2.1.1 carries over the before-write half for property-determinable rejections. 2.1.2 ('Rejections requiring database state may fall through to database constraints') says nothing about the error the caller receives. The withdrawal therefore changed substance, not just clarity. REQ-SYS-6.3 then requires a typed not-found error before any write for missing referents, which are a database-state rejection, because 'Relying on the database's foreign key check produces an error the caller cannot act on.' REQ-SYS-6.2 likewise forbids generic database or row-count errors for missing IDs. 2.1.2 does not acknowledge these carve-outs. For other database-state rejections, such as uniqueness (REQ-AC-2.9, REQ-FP-2.2, and the name uniqueness added in migration 202609271200-UniqueIngestionSourceName), one developer can let a raw unique-violation surface under 2.1.2 while another maps it to a typed domain error. The caller sees the difference.

**Action:** Amend REQ-SYS-2.1.2 to require that a database-state rejection reaches the caller as a typed, actionable error (not a generic database error), noting REQ-SYS-6.2 and 6.3 as specific instances. Alternatively, correct the SYS-2.2 withdrawal reason to say the meaningful-error clause was dropped on purpose.

**Why:** A withdrawal must not quietly open a gap. 'For clarity' tells future readers that nothing in substance was lost, but an obligation on error quality was.

---

## SYS-STALE-IEAC1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md IE-AC-1 vs Specs/Behavioral/SystemWide.md line 30 (REQ-SYS-3.4, 2026-09-26)
- **Summary:** Precedent IE-AC-1 says reads use Calendar.today() rather than the operation's instant 'by design'. REQ-SYS-3.4, added later, requires every derived 'current date', naming the account-activity reference date, to come from the operation's initiation instant.
- **Resolution:** dan-decides

IE-AC-1 (overruled 2026-07-06): 'AuditEnvelope is for mutations with audit timestamps. Reads use Calendar.today() ... The mechanism differs from mutation-path checks by design.' REQ-SYS-3.4 (2026-09-26): 'Every operation carries an auditable action ... and a single initiation instant ... every "current date" it derives (e.g. the reference date for account activity ...) uses that instant.' Read routes now build a Context with a FetchOnly action (e.g. ReportRoutes.fs), and the only Calendar.today() call left in Src is ReportRoutes.fs:45. The ruling therefore contradicts a higher authority, and a future auditor or agent could cite it to justify a clock read that REQ-SYS-3.4 now forbids. REQ-AC-3.9 ('the calendar date associated to the system run time') keeps the older wording, while REQ-AC-2.7, 4.1 and 4.3 use 'the AuditEnvelope's system instant'.

**Action:** Mark IE-AC-1 in resolved-findings.md as superseded by REQ-SYS-3.4 (2026-09-26).

**Why:** The precedent ledger exists to suppress re-raised findings. A ruling that conflicts with a current requirement would suppress valid findings and could lead agents into non-compliant code.

---

## SYS-FMT-1 — maintainability
- **Location:** Specs/Behavioral/SystemWide.md lines 21-22, REQ-SYS-2.1.1 and REQ-SYS-2.1.2
- **Summary:** The bullets for REQ-SYS-2.1.1 and REQ-SYS-2.1.2 open with ** but never close it, so the REQ ID and the requirement text render as one bold run and do not follow the '- **REQ-ID** text' format used by every other REQ.
- **Resolution:** fix-spec

Line 21: '- **REQ-SYS-2.1.1 Rejections determinable from the entity's own properties must occur before any database write. ' (also has trailing whitespace). Line 22: '- **REQ-SYS-2.1.2 Rejections requiring database state may fall through to database constraints.' All other REQ bullets in this file and the other Behavioral specs use '**REQ-...**'. The traceability script happens to pick these IDs up today, but any tool that parses the closed-bold ID pattern will miss them.

**Action:** Add the closing ** after each ID on lines 21 and 22.

**Why:** Agents and scripts read REQ IDs from the spec by pattern. A malformed ID bullet can drop a requirement from mechanical traceability without anyone noticing.

---

