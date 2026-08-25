# resolved-findings-staleness-auditor

_No findings._

## Reasoning

Reviewed all 30 resolved findings (22 overruled, 2 deferred) against the current state of all 10 behavioral specs, Definitions.md, DB migrations, and source code. Each ruling was checked for three staleness criteria: (1) referenced requirement withdrawn, renumbered, or materially rewritten; (2) scoped to a passed project phase; (3) scope so broad it could suppress unintended findings.

**Requirement-level verification (criterion 1):**
Every REQ ID cited in a ruling was confirmed still active and materially unchanged in the current spec. Specifically: REQ-MON-2.2/2.4/2.7/2.9 (CV-2, CV-4, AMB-13, MON-2, MON-3); REQ-DAL-1.16/2.1/2.2/2.3/3.2.1/3.6 (AMB-4, AMB-5, AMB-11, AMB-DAL-01, CON-DAL-02, IE-2); REQ-SYS-2.1.1/3.1/3.3/5.1 (AMB-6, SYS-CLK-1, GAP-JE-2, JE-COMPOSITE-ORDER); REQ-AC-1.18/1.39/3.9/4.4 (IE-4, DEC-3, IE-AC-1, AMB-AC-2); REQ-JE-1.11/1.40/1.44/1.45/3.6.1/4.9 (AMB-JE-1, AMB-JE-3a, IDIOM-JE-1); REQ-NGUI-3.1-3.5 (WAIVE-1). All 19 DAL requirements are still either waived or unenforceable (16 + 3 = 19), confirming DAL-EFFICACY's factual basis. Verified fiscal_period_id FK on journal_entry (AMB-JE-1) and created_at/modified_at on journal_entry_ext_reference (GAP-JE-2) against the migration SQL. Definitions.md now explicitly encodes DB-STAGE-1's conclusion (staged entries and staged lines are not entities), strengthening rather than weakening that ruling.

**Phase-scoping check (criterion 2):**
No ruling is scoped to a project phase. The two deferred findings (IE-4, GAAP-CLOSE) have revisit triggers ("GAAP closing entries designed" / "Dan schedules closing-entries slice"). No closing-entries spec exists; the triggers remain unmet.

**Breadth-of-scope check (criterion 3):**
Examined each ruling for scope that could suppress unintended findings. Each ruling has a Scope line limiting its applicability to a specific requirement or question. Where ruling text uses broader language (AMB-5: "not setting a precedent"; SS-3: "todo remarks should not be evaluated as any sort of stand-alone directive"), the Scope line constrains the suppression zone. WAIVE-1's "do not re-flag waiver reasons without understanding Dan's usage of the phrase" is a conduct directive, not a blanket suppression -- it says "understand first," not "never flag." DAL-EFFICACY explicitly carves out non-test-efficacy audits from its scope. No ruling was found whose scope extends materially beyond the specific finding it addresses.

**New domains considered:**
Three behavioral specs (DataIngestion.md, ClassificationRuleCrud.md, Reporting.md) were added after most rulings were written. None of them introduce requirements that conflict with or invalidate any existing ruling. The conventions-without-reqs conduct article mentions temporal guidance for import/staging "whose domain doesn't exist yet" -- that domain now exists, but this is a conduct article observation, not a resolved finding, and no resolved finding depends on the staging domain being absent.
