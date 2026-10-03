# stale-ruling-auditor

## STALE-IE-AC-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:87-91 (IE-AC-1); Specs/Behavioral/SystemWide.md:30 (REQ-SYS-3.4); Src/Business.FinancialServices.Ledger/Account.fs:214
- **Summary:** IE-AC-1 rules that reads take their current date from Calendar.today(), not the AuditEnvelope, but REQ-SYS-3.4 (added 2026-09-26) now requires the opposite, and the code follows the new rule.
- **Resolution:** dan-decides

IE-AC-1 (2026-07-06) says: "Reads use Calendar.today() (Clock.now() through US Eastern Time). The mechanism differs from mutation-path checks by design." REQ-SYS-3.4 (2026-09-26) now says: "Every operation carries ... a single initiation instant read from the system clock when the operation begins. Every timestamp the operation writes, and every 'current date' it derives (e.g. the reference date for account activity ...), uses that instant." The code has moved to match. Account.fs:214 computes the active-account reference date (REQ-AC-3.9) as `context |> Context.getInitiationInstant |> Calendar.dateFromInstant`, and Context.fs:26 reads that instant from `AuditEnvelope.instant`. The same pattern appears in AccountDeactivation.fs:58, CashFlowOps.fs:34 and IngestionRoutes.fs:157. The ruling now endorses a design the spec has rejected. It would also suppress a real finding: Ui.InterfaceBridge/Routes/ReportRoutes.fs:45 still calls `Calendar.today()` for PrePostingReviewWriter, and IE-AC-1 would excuse that today.

**Action:** Retire IE-AC-1, noting that REQ-SYS-3.4 (2026-09-26) superseded it.

**Why:** A precedent that contradicts a current requirement lets auditors skip real REQ-SYS-3.4 violations.

---

## STALE-SYS-CLK-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:99-103 (SYS-CLK-1); Specs/Behavioral/SystemWide.md:29 (REQ-SYS-3.3)
- **Summary:** SYS-CLK-1 rules on REQ-SYS-3.3's "system clock at time of the update" wording, which was amended out on 2026-09-26.
- **Resolution:** dan-decides

SYS-CLK-1's scope: "Whether REQ-SYS-3.3's 'system clock' contradicts the AuditEnvelope decision." REQ-SYS-3.3 now reads: "...set its 'modified at' timestamp to the initiation instant of the operation performing the update (REQ-SYS-3.4). (Amended 2026-09-26 — was 'the system clock at time of the update')". The wording the ruling defends is gone, and the amended text adopts the auditor's position (one initiation instant). The ruling's tolerance ("all reasonable interpretations land within a second of each other, which is fine") also conflicts with REQ-SYS-3.4's stated Why: "One operation, one moment." Left in place, it could be read as permission for per-write clock reads, which REQ-SYS-3.4 now forbids.

**Action:** Retire SYS-CLK-1. The 2026-09-26 amendment of REQ-SYS-3.3 resolved the underlying question.

**Why:** A ruling on withdrawn wording invites misapplication to the new wording, whose intent is the opposite.

---

## STALE-MAINT-TZ-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:195-199 (MAINT-TZ-1); Src/App.Utility/Calendar.fs:9; Src/App.Utility/Clock.fs:9-12
- **Summary:** MAINT-TZ-1 defends a duplicated time-zone binding in Clock.fs and Calendar.fs that no longer exists; Calendar now depends on Clock.
- **Resolution:** dan-decides

The ruling says: "Each reads the configured time zone independently ... Coupling them to share a single binding would create a dependency between modules that currently have none." In current code only Clock.fs:9-12 binds `timeZoneLocal` from config. Calendar.fs:9 is `let dateFromInstant (i: Instant) : LocalDate = i.InZone(Clock.timeZoneLocal).Date`, and Calendar.today() calls Clock.now(). The modules now share one binding with a Calendar-to-Clock dependency, which is the arrangement the ruling called undesirable. REQ-SYS-7.1 (revised 2026-09-28) now also requires "one configured local time zone, system-wide." The facts and rationale the ruling rests on are both gone. The "Do not re-flag" instruction protects nothing, and its claim that the modules are independent would mislead an architecture or maintainability auditor reading the ledger.

**Action:** Retire MAINT-TZ-1. The duplication it defended has been removed and REQ-SYS-7.1 now requires a single time-zone binding.

**Why:** A precedent whose factual premise is false misdescribes the codebase to every auditor that reads the ledger before looking at the code.

---

## STALE-DAL-EFFICACY — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:165-169 (DAL-EFFICACY); Specs/Behavioral/DataAccessLayer.md:33-34 (REQ-DAL-2.4), 50-80 (waiver tables)
- **Summary:** DAL-EFFICACY says every active REQ-DAL is waived or unenforceable, but REQ-DAL-2.4 (added 2026-09-26) is active, not waived, and cited by 8 tests.
- **Resolution:** dan-decides

The ruling says: "19 active REQ-DAL requirements, but all 19 are either waived from testing or classified as unenforceable ... A test-efficacy auditor scoped to the DAL will always return 'no findings' because there are no tested REQ IDs to audit against. ... Do not flag the absence of DAL-specific efficacy findings." There are still 19 active REQ-DAL IDs (1.14-1.20, 2.1-2.4, 3.1-3.7 including 3.2.1), but the set has changed. REQ-DAL-2.4 ("Every database transaction ... is committed or rolled back, and its connection released, on every path") is new, appears in neither the waived table (15 IDs) nor the unenforceable table (3 IDs), and is cited 8 times under Tests/ (`grep -rn REQ-DAL Tests` returns only REQ-DAL-2.4). Commit 351fa19 also rewrote the REQ-DAL-2.4 rollback tests. The ruling's premise that nothing in the DAL is tested is false, and its "do not flag" instruction would suppress efficacy review of the one DAL requirement that has tests.

