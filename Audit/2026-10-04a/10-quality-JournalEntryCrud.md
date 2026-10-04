# spec-quality-auditor:JournalEntryCrud.md

## JE-CONTRA-1 — contradiction
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-3.8 (line 105) vs REQ-JE-3.5 (line 98)
- **Summary:** REQ-JE-3.8 says it differs from REQ-JE-3.5 because it needs only the FI, but the 2026-09-26 amendment to REQ-JE-3.5 already allows a lookup by FI alone, so REQ-JE-3.8 is now fully covered by REQ-JE-3.5 and its distinguishing sentence is false.
- **Resolution:** fix-spec

REQ-JE-3.5 (amended 2026-09-26, 'Either-or lookup added') reads: 'retrieve the journal entries carrying a matching external reference, by the caller providing a source FI, a reference value, or both.' REQ-JE-3.8 reads: 'retrieve all journal entries carrying at least one external reference whose source FI matches a caller-provided value. Unlike REQ-JE-3.5, this requires only the FI — no reference value.' Since the amendment, a lookup by FI alone is one of REQ-JE-3.5's own modes. The 'Unlike REQ-JE-3.5' sentence describes the version before the amendment. The tests show the overlap. Both requirements are verified through one capability (fetchByReference in Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryFetching.fs). REQ-JE-3.5's own test (RevisedRequirementsLedger.fs:216, 'for each of a source FI alone and a reference value alone') already covers FI-only. JournalEntryFetching.fs:234 cites REQ-JE-3.8 for 'reference text only', which REQ-JE-3.8 does not describe. JournalEntryFetching.fs:257 cites REQ-JE-3.8 for the both-None error, which belongs to REQ-JE-3.5.1. A reader of REQ-JE-3.8 is told the two lookups differ when they do not.

**Action:** Withdraw REQ-JE-3.8 as superseded by REQ-JE-3.5 (FI-only mode) and re-cite its tests to REQ-JE-3.5/3.5.1. Alternatively, keep REQ-JE-3.8 but strike the 'Unlike REQ-JE-3.5' sentence and re-cite the tests that describe behavior REQ-JE-3.8 does not state.

**Why:** Two requirements that claim to differ but describe the same behavior make readers look for a second capability that does not exist. They also let tests cite a requirement for behavior it does not state (reference-only lookup under REQ-JE-3.8), which weakens traceability.

---

## JE-STALE-1 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-1.13 (line 32)
- **Summary:** REQ-JE-1.13 treats 'the Decisions-log invariant' as a live authority, but the Decisions log was archived on 2026-07-30 to Specs/Archive/Decisions.md, which says not to cite its entries as current rules.
- **Resolution:** fix-spec

REQ-JE-1.13: 'this equality is the realization of the Decisions-log invariant "a journal entry's lines sum to zero"; the two are not in conflict.' The only Decisions log in the repo is Specs/Archive/Decisions.md. Its header says: 'This is history, not authority. Archived 2026-07-30 ... Do not cite an entry here as a current rule — find the rule where it lives now (Specs/Behavioral/, ...) and cite that.' Specs/README.md also lists Specs/Archive/ under History: 'Never read as authority.' The quoted wording is not verbatim either. The archived 2026-06-11 entry says lines 'will be required to sum to zero exactly'. No other spec or learning states a 'sum to zero' invariant. REQ-JE-1.13 is now the rule itself, so its reconciliation clause points readers at a document they are told not to treat as authority.

**Action:** Strike the Decisions-log clause from REQ-JE-1.13 so the requirement stands on its own. If the rationale is worth keeping, move it to a dated *Why:* line that states the reasoning without citing the archive.

**Why:** The README's one-source-of-truth rule: a spec that cites a document marked history-only invites readers, and LLM agents especially, to treat the archive as authority. That is the failure the archive header was written to prevent.

---

## JE-STALE-2 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md Waived table, REQ-JE-2.14 (line 165)
- **Summary:** The waiver reason for REQ-JE-2.14 names a source file (`JournalContracts.fs`) and a type (`JournalEntryHeaderReturn`), which breaks the README rule that spec documents never name source files or functions.
- **Resolution:** fix-spec

The waiver reads: 'Enforced structurally — no creation input contract exposes `voidedAt` (`JournalContracts.fs`: it appears only on `JournalEntryHeaderReturn`).' Specs/README.md, 'Linkage rules (the star chart)', says: 'Spec documents **never** name source files, functions, or tests. All linkage lives at the destination.' A grep of Specs/Behavioral/*.md for '.fs' finds this waiver row and nothing else, so it is the only file citation in the behavioral corpus. The claim itself is true today. Src/Ui.InterfaceBridge/InterfaceContracts/JournalContracts.fs has `voidedAt` only on JournalEntryHeaderReturn, and the input records have no void field. But the citation is a coordinate that will go stale silently if the file is renamed or split.

**Action:** Restate the waiver reason in operator terms, without the file and type names. For example: 'Enforced structurally — no posting input carries a void marker; only the returned journal entry exposes it. Reconstitution from persistence legitimately carries the void marker; reconstitution is not creation.'

**Why:** The star-chart rule exists because a coordinate in prose cannot be checked and goes stale silently. This is the one place in the behavioral specs that breaks the rule.

---

## JE-AMB-1 — ambiguity
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-1.52 (line 65) vs REQ-JE-2.15 (line 88), REQ-JE-5.1 (line 131), REQ-JE-4.8 (line 119)
- **Summary:** REQ-JE-1.52 says every requirement that creates a comment with a secondary entry names the relation that link records, but REQ-JE-2.15 and REQ-JE-5.1 create such comments without naming any relation, and REQ-JE-4.8 (cited as one of the creators) is unenforceable guidance rather than a comment-creating operation.
- **Resolution:** dan-decides

REQ-JE-1.52 (amended 2026-10-03): 'The link itself carries no direction: each requirement that creates a comment with a secondary entry names the relation it records (REQ-JE-4.4: the voided entry's replacement; REQ-JE-4.8: the original being corrected).' The design note on lines 7-9 repeats this: 'The relation is stated by the requirement that creates the comment.' Two operations create comments with a secondary entry and name no relation. REQ-JE-2.15 lets comments supplied at posting name a secondary entry ('validated as any other new comment (REQ-JE-1.52–1.54...)'). REQ-JE-5.1 says only 'optionally naming a secondary, related journal entry'. REQ-JE-4.8 is in the Unenforceable table: it is a procedure for the operator, and the correcting comment it describes is actually written through REQ-JE-2.15 or REQ-JE-5.1. The comment record has no relation field. So for any comment with a secondary entry, nothing stored says which relation applies (replacement, original being corrected, or generic 'related'), and the 'named by the creating requirement' rule cannot be checked after the fact. Readers can reasonably disagree. One reader takes a secondary link on a correcting entry to mean 'original being corrected'. Another takes it as merely 'related', since it came through REQ-JE-5.1. The difference matters because REQ-JE-4.13's *Why* relies on the void comment's secondary link as the provenance path from a replacement entry.

**Action:** Dan decides one of two things. (a) Amend REQ-JE-1.52 to say that a secondary link records a generic 'related entry' relation, except where a requirement (REQ-JE-4.4) gives it a specific meaning, and drop REQ-JE-4.8 from the list of creators. (b) Have REQ-JE-2.15 and REQ-JE-5.1 each state the relation their secondary link records.

**Why:** A requirement that claims every creating path names the relation, when two of the creating paths do not, leaves the meaning of a stored link to whoever reads it. That is risky for audit and provenance data the ledger keeps permanently.

---

