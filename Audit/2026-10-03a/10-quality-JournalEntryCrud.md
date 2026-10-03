# spec-quality:JournalEntryCrud

## CON-JE-1 — contradiction
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-1.52 vs REQ-JE-4.4 (and REQ-JE-4.8)
- **Summary:** REQ-JE-1.52 says the comment link runs from primary to secondary ('the primary entry ... corrects, supersedes, voids the secondary'), but REQ-JE-4.4 puts the voided entry in primary and its replacement in secondary, which is the opposite direction.
- **Resolution:** dan-decides

REQ-JE-1.52 (line 65): 'When set, it records a directional relationship: the primary entry relates to (comments on, corrects, supersedes, voids) the secondary entry.' REQ-JE-4.8 (line 121) follows this: the correcting entry is primary, and 'secondary journal entry = the original', so the primary corrects the secondary. REQ-JE-4.4 (line 117) reverses it. The void reason comment has 'primary journal entry = the voided entry' and 'may name a secondary journal entry (e.g. the replacement entry)'. Read through REQ-JE-1.52, that comment says the voided entry 'voids' or 'supersedes' its replacement, which is backwards. Code and tests follow REQ-JE-4.4: JournalEntryVoiding.voidJournalEntry calls insertReason with the voided entry as primary, and Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsLedger.fs:250 asserts primary = voided and secondary = named. So the stored data has two opposite meanings for the same primary/secondary columns. The withdrawal reason for REQ-STG-4.7 (DataIngestion.md:324) and the Why under REQ-JE-4.13 both rely on this link for provenance ('its void comment can name the replacement'). Anything that later walks these links, such as a report or an agent tracing a replacement back to its original, cannot read the direction the same way for every comment.

**Action:** Dan decides which direction is canonical. Either (a) reword REQ-JE-1.52 so the secondary is just 'a related entry' with no direction, and drop 'voids' and 'supersedes' from its list; or (b) keep the direction and amend REQ-JE-4.4 so a void that names a replacement records the link the way REQ-JE-1.52 and REQ-JE-4.8 do. For example, the reason comment is still primary on the voided entry, and the spec says plainly that its secondary means 'replaced by'.

**Why:** REQ-JE-1.52 is the data-state rule that gives the secondary column its meaning. When one requirement fills that column against the stated direction, the meaning of the stored data is no longer reliable. That matters for the provenance chain the staging withdrawals depend on.

---

## AMB-JE-4.5 — ambiguity
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-4.5 (line 118)
- **Summary:** The second sentence of REQ-JE-4.5, 'A voided period cannot be re-opened by voiding within it', does not make sense: fiscal periods are never voided, and voiding does not re-open a period.
- **Resolution:** fix-spec

REQ-JE-4.5: 'Voiding is rejected when the entry's derived fiscal period is not open. A voided period cannot be re-opened by voiding within it; closed-period corrections go through an offsetting entry (REQ-JE-4.8).' FiscalPeriodCrud.md has no void state for periods (they are open or closed via is_open), and no requirement anywhere says voiding re-opens a period. The sentence is probably a garbled 'A closed period cannot be corrected by voiding within it'. As written it suggests an extra rule, about voided periods or about voiding re-opening periods, that a test writer could try to cover. The first sentence holds the testable rule, and the test JournalEntryVoiding.fs:193 covers only that sentence.

**Action:** Replace the second sentence with something like 'Voiding cannot be used to change a closed period; closed-period corrections go through an offsetting entry (REQ-JE-4.8).' Or delete it, since REQ-JE-4.8 already says this.

**Why:** Test agents write tests from requirement text. A sentence naming a non-existent state ('voided period') invites a pointless test or confused reasoning about period state, and Dan has said he no longer reviews that work.

---

## ELAB-JE-3.9 — insufficient-elaboration
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-3.9, REQ-JE-3.9.3; Src/Business.CrossDomainOrchestration/AccountActivity.fs fetchFiltered; Tests/Tests.Integrated/CrossDomainOrchestration/AccountActivity.fs
- **Summary:** REQ-JE-3.9 describes a lookup for one account, but the code and the tests that cite it implement a multi-account filtered fetch with filters the spec never mentions, and REQ-JE-3.9.3's sort by account code only makes sense across several accounts.
- **Resolution:** fix-spec

REQ-JE-3.9 says: 'retrieve all journal entry lines for a given account, enriched with ...'. REQ-JE-3.9.3 lets the caller sort by 'entry date, account code, or amount'. Sorting lines for one account by account code changes nothing, so REQ-JE-3.9.3 does not fit REQ-JE-3.9 as written. The code is AccountActivity.fetchFiltered. It takes an AccountActivityFilter in which accountId is optional, and it also filters by account type, subtype, parent account, entry-date range, source, journal entry ID, exact amount and description (a LIKE 'contains' match, which brings in REQ-SYS-1.4). It also returns a no-activity row for accounts that have no lines. Tests cite REQ-JE-3.9 for behavior the requirement never states: 'fetchFiltered by amount returns only matching lines' (line 182), 'by description' (316), 'by journalEntryId' (360, 390), and 'returns no-activity row for account with no lines' (155). The tests read as covering REQ-JE-3.9, but the requirement does not define how those filters should behave. Examples: is the amount match exact? Do the filters combine with AND? Does an account with no lines produce a row? So two developers rebuilding this from the spec would produce different results. Commit a1bc950 also made Period activity (Reporting) read its lines through this fetch, so the extra behavior now matters downstream.