**Action:** Rewrite to: "DAL-EFFICACY: REQ-DAL-1.14–1.20, 2.1–2.3 and 3.1–3.7 (including 3.2.1) are waived or unenforceable and are validated transitively through domain tests. Do not flag the absence of efficacy findings for those IDs. REQ-DAL-2.4 is tested directly and is in scope for test-efficacy audit. This ruling does not suppress spec-quality, ambiguity, or contradiction audits against DataAccessLayer.md."

**Why:** A blanket "no tested REQs here" precedent gets more broadly wrong as the spec grows. Scoping it to explicit IDs keeps it accurate as tested requirements are added.

---

## STALE-CLAUDE-MD — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:153-157 (CLAUDE-MD); README.md:20; Skills/SonOfLeoRequirementsAudit/README.md:26,56
- **Summary:** CLAUDE-MD's rationale ("a CLAUDE.md at the SonOfLeo repo root would never load") rests on launch roots the repo's own README no longer describes.
- **Resolution:** dan-decides

The ruling says: "The harness launches from its own root (`~/penthouse-pete/` for Hobson, `~/` for BD); a CLAUDE.md at the SonOfLeo repo root would never load." The current README.md:20 says: "Agents are Claude Code sessions, usually in the cloud, each on its own clone of the repo." The audit skill's README.md:26 says it runs from "Hobson's, or a cloud session on a clone of the repo," and line 56 covers cloud-session clones. This audit session itself runs with the repo root (/home/claude/SonOfLeo) as its working directory. Commits d2217e8 and 42bf7b3 made that shift. Whether a repo-root CLAUDE.md is wanted is still Dan's call, and the ruling's conclusion may stand, but its stated mechanical reason is no longer true for the cloud sessions the README calls the usual case.

**Action:** If Dan still rejects a repo-root CLAUDE.md, rewrite the rationale on current grounds (for example, "agents get context from the wakeup or task prompt and the workflow's own instructions; a repo-root CLAUDE.md is not wanted") and drop the "would never load" claim and the `~/penthouse-pete/` and `~/` launch-root description. Otherwise retire the ruling.

**Why:** A ruling defended by a false technical premise will be challenged on that premise each round. A ruling that states Dan's actual preference will not.

---

## STALE-NGUI-AQ-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:177-181 (NGUI-AQ-1); Tests/Tests.Integrated/Reports/Program.fs:52-74; Tests/Tests.Integrated/SonOfLeoCli/Program.fs:42-51
- **Summary:** NGUI-AQ-1's description of how the Reports CLI stderr test works no longer matches the test.
- **Resolution:** dan-decides

The ruling says: "The Reports CLI handles this by appending a newline to the expected message and using Assert.Equal." The current Reports test (Reports/Program.fs:52-74, REQ-NGUI-1.3.1 / REQ-NGUI-4.4) compares only the first line, `Assert.Equal(expectedFirstLine, firstLine e)`, then checks for a stack trace with `Assert.Matches(@"(?m)^\s+at \S", e)`. A comment explains that frames after the first line come from the runtime. No newline is appended. The main-CLI test (SonOfLeoCli/Program.fs:42-51) still uses `Assert.Contains(expectedError, e)`, so the ruling's operative conclusion, that Contains is acceptable on the main CLI, still applies. Only its comparison to the Reports CLI is out of date.

**Action:** Rewrite the ruling to: "The main CLI's REQ-NGUI-1.3.1/3.7 stderr test uses Assert.Contains to confirm the full expected error message is present. The Reports CLI's REQ-NGUI-1.3.1/4.4 test asserts first-line equality plus the presence of a stack trace, because runtime frames after the first line vary. Both are adequate. Do not re-flag either for not matching the other."

**Why:** Auditors check precedents against code, and a precedent whose described mechanism has changed weakens trust in the rest of the ledger.

---

## STALE-AMB-4 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:26-30 (AMB-4); Specs/Behavioral/DataAccessLayer.md:32 (REQ-DAL-2.3); Src/Business.FinancialServices.Ledger/Account.fs:125-138
- **Summary:** AMB-4 cites a function that no longer exists (Account.fs readRowsFromDb), and its rationale has since been written into REQ-DAL-2.3 itself.
- **Resolution:** dan-decides

AMB-4 justifies keeping DAL-2.1 and DAL-2.3 separate by pointing to "the flexible multipurpose read pattern (Account.fs readRowsFromDb)." `grep -rn readRowsFromDb Src` finds nothing. The equivalent today is the private `query` function in Account.fs:125-138, which passes `limit: int option` to `buildReadQuery`. REQ-DAL-2.3 has also been reworded to carry the carve-out directly: "Values whose type makes injection structurally impossible (e.g. `limit: int option`, where F# enforces the type at compile time) may be interpolated directly." The ruling's conclusion (keep 2.1 and 2.3 separate) still fits the spec, but its code anchor is dangling and its rationale now duplicates the requirement text.

**Action:** Rewrite the ruling to: "AMB-4: REQ-DAL-2.1 (all inserted data parameterized) and REQ-DAL-2.3 (user-originated input parameterized, with type-safe structural values such as `limit: int option` allowed to be interpolated) are distinct. The distinction lets the shared read-query builder (`buildReadQuery`, used e.g. by Account.fs `query`) interpolate LIMIT. Do not propose consolidating them." Alternatively, retire it as absorbed into REQ-DAL-2.3's own wording.

**Why:** A precedent that points at a deleted symbol cannot be verified, and auditors told to verify before citing will discount it.

---

