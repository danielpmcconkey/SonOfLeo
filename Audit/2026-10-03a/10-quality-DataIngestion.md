# requirements-quality-auditor:DataIngestion.md

## STG-CON-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-7.2 (line 230) vs REQ-STG-7.3 (lines 232-233); Src/Business.FinancialServices.DataIngestion/StageEntryHeader.fs fetchDuplicates (lines 338-391)
- **Summary:** REQ-STG-7.3 says a voided journal entry must not block re-import of the same transaction, but REQ-STG-7.2 flags the re-import anyway, because the staged entry that produced the voided journal entry stays 'Posted' and counts as the original.
- **Resolution:** dan-decides

REQ-STG-7.3 excludes voided journal entries from the stage-vs-ledger check. Its *Why* says: "voiding is a soft delete ... so its external reference should not block re-import of the same transaction." Since 2026-09-27, REQ-STG-4.2 makes 'Posted' terminal, REQ-JE-4.13 says voiding leaves the staged entry 'Posted', and REQ-STG-7.2 counts every staged entry as a potential original "whatever its status — including ... 'Posted'". Nothing in Src deletes ingestion.staged_entry rows (the only DELETE in Src is on staged_entry_line, StageEntryLine.fs:319).

So the case the 7.3 rationale describes always ends in a Duplicate flag through the 7.2 path: post from staging, void the JE, re-import the same fi_source+fi_reference. fetchDuplicates confirms this: `ais.ordinal > 1 or ail.journal_entry_id is not null`, with the voided filter applied only to the ledger side. The 7.3 voided-exclusion test (StageEntryIngestion.fs:702) passes only because its voided JE is a fixture that was never posted from staging. In production, the voided exclusion takes effect only for journal entries that did not come from staging. Scope exclusion 5 ("A correction ... is made with the manual journal entry routes, not by re-posting from staging") agrees with 7.2's behaviour, so the stale part is 7.3's rationale.

**Action:** Restate REQ-STG-7.3's *Why* to say that the voided exclusion governs only external references on journal entries that did not originate from a staged entry, and that a staged-origin re-import is caught by REQ-STG-7.2 by design (scope exclusion 5). Alternatively, if re-import after a void should be admitted, change 7.2.

**Why:** Two requirements in the same section give opposite answers to the same scenario: whether a voided transaction can be re-imported. An implementer or test writer who follows 7.3's stated intent would build or test the wrong behaviour, and the current 7.3 test gives false assurance because it never exercises the staged-origin path.

---

## STG-CON-2 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-6.5 (line 219); ClassificationRuleCrud.md REQ-CR-8.2; CashFlow.md REQ-CF-12.3; StageEntryOrchestration.fs protectionsOf (lines 531-547)
- **Summary:** REQ-STG-6.5 forbids removing a line that is "recorded in any classification run" and then says only lines "no classification run has evaluated" can be removed. Those are different sets, and its claim that in practice only operator-added lines are removable is false.
- **Resolution:** fix-spec

REQ-CR-8.2 says: "A line with no match produces no row." A line can therefore be evaluated by a run and still have no record in it. Two kinds of line fall in that gap:
(a) a null-account line the classifier evaluated with no match (the 'NoMatch' outcome);
(b) a parser-assigned line, which account classification never evaluates (REQ-STG-5.2) and which payment-agreement classification may evaluate (REQ-CF-12.3, "A line's account assignment does not exclude it") without matching.

Under the first sentence ("recorded in any classification run") these lines are removable. Under the closing sentence ("Only lines no classification run has evaluated ... can be removed") lines in (a) are not. The parenthetical "in practice, lines the operator added" is wrong under either reading: parser-assigned lines with no match records are removable. The code follows the "recorded" reading (protectionsOf checks only RuleMatch rows). An implementer who followed the closing sentence would need to persist evaluation outcomes that REQ-CR-8.2 says are not recorded.

**Action:** Delete the sentence "Only lines no classification run has evaluated — in practice, lines the operator added — can be removed." or rewrite it as "Only lines with no match row in any classification run (REQ-CR-8.2) can be removed."