**Action:** Amend REQ-JE-3.9 (or add sub-requirements) so it states the operation as built. Account is one optional filter among the ones listed. Filters combine conjunctively. Description is a contains match per REQ-SYS-1.4. Amount is an exact Money match. An account with no matching lines appears with no activity detail. Then REQ-JE-3.9.3's account-code sort has a purpose.

**Why:** Linkage runs only through test names. When tests cite a REQ for behavior it does not state, the three-state rule looks satisfied while the behavior is in fact unspecified. Under the fully agentic workflow, the spec is the only contract an implementing agent can be held to.

---

## STALE-JE-WAIVER — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md Waived table rows REQ-JE-1.23 (line 160) and REQ-JE-1.51 (line 164)
- **Summary:** The waiver reasons for REQ-JE-1.23 and REQ-JE-1.51 cite code names that no longer exist in Src (MoneyRecord, MoneyModule.fromDecimal, validateJournalEntryHeader).
- **Resolution:** fix-spec

REQ-JE-1.23 waiver: 'the line amount is a MoneyRecord constructed via MoneyModule.fromDecimal'. `grep -rn 'MoneyRecord\|MoneyModule' Src` returns nothing. The type is now Business.FinancialServices.Money.Money ('type Money = private { amount: decimal }', Money.fs:8), built through Money.fromDecimal (Money.fs:21). REQ-JE-1.51 waiver: 'existence of the primary entry is validated at construction (validateJournalEntryHeader, exercised by every comment test)'. `grep -rn validateJournalEntryHeader Src` returns nothing. The check is now JournalEntryCommentOrchestration.confirmJournalEntryHeader, called from constructNewAndPersist, and it maps to JournalEntryCommentPrimaryJeHeaderIdNotFound. Both waivers are still sound in substance: the private-constructor Money type and the non-nullable Guid plus NOT NULL FK still enforce these rules. Only the evidence they cite has gone stale.

**Action:** Update the two waiver reasons to name the current constructs: Money.Money with its private constructor and Money.fromDecimal; and JournalEntryCommentOrchestration.confirmJournalEntryHeader (or the REQ-JE-5.8 tests) for primary-entry existence.

**Why:** A waiver is a claim that something enforces the requirement without a test. When the cited enforcer can't be found, a later auditor or agent cannot confirm the claim and may wrongly decide the waiver is unsound, or wrongly trust it if the enforcement really has been removed.

---

## STALE-JE-4.7 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-4.7 (line 120); Src/Business.CrossDomainOrchestration/AccountBalance.fs fetchByAccountIdList
- **Summary:** REQ-JE-4.7 points readers to 'the leobloom_prod skill's note', which is not in the repo, and dictates a SQL technique ('the void check belongs in the WHERE, not the join') that the main balance query does not use.
- **Resolution:** fix-spec

REQ-JE-4.7: '(see the leobloom_prod skill's note on the `LEFT JOIN ... AND voided_at IS NULL` overstatement trap — the void check belongs in the `WHERE`, not the join)'. Skills/ holds ArchReviewer, ArchiMate, CodeReviewer, CreateLearning, SonOfLeoRequirementsAudit, SonOfLeoSrcDeveloper, TestNameReview and TestWriter, with no leobloom_prod. The name appears only in HobsonsNotes (history, not authority) as a Hobson harness skill. The Opus developer and test agents Dan now depends on cannot follow the pointer. Separately, AccountBalance.fetchByAccountIdList does not filter voided entries in a WHERE. It LEFT JOINs ledger.journal_entry and zeroes voided lines with 'case when je.voided_at is not null then 0'. That is correct (no overstatement), but it breaks the letter of REQ-JE-4.7's instruction. The testable rule, that a voided entry's lines contribute nothing, is clear. The parenthetical adds an implementation rule the code does not follow and that an agent could take as binding.

**Action:** Remove the leobloom_prod pointer and the WHERE-versus-join instruction from REQ-JE-4.7, leaving the outcome: a voided entry's lines contribute nothing to any balance. If the overstatement trap is worth keeping as guidance, move it to a CompoundedLearnings article that is in the repo.

**Why:** Requirements are the top authority agents work from. A cross-reference to material outside the repo cannot be checked, and an implementation instruction the code contradicts invites an agent to 'fix' correct code or to flag a false violation.

---

