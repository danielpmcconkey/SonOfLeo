# stale-ruling-auditor

_No findings._

## Reasoning

Checked all 30 entries in resolved-findings.md against the current state of every behavioral spec in Specs/Behavioral/ (AccountCrud.md, ClassificationRuleCrud.md, DataAccessLayer.md, DataIngestion.md, FiscalPeriodCrud.md, JournalEntryCrud.md, Money.md, NonGraphicalInterface.md, Reporting.md, SystemWide.md) and Specs/Definitions.md.

For each ruling, I verified three things:

1. REQUIREMENT CONTINUITY: Every REQ ID cited by a ruling still exists as an active (non-stricken, non-withdrawn) requirement with materially the same wording. Specifically confirmed: REQ-DAL-1.16, 2.1, 2.2, 2.3, 3.2.1, 3.6; REQ-SYS-2.1.1, 3.3, 5.1, 6.1; REQ-MON-2.4, 2.7, 2.9, 2.9.1; REQ-AC-1.39, 3.9, 4.4; REQ-JE-1.11, 1.40, 1.44, 1.45, 2.5, 3.6.1, 4.9; REQ-NGUI-3.1-3.5. No renumbering, no withdrawal, no material rewrite.

2. PHASE/TRIGGER STALENESS: The two deferred findings (IE-4 and GAAP-CLOSE) have "revisit when" triggers that have not been met. IE-4 triggers on "GAAP closing entries (retained-earnings sweep) are designed" -- no such domain exists. GAAP-CLOSE triggers on "Dan schedules the closing-entries slice" -- FiscalPeriodCrud still has only the open/close toggle with no closing-entries machinery. Both deferrals remain valid.

3. SCOPE BREADTH: Examined each ruling's scope statement for overbreadth that could suppress unintended findings. The closest candidate was IE-2 (scope: "Whether REQs can contain non-assertable language") which generalizes from a DAL-3.6-specific finding, but the ruling text self-limits with "As long as the language doesn't create ambiguity or encourage test writers to write bullshit tests" -- so it would not suppress a legitimate ambiguity finding. WAIVE-1's closing admonition ("Do not re-flag waiver reasons without understanding Dan's usage of the phrase") is broader than its titled scope (REQ-NGUI-3.1-3.5) but functions as auditor guidance rather than a blanket suppressor. DAL-EFFICACY explicitly carves out its own exceptions ("This ruling covers test-efficacy only -- it does not suppress spec-quality, ambiguity, or contradiction audits"). No ruling was broad enough to suppress findings Dan never intended to rule on.

Code-level verification: Confirmed via grep that fiscal_period_id FK still exists on journal_entry (AMB-JE-1), Money.fromDecimal still exists (CV-2, CV-4), AcceptableExpectedRows/AnyQuantityIsAcceptable pattern still exists (CON-DAL-02), Calendar.today() is still the read-path instant source (IE-AC-1).

Definitions.md verification: The Staged entry and Staged line definitions now explicitly state they are not entities and entity-level policies do not apply, which is consistent with DB-STAGE-1's overrule.

Read all 12 audit conduct articles before evaluation. No conduct rule violations in my analysis.
