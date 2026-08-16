# BD Handoff — Data Ingestion Tests

Branch: `data-ingestion`. Start there.

74 REQ-STG requirements need tests. All are new — zero coverage exists today.

## Existing test fixtures

`Tests/Tests.Helpers/TestDataStage.fs` already creates:
- Classification rules (3 seeded rules with various field match patterns)
- Ingestion sources (`testBankSource`)
- Stage entries for dedup testing:
  - `duplicateStageEntry` — same source + ref as a posted JE (`jeWithRef`, ref `TXN-001`)
  - `stageEntryWithSameAmountDateAndDescButDiffRef` — same everything except ref (unique)
  - `stageEntryWithSameRefDiffOtherStuff` — same source + ref, different date/amount/description (still a duplicate)

Build on these. Don't reinvent them.

## Slices and what to watch for

### Slice 1 — File format and ingestion (§1, §2, §3)

REQs: 1.1–1.15, 2.2–2.9, 2.12–2.17, 2.20–2.23, 3.1–3.10

The entry point is `StageEntryOrchestration.ingestRawToStageThenDeduplicateAndClassify`. It takes a `SourceFile` and a `BaseStageRawRow list`. For format validation tests, construct invalid `BaseStageRawRow` lists and confirm rejection.

Key behaviors:
- All-or-nothing ingestion (3.3, 3.10) — one bad record rejects the whole file
- Group validation (1.13, 1.14, 1.15) — inconsistent header fields, single-record groups, imbalanced debits/credits
- Source resolution (3.6) — unknown `fi_source` rejects the file
- Account code validation (3.7) — non-null code that doesn't exist rejects the file

### Slice 2 — Status lifecycle (§4)

REQs: 4.1–4.5

Test `validTransitions` in `StageEntryStatusTransition`. The full state machine:
- `Posted` is terminal — no transitions out
- Every transition creates an audit record
- `Ignored` entries count as dedup matches (4.5)

### Slice 3 — Classification (§5)

REQs: 5.1–5.8

The classifier is pure (`Classifier.classify`) and the orchestration is in `StageEntryOrchestration.classifyStagedEntries`.

**Watch for these:**
- Lines with non-null `account_code` are skipped — the classifier cannot override parser assignments (5.3). This is the authority hierarchy: parser > classifier > operator.
- Fully parser-assigned entries (all lines have codes) skip classification entirely and transition straight to `Classified` (5.8). No `MatchCandidate`s are created for them.
- Entry-level status is the worst outcome across its lines: any `ManyMatchesTied` → `Conflict`, else any `NoMatch` → `NoMatch`, else `Classified`.
- Priority: lower int wins. `ManyMatchesClearWinner` (one rule has strictly lower priority) is treated as a match, not a conflict.

### Slice 4 — Manual review (§6)

REQs: 6.1–6.3

The route is `UpdateStageEntry`. REQ-STG-6.2 was rewritten — status is NOT auto-assigned on manual update. The operator sets it explicitly. The system validates the composite after update (balanced entry, valid codes, legal transition).

- Override duplicate → `Reviewed` (6.3)
- Override any line's account_code regardless of who set it (6.1)

### Slice 5 — Deduplication (§7)

REQs: 7.1–7.3, 7.5

The dedup key is `source_id + fi_reference`. Nothing else participates.

**The dedup query uses CTEs and joins across `ingestion` and `ledger` schemas:**
- Stage-vs-ledger: matches against `ledger.journal_entry_ext_reference` (excludes voided JEs)
- Stage-vs-stage: matches against earlier staged entries (by first audit timestamp, ascending)
- Candidates exclude `Duplicate`, `Posted`, `Ignored` statuses

Test fixtures already cover the three cases (exact dup, same-everything-different-ref, same-ref-different-everything). Add a voided-JE case to confirm voided entries don't block re-import.

7.5: flagging as duplicate must not alter lines or account assignments.

### Slice 6 — Shadow post and batch post (§8, §9)

REQs: 8.1–8.4, 9.1–9.5, 9.7–9.9

Same code path, different transaction disposition.

**Shadow post (8.x):**
- Constructs JEs through the real domain model (8.2) — if it would fail on real post, it fails on shadow
- Returns before and after trial balance (8.3)
- Rolls back — no ledger changes, no staging status changes (8.1, 8.4)

**Batch post (9.x):**
- One JE per staged entry (9.9)
- Account code → account ID resolution at posting time (9.4)
- External reference constructed from source name + fi_reference (9.5)
- Status → `Posted` with `LedgerPoster` mechanism (9.7)
- All-or-nothing (9.8)

**To test shadow post rollback:** run shadow post, then query the ledger — the JEs should not exist. Query staging — statuses should be unchanged. The trial balance before and after should differ by the impact of the postable entries.