**Why:** The same requirement states two different removal predicates, and one of them cannot be implemented under REQ-CR-8.2. Two developers would build different guards, and a test written from the closing sentence would fail against correct code.

---

## STG-CON-3 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-6.7 (line 223) vs REQ-STG-7.5.1 (line 236); StageEntryOrchestration.fs deduplicateStagedEntries (lines 340-362); StagingIngestionRules.fs:801
- **Summary:** REQ-STG-6.7 says dedup reports an entry it cannot flag because of a Payment, but dedup's return (REQ-STG-7.5.1) is only the list of 'Ingested' entries, so a paid 'Classified', 'NoMatch' or 'Conflict' entry is not reported anywhere.
- **Resolution:** dan-decides

REQ-STG-6.7: "The attempt fails with a typed error (manual update) or is reported without flagging (dedup)." REQ-STG-7.5.1 defines dedup's entire return as "every staged entry whose status is 'Ingested' after the pass".

Payments can exist on lines of entries in 'Classified', 'NoMatch' and 'Conflict' as well as 'Ingested': REQ-CF-12.3 linkage candidates include all of these statuses, and REQ-CF-13.2 excludes only 'Duplicate' and 'Ignored'. REQ-STG-7.2 makes all four statuses flaggable. When dedup finds a repeat whose entry is 'Classified' and paid, it leaves the entry alone and returns nothing about it. When the entry is 'Ingested', it appears in the Ingested list with nothing to distinguish it from an ordinary Ingested entry.

The code comment at line 345 ("it stays at its status and so appears in the result below") is true only for 'Ingested'. The REQ-STG-6.7 dedup test is named "...lists it in its result as not flagged because of the Payment", but it uses an 'Ingested' entry and asserts only Assert.Contains on the Ingested list. That is plain 7.5.1 membership, not a report of the reason.

**Action:** Decide what "reported" means for dedup. Either add a REQ under §7 saying that dedup returns, alongside the Ingested list, every entry it declined to flag under REQ-STG-6.7, with the reason; or reword 6.7 to say dedup leaves such entries at their status without reporting them.

**Why:** The spec promises the operator visibility of a suppressed duplicate. For three of the four flaggable statuses, the implementation and the return contract give none. A Payment can then quietly keep a real duplicate in the postable set.

---

## STG-AMB-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-4.4 (line 143), REQ-STG-6.2 (line 208), REQ-STG-4.6 table; Definitions.md 'Postable'; StageEntryOrchestration.fs updateStageEntry line 670 (confirmLines ... AllowNone)
- **Summary:** REQ-STG-4.4 and the Postable definition rely on unnamed "upstream invariants" to guarantee every line has an account by 'Classified' or 'Reviewed', but the manual update (REQ-STG-6.2) may move an entry to either status with null-account lines.
- **Resolution:** dan-decides

REQ-STG-4.4: "if the upstream invariants are sound, all lines have an account by the time an entry reaches these statuses." Definitions.md calls a postable entry with a null account "a broken upstream invariant".

