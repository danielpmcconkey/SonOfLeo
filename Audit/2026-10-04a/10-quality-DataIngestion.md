# requirements-quality-auditor:DataIngestion.md

## STG-CON-AUTH-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md design note 'classification authority hierarchy' (line 19) vs REQ-STG-6.1 (line 209), REQ-STG-6.2 Why (line 211), REQ-STG-7.2 Why (line 233)
- **Summary:** The preamble ranks the operator lowest in a 'descending order of authority' and says each layer acts only where no higher layer has assigned an account. Elsewhere the spec calls the operator the highest authority tier, lets it override anything, and cites that same preamble as proof that the operator outranks the deduplicator.
- **Resolution:** fix-spec

Line 19: "Three layers assign accounts to staged lines, in descending order of authority: (1) the bespoke parser ... (2) the classification rules engine ... (3) the operator, who manually assigns or overrides during review. Each layer acts only where no higher-authority layer has already assigned an account." Under that text the operator is the lowest authority and may act only on lines no parser or classifier has filled.

REQ-STG-6.1 (line 209): the operator may "assign or override the account on a staged line, regardless of whether the account was previously set by a parser or the classifier". That is the opposite of 'acts only where no higher-authority layer has already assigned'.

REQ-STG-6.2 Why (line 211): "manual intervention is the highest authority tier".

REQ-STG-7.2 Why (line 233): "'Reviewed' because the operator outranks the deduplicator (see the classification authority hierarchy in the preamble)". The preamble it cites ranks the operator last and does not mention the deduplicator.

ClassificationRuleCrud.md line 7 repeats the preamble's ordering as "parser (highest) > classifier > operator (lowest, but can override all)". That shows the ordering is meant as fill precedence (who fills a null first), not as authority. DataIngestion's note, however, calls it authority and draws the 'acts only where no higher layer assigned' rule from it. The 2026-10-03 disposition #232 reworded the CR note on a different point (who owns the null-account rule) and did not touch this ordering.

**Action:** Reword the DataIngestion authority-hierarchy note to separate the two orderings. Fill order is parser, then classifier, then operator, and the classifier never overrides the parser. Authority is operator first: the operator may override any assignment, and outranks the deduplicator. Then REQ-STG-6.1, 6.2's Why and 7.2's Why all agree with the note they cite.

**Why:** The note is where a parser author or implementing agent learns who may override whom. As written, a reader taking the note at its word would forbid the operator override that REQ-STG-6.1 requires, and REQ-STG-7.2's reason for excluding 'Reviewed' entries from dedup points to text that says the opposite.

---

## STG-AMB-DEDUP-TIE-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-7.2 (line 232); related REQ-STG-4.1.2 Why (line 140), design note 'fi_reference is required' (line 21), REQ-SYS-3.4
- **Summary:** REQ-STG-7.2 says the original is the entry ingested earliest. Entries from one file are all 'first ingested' at the same instant, so when two entries in one file share a source and fi_reference, the spec gives no way to decide which is the original and which is flagged.
- **Resolution:** dan-decides

REQ-STG-7.2: "Staged entries that share the same source and fi_reference are ordered by when each was first ingested. The earliest is the original; every later one is flagged as duplicate."

Under REQ-SYS-3.4 every timestamp an operation writes uses its single initiation instant. Ingestion writes each entry's initial 'Ingested' transition with that instant (StageEntryOrchestration.fs lines 176-178: `Context.getInitiationInstant`). So every entry from one file has the same 'first ingested' time.

This case is realistic. The spec's own design note (line 21) has paystub parsers derive fi_reference from the check date, so two paystubs from one source with the same check date (a regular check and a bonus check, for example) in one file share a source and fi_reference.

The spec gives no tie rule. One developer would treat file order as ingestion order. Another would flag both entries, since neither is earlier. A third would pick one arbitrarily. The code does the third: StageEntryHeader.fs lines 351-353 orders by `earliest_statuses.modified_at nulls first, se.unique_id`, which in a tie means a random UUID. The difference is visible to the operator: it decides which entry is flagged 'Duplicate', which one stays flaggable or postable, and, with REQ-STG-6.7, whether a Payment-referenced entry is the one declined.

The spec itself rejects this kind of silent arbitrary pick for status ties. REQ-STG-4.1.2's Why says two transitions at one instant "would tie, and the current status would be picked arbitrarily and silently".

**Action:** Dan decides the tie rule for entries with the same first-ingested instant, then adds it to REQ-STG-7.2. The options are file order (line number), flagging every tied entry, or flagging none and reporting the tie.

**Why:** Which entry is the 'original' decides which one reaches the ledger. A test writer cannot write the expected result for a same-file repeat, and the current code's pick is random, which is the kind of silent arbitrary choice REQ-STG-4.1.2 was written to forbid.

---

## STG-AMB-SRCFILTER-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-10.2 (line 287), REQ-STG-10.7 and its Why (lines 295-296)
- **Summary:** REQ-STG-10.2 lists 'ingestion source' as an exact-match filter without saying whether the caller gives the source's name or its ID, or whether a value that matches no source is an error. REQ-STG-10.7 settles the second question only for the account filter, with a reason that applies just as well to the source filter.
- **Resolution:** fix-spec

