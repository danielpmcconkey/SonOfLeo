# code-truthfulness

## CT-STG-1 — enforcement-gap
- **Location:** /media/dan/fdrive/codeprojects/SonOfLeo/Src/ModelOrchestrator/StageEntryOrchestration.fs, lines 752-758
- **Summary:** StageEntryOrchestration.fetchFiltered errors instead of returning an empty list when the filter matches zero staged entries.
- **Resolution:** fix-code

When the query at line 752 returns zero headers (AnyQuantityIsAcceptable allows this), headerIds is an empty list. Lines 756-757 then pass that empty list to StageEntryLine.fetchByHeaderIdList and StageEntryStatusTransition.fetchByHeaderIdList, both of which explicitly reject empty lists with Error IngestionStageHeaderIdListCannotBeEmpty (StageEntryLine.fs line 178, StageEntryStatusTransition.fs line 156). The caller (IngestionRoutes.fs line 208) does not guard against this either.

The other fetch-composite functions in the same file handle this correctly:
- fetchAllByFile (line 240): `if headers |> List.isEmpty then return [] else`
- fetchAllForPosting (line 253): `if headersToBePosted |> List.isEmpty then return [] else`

fetchFiltered is missing the same guard.

**Action:** Add `if headers |> List.isEmpty then return [] else` between line 752 and line 753, matching the pattern used by fetchAllByFile and fetchAllForPosting.

**Why:** A filtered query that matches nothing is a normal operational scenario (e.g., filtering by a status that has no entries, or a date range with no activity). The function should return Ok [] for zero matches, not an error. As written, the CLI would report an internal error to the operator instead of showing an empty result set.

---

## CT-STG-2 — contradiction
- **Location:** /media/dan/fdrive/codeprojects/SonOfLeo/Src/Model/DataIngestion/StageEntryHeader.fs, line 205; REQ-STG-7.3
- **Summary:** The fetchDuplicates query excludes voided journal entries from the ledger dedup check, narrowing REQ-STG-7.3 beyond what the spec permits.
- **Resolution:** dan-decides

The `all_in_ledger` CTE in fetchDuplicates (StageEntryHeader.fs line 198-205) includes `where je.voided_at is null`, which means a voided journal entry's external reference will not trigger a duplicate flag for a newly staged entry with the same source+fi_reference.

REQ-STG-7.3 states: 'A staged entry is flagged as duplicate when a posted journal entry in the ledger carries an external reference whose financial_institution and reference values match the staged entry's source and fi_reference.'

A voided JE is still 'a posted journal entry' by: (1) GAAP terminology -- voiding does not un-post; (2) the system's own design note in JournalEntryCrud.md: 'a voided entry remains in the ledger (it is never edited or hard-deleted)'; and (3) Definitions.md does not distinguish voided from non-voided for the concept of posting.

REQ-JE-4.7's exclusion of voided entries applies only to 'every balance, trial-balance, and account-sum computation.' Dedup is a membership check, not a computation. The code extends the void exclusion pattern to a domain where the spec does not call for it.

Note: the staged-entry-side dedup (ordinal > 1 in the partition) would still catch re-imports of transactions that were originally staged and posted through the pipeline, since the original staged entry with status 'Posted' participates in the row_number() partition. The gap is narrower than it first appears -- it only affects transactions that were posted directly via the CLI (no staged entry) and then voided.

**Action:** Either remove the `where je.voided_at is null` filter from the all_in_ledger CTE to match the spec, or amend REQ-STG-7.3 to read 'a non-voided posted journal entry' if excluding voided entries is the intended behavior. Dan decides which.

**Why:** The spec and code disagree on whether voided JEs participate in ledger-side dedup. If someone voids a directly-posted JE and then imports the same FI data, the current code would silently admit it rather than flagging it as a duplicate. Whether that is correct depends on business intent, but the spec should match the code either way.

---