No REQ states such an invariant for the operator path. REQ-STG-6.2 validates only "balanced entry, valid account codes, legal status transition". The 4.6 table permits operator moves such as Ingested→Classified (the table's parenthetical is not a rule; only →Posted is restricted, by 4.8), NoMatch→Reviewed and Ignored→Reviewed. The code validates the manual result with AllowNone, so an operator can make an entry postable with unassigned lines. Under REQ-STG-9.8 that entry then makes the entire batch post fail.

REQ-RPT-7.5 also expects this state to reach the review. Two reasonable developers would diverge: one rejects a manual move to 'Classified' or 'Reviewed' while any line's account is null, the other permits it and lets posting fail loudly. Separately, the 4.6 parentheticals ("all lines have accounts after classification", "operator manually assigns missing accounts") read like conditions but are not enforced.

**Action:** Add one sentence to REQ-STG-6.2 (or 4.4) stating whether a manual update may leave an entry 'Classified' or 'Reviewed' while any line's account is null. If it may not, name that as the invariant 4.4 refers to.

**Why:** The posting-failure design in 4.4 and 9.4 calls the null-account case an invariant breach, yet no requirement establishes the invariant. Whether the guard sits at review time or at posting time changes what the operator sees, and whether a single edit can block a whole Saturday post.

---

## STG-AMB-2 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-6.2 (line 208) vs REQ-STG-9.10, REQ-STG-6.2.1/4.8 rationale; IngestionRoutes.fs updateStageEntry (journalEntryHeaderIdUpdate = NoChange)
- **Summary:** REQ-STG-6.2 lets the operator "set any field on the staged entry and its lines", which literally includes the journal-entry and journal-entry-line IDs that only batch post should write (REQ-STG-9.10).
- **Resolution:** fix-spec

Since 2026-09-26, staged entries and lines carry the IDs of the journal entry and journal entry lines they produced (REQ-STG-9.10). The schema has journal_entry_header_id and journal_entry_line_id. REQ-STG-6.2 still grants "any field" without exception. The implementation sensibly withholds both: the route hard-codes journalEntryHeaderIdUpdate = NoChange, and the contract has no line-level JE-line field. That restriction is not in the spec.

The spec's own rationale elsewhere argues for withholding them: 4.8 ("An operator moving an entry to 'Posted' by hand records a posting that never happened") and 6.2.1 (an operator must not masquerade as the poster). A developer implementing 6.2 literally would expose fields that let an operator fabricate ledger linkage. That linkage feeds the CashFlow payment-to-posted transition (scope exclusion 2).

**Action:** Amend REQ-STG-6.2 to exclude the journal entry and journal entry line IDs from the fields the manual update may set ("set only by batch post, REQ-STG-9.10").

**Why:** An unqualified "any field" grant conflicts with a field that is written by a single mechanism. The current code is correct only because the implementer read intent into the spec, which is the opposite of what a fully agentic workflow should depend on.

---

## STG-AMB-3 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-2.6 (line 71), REQ-STG-3.4 (line 114), REQ-STG-3.12 (line 124); IngestionRoutes.fs lines 35 and 90
- **Summary:** source_file is "the full file path of the ingested file", but REQ-STG-3.12 moves and renames the file after commit, so the spec does not say whether the import path or the final processed path is recorded.
- **Resolution:** dan-decides

REQ-STG-2.6 records "the full file path of the base staging format file that produced this entry". REQ-STG-3.4 says "The source_file is the full file path of the ingested file." REQ-STG-3.12 then moves the file to the processed directory under a new name, prefixed with the ingestion timestamp.

The code stores the import-directory path (toBeProcessedPath). After a successful ingestion that path never holds the file again, and the timestamped processed name is stored nowhere. Another reasonable developer would record the processed path, since that is where the file can actually be found. The REQ-STG-10.2 source-file filter behaves differently under each choice. Under the import-path choice, two drops of the same file name (the case 3.12's timestamp exists for) are also indistinguishable by source_file. The 150-character limit applies to whichever path is chosen, and the processed path is longer by the 23-character prefix.

**Action:** State in REQ-STG-3.4 which path source_file records: the path the file was read from, or the processed path it was moved to.

**Why:** The field exists for traceability back to the file. Which path is stored decides whether that trace leads to an existing file, and two implementations would give different query results under REQ-STG-10.2.

---

## STG-AMB-4 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-10.2 (line 283); StageEntryOrchestration.fs fetchFiltered (memo/source_file/fi_reference use '=')
- **Summary:** REQ-STG-10.2 gives match semantics only for description (case-sensitive partial). Memo, source file and FI reference, which are also free text, have none, and a developer could reasonably implement them as partial matches.
- **Resolution:** fix-spec

The *Why* explains that description is a partial match because "FI descriptions are long institution-specific strings; the operator needs to search by ... fragments". The same reasoning applies to memo, which is free text up to 1000 characters (REQ-STG-1.12, 2.15), and to source_file, which is a full path up to 150 characters.

The spec says nothing about either, or about fi_reference. The code uses exact equality for all three. A developer extending or re-implementing the query could equally choose partial match for memo or source file, and both readings satisfy the text. Observable results differ: a memo filter of "rent" returns nothing under exact match and every rent split line under partial match. REQ-SYS-1.4 governs only filters that are declared partial-match, so it does not settle the question.

**Action:** Amend REQ-STG-10.2 to state that every filter other than description is an exact match. Alternatively, name the other partial-match fields explicitly.

**Why:** Filter semantics are directly observable to the operator. With only one field's semantics stated, the others are left to implementer judgment, and a later route or refactor could silently change query results.

---

## STG-CON-4 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md design note 'system boundary' (line 7) vs design notes 'parser latitude' (line 13) and 'classification authority hierarchy' (line 19), and REQ-STG-6.4 *Why* (line 218)
- **Summary:** The boundary note says parsers "know nothing about accounts ... or the ledger", while two later notes have parsers assigning account codes and querying obligation data; one of those notes also contradicts REQ-STG-6.4's rationale on whether the parser can produce the tenant split.
- **Resolution:** fix-spec

Line 7: "Parsers know nothing about accounts, classification, or the ledger."
Line 13: a parser "produces the full leg decomposition with account_code populated on every line". It also says "A tenant-payment parser queries obligation data to produce a rent/utility split."
Line 19: the parser is authority layer (1) and "assigns accounts only when it knows the answer with certainty".
REQ-STG-1.8 and 3.7 make parser-supplied account_code part of the contract.

Obligation data now lives inside the system as the CashFlow domain, so an external parser querying it also contradicts the line 7 boundary ("The two meet at the file format and nowhere else").

REQ-STG-6.4's *Why* says the opposite of line 13 for the same example: "A tenant payment covers rent and a utility share ... The split depends on data the parser may not have when it runs (the tenant's invoice can postdate the parse), so the operator must be able to split in review."

**Action:** Reword the line 7 note to say parsers know nothing about classification rules or ledger state beyond account codes. Revise or remove the line 13 tenant-payment example so it agrees with REQ-STG-6.4's rationale and the CashFlow domain's ownership of obligation data.

**Why:** The design notes are what a parser author or implementing agent reads to learn where responsibilities lie. As written they give contradictory answers on whether parsers may assign accounts and who produces tenant splits.

---

## STG-STALE-1 — stale-reference
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-4.5 *Why* (line 145)
- **Summary:** REQ-STG-4.5's rationale describes "voiding a bad JE and ignoring its staged source", which is impossible now that 'Posted' is terminal (REQ-STG-4.2, 4.7 withdrawn, REQ-JE-4.13).
- **Resolution:** fix-spec

*Why:* "Without this, voiding a bad JE and ignoring its staged source would cause the next overlapping file import to re-ingest the same bad data." A staged entry whose JE was voided stays 'Posted' (REQ-JE-4.13), and 4.6 has no Posted→Ignored transition, so its staged source can never be ignored.

The requirement body (dedup treats 'Ignored' entries as matches) is still sound, for entries ignored before posting. Only the motivating scenario is stale. The re-import after a void is now caught by REQ-STG-7.2's 'Posted'-counts-as-original rule. This is related to STG-CON-1 but is a separate REQ.

**Action:** Rewrite the REQ-STG-4.5 *Why* to describe a pre-posting scenario: an entry deliberately ignored in review, whose transaction reappears in the next overlapping export.

**Why:** Rationale lines are what implementing agents use to resolve edge cases. A rationale built on a withdrawn transition invites someone to reintroduce Posted→Ignored as "what the spec intended".

---

## STG-WD-1 — stale-reference
- **Location:** Specs/Behavioral/DataIngestion.md Withdrawn table REQ-STG-2.8 (line 320); §2 data states; REQ-STG-9.10; DbMigration/Scripts/202609071115-CreateIngestionTables.sql
- **Summary:** REQ-STG-2.8 was withdrawn because "back-trace from staging to ledger is not a system concern". That reason is now false (scope exclusion 2 was reversed and REQ-STG-9.10 added), and §2 has no data-state requirements for the back-trace columns that now exist.
- **Resolution:** fix-spec

The withdrawal reason reads: "Back-trace from staging to ledger is not a system concern. The JE's external reference ... provides the link back." Scope exclusion 2 (line 26) was reversed on 2026-09-26: "the external reference alone cannot identify a line." REQ-STG-9.10 now requires recording the JE ID on the staged entry and the JE line ID on each staged line. The schema has nullable FK columns staged_entry.journal_entry_header_id and staged_entry_line.journal_entry_line_id.

The withdrawn table therefore justifies the withdrawal with a premise the spec has since rejected. The legal data states that 2.8 used to cover (nullable, FK to ledger, null until posting) are stated nowhere in §2, which otherwise enumerates every column of both tables (2.1–2.17). Under REQ-SYS-2.1, an entity's legal data-state rules come from its spec's data-states section. For these two columns that section is silent, for example on whether a non-'Posted' entry may carry a JE ID.

**Action:** Amend the REQ-STG-2.8 withdrawal reason to note that it was superseded by REQ-STG-9.10 (2026-09-26). Add §2 data-state REQs for the staged entry's journal entry ID and the staged line's journal entry line ID: nullable, FK to ledger, and set only by batch post.

**Why:** Check 6 asks whether a withdrawal left an uncovered gap. This one did once the back-trace returned: the columns exist and are load-bearing for CashFlow §10, but their legal states are unspecified, and the withdrawn table gives a reader the wrong reason.

---

## STG-UNENF-1 — enforcement-gap
- **Location:** Specs/Behavioral/DataIngestion.md Unenforceable table REQ-STG-1.16 (line 314); Specs/README.md lines 82-84; StageEntryOrchestration.fs constructFromRaw (line 200)
- **Summary:** REQ-STG-1.16 is listed as Unenforceable, but it binds the system's grouping code, not humans, and its observable half (a group_id reused across files produces separate staged entries) is testable. The cited function constructSetFromRaw also no longer exists.
- **Resolution:** dan-decides

Specs/README.md defines Unenforceable as "nothing in the system enforces it; it binds humans, not code." REQ-STG-1.16 ("group_id is unique within the file. Not globally unique — the ingestion step replaces it with a system-generated staged entry ID") is enforced by code: constructFromRaw groups each file's rows with List.groupBy on group_id and gives each group a fresh StageEntryHeaderId.

The "not globally unique" clause has a direct observable test: ingest two files that reuse a group_id and assert two distinct staged entries, neither rejected nor merged. The table's reason, "No persistent state to assert against", overlooks that the staged entries themselves are that state.

The reason also cites `constructSetFromRaw`, which is not in Src. The current function is constructFromRaw.

The within-file half is arguably tautological given REQ-STG-1.3 (rows that share a group_id form one group by definition). That would justify a waiver, not an Unenforceable entry.

**Action:** Move REQ-STG-1.16 out of the Unenforceable table. Either cover it with a test that ingests two files sharing a group_id, or put it in the Waived table with a reason that refers to the existing constructFromRaw. Fix the function name either way.

**Why:** The three-state rule is only as good as the honesty of each classification. Labelling a code-enforced, testable behaviour as "binds humans" removes it from test coverage, and a regression such as global group_id matching would go unnoticed.

---

## STG-CODE-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-8.4 (line 258); IngestionRoutes.fs post / postWithExternallyManagedTransaction; StageEntryOrchestration.fs post (lines 803-835)
- **Summary:** REQ-STG-8.4 says shadow post is "read-only against the staging tables", but the implementation runs the real post path, which writes staging rows (the 'Posted' audit transitions and the journal-entry IDs) and then rolls them back with the ledger writes.
- **Resolution:** dan-decides

REQ-STG-8.4: "Shadow post must not modify any staged entry's status or any staging data. It is read-only against the staging tables and write-then-rollback against the ledger." The shadow route calls the same StageEntryOrchestration.post as batch post, under runCommandRouteAndAutoRollback. That function writes journal_entry_header_id (StageEntryHeader.update), journal_entry_line_id (updateJournalEntryLineId) and a 'Posted' / 'LedgerPoster' audit row (updateHeaderStatus) for every postable entry, all inside the transaction that is later rolled back.

Nothing persists, so the requirement's first sentence ("must not modify") is satisfied as an outcome. The second sentence explicitly separates read-only staging from write-then-rollback ledger, and the code does write-then-rollback on both. REQ-RPT-4.4 inherits the same wording. Code, as the lower authority, contradicts the spec's literal text. The practical exposure is limited to anything that could observe uncommitted writes or hit staging constraints during the shadow, such as the REQ-STG-4.1.2 unique-instant constraint.

**Action:** Choose one. Relax REQ-STG-8.4's second sentence to "leaves no change to staging data (writes to staging, if any, are rolled back with the ledger)". Or have shadow post stop before the staging writes.

**Why:** The spec states an implementation-level property that the code does not have, and the test (IngestionRoutes.fs:430) checks only the outcome. A future auditor or agent cannot tell from the spec whether the staging writes are sanctioned.

---

## STG-TERM-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md design note 'staging schema' (line 15); REQ-STG-3.15, 2.27, 2.28; Definitions.md 'Entity'
- **Summary:** The staging-schema note calls ingestion.source "a lookup entity", but Definitions.md treats lookup and entity as mutually exclusive, and REQ-STG-3.15 (sources are created at runtime by user action) makes it an entity.
- **Resolution:** fix-spec

Definitions.md 'Entity' gives the litmus: "does any user action ever insert or update a row? Yes → entity ... Yes [regenerable] → lookup, not an entity. ... a lookup-shaped table becomes an entity the moment users can extend it at runtime." REQ-STG-3.15 provides a route that creates a source by name, so users extend the table at runtime. REQ-STG-2.27 (created/modified Instants) and 2.28 (uniqueness, i.e. REQ-SYS-6.1 duplicate creation) already treat it as an entity.

The definition admits terms precisely because the classification "changes which requirements apply". Calling it a "lookup" invites the opposite conclusion: that REQ-SYS-2.1, 3.x and 6.x do not apply, as DB-STAGE-1 ruled for staged entries and lines. The two-word phrase is the only place the spec classifies the source table.

**Action:** Change "a lookup entity identifying financial institution sources" to "an entity identifying financial institution sources" in the staging-schema design note.

**Why:** Definitions.md exists so that entity status decides which system-wide policies apply. A term that blends the two categories reopens a question the definitions were written to settle.

---

## STG-STALE-2 — stale-reference
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-2.7 (line 72) under '### Staged entry (`ingestion.staged_entry`)'; REQ-STG-4.1.1; Withdrawn REQ-STG-2.24; StageEntryIngestion.fs:226
- **Summary:** REQ-STG-2.7 ("Staged entry status cannot be null") is listed as a data state of the ingestion.staged_entry table, which no longer has a status column. Its test had to reinterpret the requirement in a comment.
- **Resolution:** fix-spec

REQ-STG-2.24's withdrawal says "Status column removed ... Status is now derived from the audit trail at read time", and REQ-STG-4.1.1 says "Status is not stored anywhere else." REQ-STG-2.7 still sits among the column-level states of ingestion.staged_entry, and its wording is column wording.

The citing test (StageEntryIngestion.fs:226) explains: "REQ-STG-2.7: status is no longer a column, so 'cannot be null' means the derived value is present and agrees with the entry's own latest transition." That is a test author's reinterpretation of an orphaned requirement. Another test writer could reasonably read it as a column assertion that cannot apply, or treat it as redundant with 4.1.1 and the at-least-one-transition rule (3.9).

**Action:** Reword REQ-STG-2.7 to the derived form, for example "Every staged entry has at least one status transition, so its current status (REQ-STG-4.1.1) is never absent, and is one of the §4 values." Alternatively, move it under §4.

**Why:** A requirement that describes a schema shape which no longer exists depends on each test writer's interpretation. The comment in the test shows that interpretation was needed.

---