REQ-STG-10.2: "...ingestion source, FI reference, status ... Every filter except description is an exact match." Ingestion source is an entity with a UUID key (REQ-STG-2.25) and a unique name (REQ-STG-2.28). The audit-conduct article on entity identification makes the UUID the default reading. The code instead filters by name (StageEntryOrchestration.fs lines 908-911: `source_name = @source_name`, from a `JournalRefFinancialInstitution`), and a name that matches no source returns an empty list.

REQ-STG-10.7 makes an unresolvable account code a typed error. Its Why states a general reason: "A code that matches nothing is a caller error, not an empty-result condition — silently returning no results would be indistinguishable from 'no staged entries match.'" A mistyped source name has exactly that problem, but the spec is silent on it. One developer would key the filter on the source ID. Another would key it on the name and raise an error for an unknown name, as 10.7 does. A third would return an empty list, which is what the code does. Each gives a different result for the same request.

**Action:** Amend REQ-STG-10.2 to state that the ingestion-source filter takes the source name. Either extend REQ-STG-10.7 to an ingestion-source name that matches no source, or state that such a name returns an empty result.

**Why:** Without this, the operator's main review query gives different answers for the same input depending on which developer built it. In the current code a typo in the source name looks exactly like 'nothing staged from that source', which is the failure REQ-STG-10.7 was written to prevent.

---

## STG-AMB-TSZONE-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-3.12 (line 124); SystemWide.md REQ-SYS-3.4, REQ-SYS-7.1
- **Summary:** REQ-STG-3.12 fixes the processed-file name prefix as `yyyy-MM-dd.HHmmss.fff-` from 'the ingestion timestamp' but does not say which time zone renders it, and the prefix carries no offset.
- **Resolution:** fix-spec

REQ-STG-3.12: the file is moved "its name prefixed with the ingestion timestamp (`yyyy-MM-dd.HHmmss.fff-`)". REQ-SYS-3.4 says file names use the operation's initiation instant, which is an Instant. REQ-SYS-7.1 fixes the configured local zone only for converting an Instant to a calendar Date, not to a wall-clock time string. The format has no offset, so UTC and local rendering are both reasonable readings, and they produce different file names. The code renders in the configured local zone (IngestionRoutes.fs line 60, via Clock.instantToString, Clock.fs line 28: `instant.InZone(timeZoneLocal)`). A test asserting the processed file name cannot get its expected value from the spec. In local time the prefix also repeats during the hour after clocks fall back, while in UTC it does not.

**Action:** Amend REQ-STG-3.12 to name the zone of the timestamp prefix (the configured local zone, matching the code, or UTC).

**Why:** The processed file name is part of the operator-visible contract. The spec's stated reasons for the prefix, avoiding collisions and keeping drops apart, depend on how it is rendered, and a test writer cannot write the expected name without knowing the zone.

---

## STG-STALE-WAIVE-1 — stale-reference
- **Location:** Specs/Behavioral/DataIngestion.md Waived table rows REQ-STG-3.5 (line 308), REQ-STG-1.2 (line 311), REQ-STG-9.9 (line 314); Specs/README.md 'Linkage rules' (line 106)
- **Summary:** Three waiver reasons name code (`Guid.NewGuid()`, `StagedEntryLine`, `postStageEntry`/`StageEntry`), which the specs README forbids. One of those names, `StagedEntryLine`, does not exist in the code.
- **Resolution:** fix-spec

Specs/README.md line 106: "Spec documents **never** name source files, functions, or tests."

The waiver reasons:
- REQ-STG-3.5: "UUID generation via Guid.NewGuid() in create functions"
- REQ-STG-1.2: "one JSON object → one StagedEntryLine". No type, module or function called `StagedEntryLine` exists anywhere in Src. The type is `StageEntryLine` (Business.FinancialServices.DataIngestion/StageEntryLine.fs).
- REQ-STG-9.9: "postStageEntry takes a single StageEntry and produces one JE."

The same habit appears in other specs (REQ-CR-4.2's waiver and REQ-JE-2.14's waiver), so it is not unique to this file. Here it has already gone stale once, because the type was renamed without the waiver following. The README rule exists for exactly this reason: code names in specs cannot be kept in sync.

**Action:** Restate the three waiver reasons in operator and structural terms with no code names. For example, REQ-STG-1.2: 'each record becomes exactly one staged line by construction'. REQ-STG-9.9: 'posting builds one journal entry per staged entry; no aggregation step exists'. REQ-STG-3.5: 'IDs are system-generated UUIDs; uniqueness enforced by the PK constraint'.

**Why:** A waiver's reason is what a later auditor checks to decide whether the waiver still holds. A reason that rests on a code name breaks silently when the code is renamed, as `StagedEntryLine` already has, and it goes against the README's rule that all linkage between spec and code lives in test names.

---

