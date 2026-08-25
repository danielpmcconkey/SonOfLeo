# gaap-domain-auditor

## AMB-STG-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md (REQ-STG-7.3), Src/Model/DataIngestion/StageEntryHeader.fs (fetchDuplicates, lines 290-342)
- **Summary:** REQ-STG-7.3 says "a posted journal entry in the ledger" but the dedup implementation excludes voided journal entries from the ledger-side check; the spec does not address the voiding interaction.
- **Resolution:** dan-decides

The fetchDuplicates function builds an all_in_ledger CTE that filters with `where je.voided_at is null`, excluding voided JEs from the dedup match set. REQ-STG-7.3 states: "A staged entry is flagged as duplicate when a posted journal entry in the ledger carries an external reference whose financial_institution and reference values match the staged entry's source and fi_reference." Per GAAP terminology (and per this project's own Definitions.md and the domain-terminology-is-precise audit conduct article), "posted" means recorded to the ledger. Voiding is a soft-delete marker that excludes lines from balance computations but does not un-post the entry — the entry remains in the ledger. The spec text therefore includes voided entries in the dedup check, but the code excludes them.

The code's behavior is operationally reasonable: it enables re-importing a transaction after voiding a bad JE created from it outside the staging pipeline (e.g., posted manually via the CLI). Without the exclusion, such re-imports would be permanently blocked by the voided JE's external reference. However, the spec does not authorize or document this exclusion. The spec says "posted journal entry" — full stop.

The practical impact is narrow because the staged-to-staged dedup (REQ-STG-7.2) independently catches re-imports when the original staged entry still exists with status Posted. The void exclusion only changes behavior when a JE was created outside the staging pipeline (or if staged entries were somehow lost) and then voided.

**Action:** Decide whether voided JEs should participate in the ledger-side dedup. If yes (matching the current spec text), remove the `where je.voided_at is null` clause from the all_in_ledger CTE. If no (matching the current code, which has good operational reasons), amend REQ-STG-7.3 to read "a non-voided posted journal entry in the ledger" and add a brief design-note explaining the rationale (re-import-after-void workflow).

**Why:** The spec and code disagree on the scope of the ledger-side dedup check. Left unresolved, the spec could be read as requiring a behavior the code deliberately avoids, creating ambiguity about whether the code is correct or the spec is authoritative. Documenting the void exclusion in the spec also ensures that future readers and auditors understand the intentional interaction between dedup and voiding.

---
