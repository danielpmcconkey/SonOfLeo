# Data Ingestion

Behavioral specs for the staging and ingestion pipeline — the mechanism by which external financial data enters the SonOfLeo ledger. Data flows through a universal staging area where it is validated, classified, deduplicated, reviewed, and batch-posted as journal entries through the existing JE domain model.

**Design note — naming.** Identifiers in this spec (e.g. `fi_source`, `entry_date`, `line_type`) name domain concepts for readability. They do not prescribe variable names, function names, property names, or any other naming convention in the code or tests.

**Design note — system boundary.** SonOfLeo's ingestion boundary is "a valid base staging format file appeared." The files are produced by bespoke parsers — lightweight, institution-specific scripts that live outside this repository. Each parser reads a financial institution's native export format and converts it to the base staging format. Parsers know nothing about accounts, classification, or the ledger. The staging pipeline knows nothing about CSV column positions, JSON shapes, or FI-specific quirks. The two meet at the file format and nowhere else.

**Design note — file format choice.** The base staging format is JSONL (newline-delimited JSON, one object per line). No industry interchange standard models multi-leg journal entry decomposition from the consumer's perspective. OFX, QIF, FIX, BIAN, and Plaid's transaction model were evaluated; all are single-entry, per-account formats designed for FI-to-consumer or FI-to-FI communication. The staging format's job is different: it carries the parser's decomposition of an economic event into journal entry legs, including legs the parser already knows the account for and legs it leaves for classification.

**Design note — amount sign convention.** The base staging format carries amount as a positive value in every record. Direction is expressed by the line_type field. How the parser derives line_type from the source's own debit/credit/sign conventions is parser-specific — the format constrains only the result: positive amount, explicit direction.

**Design note — parser latitude.** The description field in the base staging format is the raw string from the financial institution, untransformed. However, the parser has full latitude to implement deterministic pre-processing of the overall record set. A payroll parser decomposes a paystub into 10 fully-assigned legs. A tenant-payment parser queries obligation data to produce a rent/utility split. A mortgage parser reads amortization data to split principal from interest. In each case the parser produces the full leg decomposition with account_code populated on every line. The classification step has nothing to do on these entries — it exists for the simple bank transactions where only the cash leg is known and the expense account must be determined by pattern matching.

**Design note — staging schema.** Staged data is stored in normalized tables within the `ingestion` schema: `ingestion.staged_entry` (one row per economic event, carrying entry-level fields), `ingestion.staged_entry_line` (one row per journal entry leg, carrying amount, direction, and account assignment), `ingestion.staged_entry_audit` (one row per status transition, providing a complete audit trail), and `ingestion.source` (a lookup entity identifying financial institution sources). This mirrors the journal entry's header/line structure because a staged entry is a draft journal entry held in a review area.

**Design note — account IDs in the model, codes at the boundary.** Staged lines and classification rules reference accounts by UUID internally. The boundary layer resolves account codes (provided by parsers, operators, and CLI input) to account IDs at ingestion time. The Saturday review surface shows account codes, but these are looked up from the stored ID. This design gives staged lines and classification rules a proper FK to `ledger.account`, preventing stale references when account codes change. (2026-08-22)

**Design note — classification authority hierarchy.** Three layers assign accounts to staged lines, in descending order of authority: (1) the bespoke parser, which assigns accounts only when it knows the answer with certainty (payroll splits, tenant payment decompositions, mortgage allocations); (2) the classification rules engine, which fills null account assignments by pattern matching against the description; (3) the operator, who manually assigns or overrides during review. Each layer acts only where no higher-authority layer has already assigned an account. A parser that cannot determine the account leaves the account null — it does not guess. This hierarchy is why the classifier cannot override parser assignments (REQ-STG-5.3) and why fully parser-assigned entries skip classification and transition directly to `'Classified'` (REQ-STG-5.8). (2026-08-15, updated 2026-08-22)

**Design note — fi_reference is required.** Every parser must produce an fi_reference — the dedup key. For sources without a natural transaction ID (e.g., paystubs), the parser derives a deterministic reference (e.g., the check date). This ensures the dedup pass has universal coverage; no class of transaction is invisible to duplicate detection.

**Scope exclusions — deliberately not supported by the staging pipeline.** These are design decisions, not gaps. Each was evaluated and excluded during the initial design (2026-08-08).

1. **Journal entry comments.** The staging format carries no comment fields. Comments are editorial artifacts that describe relationships and corrections which do not exist until after posting. Attach comments after posting via the CLI (JournalEntryCrud §5).
2. ~~**Back-trace from staging to ledger.**~~ *Reversed 2026-09-26.* Posting now records the journal entry a staged entry became, and the journal entry line each staged line became (REQ-STG-9.10). The payment-to-posted transition (CashFlow §10) and the void reversal (REQ-JE-4.11) both have to follow a staged line to its ledger line; the external reference alone cannot identify a line.
3. **Multiple external references per journal entry.** A staged entry carries one fi_source and one fi_reference, producing exactly one external reference on the resulting JE. An imported transaction has one financial-institution identity. Additional external references can be attached after posting (REQ-JE-4.10).
4. ~~**Obligation instance linking.**~~ *Reversed 2026-09-26.* Staged lines are linked to payment agreements and matched to invoices before posting, by the cash flow domain (CashFlow §12–§13). The link lives in the cash flow domain, not on the staged line.
5. **Voids, adjustments, and corrections.** Staging handles incoming financial data only. Voids and corrections operate on existing ledger state via dedicated CLI commands (JournalEntryCrud §4). One consequence reaches back into staging: voiding a journal entry that was posted from a staged entry returns that staged entry to `'Reviewed'` so it can be corrected and posted again (REQ-STG-4.7, REQ-JE-4.11). (Amended 2026-09-26)
6. **Period close entries.** Closing and reversing entries are domain operations, not imported data.
7. **Classifier override of parser-assigned accounts.** The classification rules engine can only assign an account where the staged line's account is null. It cannot override a value assigned by the parser. Manual override is available via the review step (§6). This ensures that a parser's deterministic decomposition is authoritative within its domain.


## 1. Base staging file format

The base staging format is the interface contract between bespoke parsers and the ingestion step. These requirements define what the system accepts; a file that violates any requirement is rejected at ingestion.

- **REQ-STG-1.1** The base staging format is JSONL: one JSON object per text line, newline-delimited.
- **REQ-STG-1.2** Each record in the file represents one future journal entry line.
- **REQ-STG-1.3** Records sharing a `group_id` value within a single file form one economic event. All records in a group will produce one journal entry when posted.
- **REQ-STG-1.4** `group_id`: required, string, maximum 36 characters (room for a UUID). (Length limit added 2026-09-26)
- **REQ-STG-1.16** `group_id` is unique within the file. Not globally unique — the ingestion step replaces it with a system-generated staged entry ID.
  - *Why:* group_id is a local association mechanism for the parser. Global identity is the staged entry's UUID, assigned at ingestion. (2026-08-08)
- **REQ-STG-1.5** `entry_date`: required, ISO 8601 calendar date (`yyyy-MM-dd`). Must parse to a valid Calendar Date.
- **REQ-STG-1.6** `amount`: required, positive decimal with no more than two decimal places. Maximum value 9,999,999,999.99. Direction is expressed by `line_type`, not by sign. (Revised 2026-09-26 from "exactly two decimal places" — a JSON number cannot carry trailing zeros, so the system rejects excess precision rather than requiring two digits.)
- **REQ-STG-1.7** `line_type`: required. Must be `"Debit"` or `"Credit"`.
- **REQ-STG-1.8** `account_code`: optional (null). When present, must be a non-empty string. The parser populates this when the destination account is known; null when classification must determine it.
- **REQ-STG-1.9** `description`: required, maximum 1000 characters. The raw description from the source document.
- **REQ-STG-1.10** `fi_source`: required, maximum 100 characters. Identifies the institution and account that originated this data.
- **REQ-STG-1.11** `fi_reference`: required, maximum 100 characters. The financial institution's own transaction identifier, or a deterministic parser-derived reference for sources without native IDs.
- **REQ-STG-1.12** `memo`: optional (null), maximum 1000 characters. A per-line note describing what this leg represents.
- **REQ-STG-1.13** All records in a group (same `group_id`) must carry the same `entry_date`, `description`, `fi_source`, and `fi_reference` values.
  - *Why:* These are entry-level fields describing the economic event, not the individual leg. Inconsistency within a group indicates a parser defect. (2026-08-08)
- **REQ-STG-1.14** A group must contain at least two records.
  - *Why:* Every journal entry requires at least two legs (REQ-JE-1.12). A single-record group cannot produce a balanced entry. (2026-08-08)
- **REQ-STG-1.15** Within a group, the sum of all amounts where line_type is `"Debit"` must equal the sum of all amounts where line_type is `"Credit"`.
  - *Why:* The balanced-entry invariant (REQ-JE-1.13) is validated at ingestion rather than deferred to posting. Catching imbalance immediately gives the parser actionable feedback. (2026-08-08)
- **REQ-STG-1.17** The JSON property names are: `baseStageEntryGroupId` (group_id), `entryDate` (entry_date), `amount`, `entryType` (line_type), `accountCode` (account_code), `description`, `fiSource` (fi_source), `fiReference` (fi_reference), `memo`. Text fields are trimmed before validation (REQ-SYS-1.1).
  - *Why:* The file is an external contract. The naming design note frees code from the spec's identifiers, but a parser author needs the literal keys. (2026-09-26)


## 2. Staged data states

### Staged entry (`ingestion.staged_entry`)

- **REQ-STG-2.1** Staged entry ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.2** Staged entry date is a Calendar Date. Cannot be null.
- **REQ-STG-2.3** Staged entry description cannot be null and cannot be whitespace only (post-trim per REQ-SYS-1.1). Maximum 1000 characters.
- **REQ-STG-2.4** Staged entry must reference a source in `ingestion.source` (source_id foreign key, not null).
- **REQ-STG-2.5** Staged entry fi_reference cannot be null. Maximum 100 characters.
- **REQ-STG-2.6** Staged entry source_file cannot be null or whitespace only, and cannot exceed 150 characters. Records the full file path of the base staging format file that produced this entry. (Length limit added 2026-09-26)
- **REQ-STG-2.7** Staged entry status cannot be null. Must be one of the values defined in §4.
- **REQ-STG-2.24** Stricken.
- **REQ-STG-2.8** Stricken.
- **REQ-STG-2.9** A staged entry must have at least two staged lines.

### Staged line (`ingestion.staged_entry_line`)

- **REQ-STG-2.10** Staged line ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.11** Staged line must belong to exactly one staged entry (entry_id foreign key, not null).
- **REQ-STG-2.12** Staged line amount is a positive decimal(12,2) value (greater than zero).
- **REQ-STG-2.13** Staged line line_type must be `'Debit'` or `'Credit'`.
- **REQ-STG-2.14** Staged line account is nullable (account_id foreign key to `ledger.account`). When set, identifies the target account by UUID.
- **REQ-STG-2.15** Staged line memo is optional (nullable). When provided, cannot be whitespace only (post-trim per REQ-SYS-1.1). Maximum 1000 characters.
- **REQ-STG-2.16** *(Withdrawn 2026-09-26 — see the Withdrawn table.)*
- **REQ-STG-2.17** Within a staged entry, the sum of all line amounts where line_type is `'Debit'` must equal the sum of all line amounts where line_type is `'Credit'`.

### Staged entry audit (`ingestion.staged_entry_audit`)

- **REQ-STG-2.18** Audit record ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.19** Audit record must reference a staged entry (entry_id foreign key, not null).
- **REQ-STG-2.20** Audit record from_status is nullable (null on the initial ingestion transition).
- **REQ-STG-2.21** Audit record to_status cannot be null.
- **REQ-STG-2.22** Audit record changed_at is a non-null Instant.
- **REQ-STG-2.23** Audit record change_mechanism cannot be null. Must be one of: `'StageIngestion'`, `'Classifier'`, `'Deduplicator'`, `'Operator'`, `'LedgerPoster'`.

### Ingestion source (`ingestion.source`)

- **REQ-STG-2.25** Ingestion source ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.26** Ingestion source name cannot be null or whitespace only (post-trim per REQ-SYS-1.1). Maximum 100 characters. It is the value a record's `fi_source` must equal to resolve to this source (REQ-STG-3.6), and the financial institution written on the external reference of every journal entry posted from it (REQ-STG-9.5).
- **REQ-STG-2.27** Ingestion source carries created-at and modified-at Instants.


## 3. Ingestion behaviors

- **REQ-STG-3.1** The system must provide a means to ingest a base staging format file.
- **REQ-STG-3.2** The system must validate every record in the file against the format requirements in §1. A record that fails validation must be rejected with a typed error identifying the record and the violation.
- **REQ-STG-3.3** If any record in the file fails validation, the entire file is rejected. No staged entries or lines are created. Partial ingestion is not permitted.
  - *Why:* A group that loses a record to validation cannot produce a balanced entry. All-or-nothing prevents orphaned legs. (2026-08-08)
- **REQ-STG-3.4** For each group in the file, the system must create one staged entry and one staged line per record. The staged entry's entry_date, description, and fi_reference are populated from the group's shared values. The source_id is resolved from the group's fi_source. The source_file is the full file path of the ingested file.
- **REQ-STG-3.5** The system must generate a UUID for each staged entry and each staged line.
- **REQ-STG-3.6** When a record's fi_source does not resolve to an existing source in `ingestion.source`, the system must reject the file.
  - *Why:* An unrecognized source indicates a parser misconfiguration, not a classification concern. (2026-08-08)
- **REQ-STG-3.7** When a record's account_code is non-null, the system must validate it resolves to an existing account in the chart of accounts. If it does not, the system must reject the file. The resolved account ID is stored on the staged line.
  - *Why:* A parser-assigned account code that does not exist is a parser defect. Fail fast. Code-to-ID resolution at ingestion time (not posting time) ensures the staged line carries a FK-backed account reference from the moment it is created. (2026-08-22)
- **REQ-STG-3.8** When a record's account_code is null, the staged line's account is set to null.
- **REQ-STG-3.9** On successful ingestion, every staged entry's status is set to `'Ingested'` and an audit record is created (from_status null, to_status `'Ingested'`).
- **REQ-STG-3.10** Ingestion is atomic: either the entire file is ingested (all entries and lines persisted) or no rows are created.
- **REQ-STG-3.11** The ingestion request names the file, the directory to read it from, and a processed directory. Both directories must exist and the file must exist in the import directory; otherwise the request fails with a typed error and nothing is ingested.
- **REQ-STG-3.12** On successful ingestion, the file is moved to the processed directory, its name prefixed with the ingestion timestamp (`yyyy-MM-dd.HHmmss.fff-`). A failed ingestion leaves the file where it was.
  - *Why:* The import directory then holds only files not yet ingested, and a re-run cannot ingest the same file twice by accident. The timestamp keeps repeated drops of the same file name from colliding. (2026-09-26)
- **REQ-STG-3.13** Ingestion returns every staged entry it created, each with its full composition (header, lines, status transitions).
- **REQ-STG-3.14** Ingestion does not deduplicate or classify. Those are separate operations (§7, §5).
- **REQ-STG-3.15** The system must provide a means to create an ingestion source by name. The system generates the ID and sets created-at and modified-at to the time of creation.


## 4. Status lifecycle

- **REQ-STG-4.1** A staged entry's status must be one of: `'Ingested'`, `'Classified'`, `'NoMatch'`, `'Conflict'`, `'Reviewed'`, `'Duplicate'`, `'Posted'`, `'Ignored'`.
- **REQ-STG-4.1.1** A staged entry's current status is the to_status of its most recent audit record. Status is not stored anywhere else.
- **REQ-STG-4.2** `'Posted'` is a terminal status. No transitions out of `'Posted'` are permitted, except the void reversal in REQ-STG-4.7. (Amended 2026-09-26)
- **REQ-STG-4.3** Every status transition must create an audit record in `ingestion.staged_entry_audit`.
- **REQ-STG-4.4** A staged entry is postable when its status is `'Classified'` or `'Reviewed'`. No additional filtering (e.g. line-level account presence) is applied — if the upstream invariants are sound, all lines have an account by the time an entry reaches these statuses. If they do not, posting fails loudly (REQ-STG-9.4) rather than silently excluding the entry.
- **REQ-STG-4.5** `'Ignored'` marks an entry that should not be posted due to data problems at the source. The deduplication pass must treat `'Ignored'` entries as matches — re-importing a transaction that was deliberately ignored must flag the new entry as duplicate, not silently re-admit it.
  - *Why:* Without this, voiding a bad JE and ignoring its staged source would cause the next overlapping file import to re-ingest the same bad data. (2026-08-09)

- **REQ-STG-4.6** The following are the only permitted status transitions. Any transition not listed must be rejected.

```
Ingested   → Classified  (all lines have accounts after classification)
Ingested   → NoMatch     (at least one line has no rule match after classification)
Ingested   → Conflict    (at least one line has multiple rule matches at equal priority)
Ingested   → Duplicate   (dedup identifies this entry as a duplicate)
Ingested   → Ignored     (operator deliberately excludes the entry)
Classified → Duplicate   (dedup re-run finds a match after classification)
Classified → Reviewed    (operator confirms or adjusts)
Classified → Ignored     (operator deliberately excludes the entry)
Classified → Posted      (batch post)
NoMatch    → Classified  (classification re-run; all lines now have accounts)
NoMatch    → Conflict    (classification re-run; a line now has multiple rule matches at equal priority)
NoMatch    → Duplicate   (dedup re-run finds a match)
NoMatch    → Reviewed    (operator manually assigns missing accounts)
NoMatch    → Ignored     (operator deliberately excludes the entry)
Conflict   → Classified  (classification re-run; all lines now have accounts)
Conflict   → NoMatch     (classification re-run; the tie is gone but a line still has no rule match)
Conflict   → Duplicate   (dedup re-run finds a match)
Conflict   → Reviewed    (operator resolves the conflict)
Conflict   → Ignored     (operator deliberately excludes the entry)
Duplicate  → Reviewed    (operator overrides — legitimate duplicate)
Duplicate  → Ignored     (operator deliberately excludes the entry)
Ignored    → Reviewed    (operator resurrects a previously ignored entry)
Reviewed   → Ignored     (operator deliberately excludes the entry)
Reviewed   → Posted      (batch post)
Posted     → Reviewed    (the journal entry posted from this entry was voided; REQ-STG-4.7)
```

  - Setting a staged entry to the status it already has is not a transition: nothing is written and no audit record is created. Classification re-runs rely on this, since they re-derive the status of every entry they touch and many land where they started.
  - *Why:* The four classification re-run transitions out of `'NoMatch'` and `'Conflict'` exist because classification runs over those statuses as well as `'Ingested'` (REQ-STG-5.1). (2026-09-26)


- **REQ-STG-4.7** When the journal entry posted from a staged entry is voided, the staged entry transitions from `'Posted'` to `'Reviewed'` with change mechanism `'Operator'`, as part of the void (REQ-JE-4.11). No other path out of `'Posted'` exists.
  - *Why:* Correcting an imported transaction is void-and-repost. Without this path the staged entry stays `'Posted'`, its fi_reference blocks re-import as a duplicate (REQ-STG-7.2 counts `'Posted'` entries), and the only way back into the ledger is a hand-built journal entry that has lost its provenance. `'Reviewed'` because the operator is now the authority on this entry, and a `'Reviewed'` entry is postable (REQ-STG-4.4) and exempt from dedup (REQ-STG-7.2). (2026-09-26)

## 5. Classification behaviors

The classification step runs the vendor classification rules engine against staged entries. The rules entity (pattern, priority, FI scoping, account mapping) is specified in `ClassificationRuleCrud.md`. These requirements govern how the staging pipeline interacts with the rules engine.

- **REQ-STG-5.1** The system must provide a means to run automated classification against staged entries with status `'Ingested'`, `'NoMatch'`, or `'Conflict'`.
  - *Why:* A rule added after the first run can resolve an entry that previously failed to classify. Re-running over `'NoMatch'` and `'Conflict'` entries picks those up without operator intervention; REQ-STG-5.3 keeps already-settled lines untouched. (2026-09-26)
- **REQ-STG-5.2** Classification evaluates each staged line whose account is null against the active classification rules whose claimant is an account. A rule matches when its field match conditions are satisfied by the staged entry and line properties (description, source, amount, line type, memo, and their combinations per ClassificationRuleCrud.md). (Revised 2026-09-26: account-claimant rules only; rules claiming a payment agreement are evaluated by payment agreement linkage, CashFlow §12.)
- **REQ-STG-5.3** Classification must not modify a staged line whose account is already non-null.
  - *Why:* Parser-assigned accounts are authoritative. The classifier fills gaps; it does not override. (2026-08-08)
- **REQ-STG-5.4** When exactly one rule matches and the line's account is null, the classifier assigns the matching rule's account to the line. (Revised 2026-09-26: the matching rule is recorded in the classification run, REQ-STG-5.10, not on the staged line.)
- **REQ-STG-5.5** When multiple rules match and one has strictly higher priority, the classifier assigns the highest-priority rule's account to the line. (Revised 2026-09-26: see REQ-STG-5.4.)
- **REQ-STG-5.6** When multiple rules match with equal priority for a line with null account, the staged entry's status is set to `'Conflict'`.
- **REQ-STG-5.7** When no rule matches a line with null account, the staged entry's status is set to `'NoMatch'`.
  - *Why:* `'NoMatch'` means the classifier ran and found nothing — it is distinct from "not yet classified." The name was chosen over "unclassified" to avoid ambiguity. (2026-08-09)
- **REQ-STG-5.8** When classification completes and every line in the staged entry has a non-null account, the entry's status is set to `'Classified'`.
- **REQ-STG-5.9** When classification of a single entry produces both Conflict and NoMatch outcomes on different lines, Conflict takes precedence.
- **REQ-STG-5.10** Every classification run is identified by a system-generated run ID. For each line evaluated, every rule that matched it is recorded against the line under the run ID — all matching rules, including those that lost to a higher priority and every rule in a tie. The record is retrievable by run ID.
  - *Why:* An operator resolving a Conflict or questioning an assignment needs to see what matched, not re-run the classifier against rules that may have changed since. (2026-09-26)
- **REQ-STG-5.11** Classification returns the run ID, the outcome for each line evaluated, and every staged entry whose status is `'Ingested'`, `'Classified'`, `'NoMatch'`, `'Conflict'` or `'Reviewed'` after the run.


## 6. Manual review behaviors

- **REQ-STG-6.1** The system must provide a means for an operator to assign or override the account on a staged line, regardless of whether the account was previously set by a parser or the classifier.
- **REQ-STG-6.2** The manual update mechanism allows the operator to set any field on the staged entry and its lines, including status. The system validates the result (balanced entry, valid account codes, legal status transition) but does not infer or auto-assign status from the operator's changes.
  - *Why:* Original spec auto-transitioned to `'Reviewed'` on any line modification. Overruled — manual intervention is the highest authority tier, and the operator knows the intended status. Inferring it revokes that authority. (2026-08-16)
- **REQ-STG-6.3** The operator may override a duplicate flag, transitioning the entry's status from `'Duplicate'` to `'Reviewed'`.
  - *Why:* Legitimate duplicate transactions exist (two identical charges on the same day). The operator, not the system, makes this call. (2026-08-08)
- **REQ-STG-6.3.1** A manual update that names a line belonging to a different staged entry is rejected with a typed error naming both.
- **REQ-STG-6.3.2** A manual update that changes nothing is rejected, per REQ-SYS-6.1. Setting only the status to the entry's current status is the exception stated under REQ-STG-4.6: it writes nothing and succeeds.
- **REQ-STG-6.4** The system must provide a means for the operator to add lines to a staged entry and to remove lines from it, in the same operation as any other manual update (REQ-STG-6.2). Validation applies to the entry as it stands after the whole operation: it must satisfy every staged-entry requirement in §2, in particular at least two lines (REQ-STG-2.9) and balance (REQ-STG-2.17). An operation whose result fails validation changes nothing.
  - *Why:* One FI line often carries more than one economic purpose. A tenant payment covers rent and a utility share that post to different revenue accounts; a mortgage payment covers principal, interest and escrow. The split depends on data the parser may not have when it runs (the tenant's invoice can postdate the parse), so the operator must be able to split in review. Validating only the final state is what makes a split possible at all: reducing one line and adding another passes through an unbalanced intermediate. (2026-09-26)
- **REQ-STG-6.5** A staged line that is linked to a payment agreement (CashFlow §12) or referenced by a Payment cannot be removed. The operation fails with a typed error naming the line; the link or Payment must be removed first.
  - *Why:* Removing the line would orphan the link or Payment that points at it. (2026-09-26)
- **REQ-STG-6.6** Lines cannot be added to or removed from a staged entry whose status is `'Posted'`.


## 7. Deduplication behaviors

- **REQ-STG-7.1** The system must provide a means to run deduplication against staged entries.
- **REQ-STG-7.2** Staged entries that share the same source and fi_reference are ordered by when each was first ingested. The earliest is the original; every later one is flagged as duplicate. Every staged entry counts when establishing the original, whatever its status — including `'Ignored'` (per REQ-STG-4.5), `'Posted'`, `'Reviewed'` and `'Duplicate'`. Only entries with status `'Ingested'`, `'Classified'`, `'NoMatch'` or `'Conflict'` are ever flagged; entries already `'Duplicate'`, `'Posted'`, `'Ignored'` or `'Reviewed'` are left as they are.
  - *Why:* Every prior sighting of a transaction is evidence the new one is a repeat, so every status counts as the original. Only undecided entries get flagged: `'Reviewed'` because the operator outranks the deduplicator (see the classification authority hierarchy in the preamble), and the rest because they are already decided. (2026-08-15, Reviewed exclusion added 2026-08-25, restated 2026-09-26 to name the original-by-ingestion-order rule and the flaggable statuses)
- **REQ-STG-7.3** A staged entry is flagged as duplicate when a non-voided journal entry in the ledger carries an external reference whose financial_institution and reference values match the staged entry's source and fi_reference. Voided journal entries are not considered present in the ledger for dedup purposes.
  - *Why:* Prevents re-importing transactions that were posted in a prior cycle. Voided entries are excluded because voiding is a soft delete — the economic event the entry recorded has been reversed, so its external reference should not block re-import of the same transaction. (2026-08-08, voided exclusion clarified 2026-08-25)
- **REQ-STG-7.4** Stricken.
- **REQ-STG-7.5** Flagging a staged entry as duplicate must not alter its lines or their account assignments.
- **REQ-STG-7.5.1** Deduplication returns every staged entry whose status is `'Ingested'` after the pass.

### Transfer pairing

A transfer between two accounts that are both imported appears twice: once in each institution's export. Each side, once its accounts are assigned, describes the same journal entry. Reference-based dedup (REQ-STG-7.2, REQ-STG-7.3) cannot see this — the two sides carry different sources and references.

- **REQ-STG-7.6** The system must provide a means to detect transfer pairs among staged entries.
  - *Why:* Posting both sides doubles the movement on both accounts. LeoBloom handled this by convention (one importer owns each transfer; the other side is skipped by hand), which lived in operator memory and failed silently. (2026-09-26)
- **REQ-STG-7.7** Two staged entries form a transfer pair when all of the following hold: (a) they come from different sources; (b) every line on both entries has an assigned account; (c) the two entries would produce the same journal entry lines — the same accounts, each with the same line type and the same amount; (d) their entry dates are no more than a caller-supplied number of days apart.
  - *Why:* (c) is the whole definition of "the same movement recorded twice." It needs no notion of cash accounts or transfer vocabulary, and it cannot pair two entries that would post differently. (b) makes pairing run after account classification — before it, the non-cash side of each entry is unknown. (2026-09-26)
- **REQ-STG-7.8** An entry is a pairing candidate only when its status is `'Classified'` or `'Reviewed'`. An entry's counterpart may additionally have status `'Posted'`.
  - *Why:* A transfer whose other side posted in a prior week must still be caught when the second side arrives. (2026-09-26)
- **REQ-STG-7.9** When an entry has exactly one counterpart, and that counterpart has exactly one counterpart (the entry itself), the pair is resolved. The survivor is chosen by the first rule that decides it: (1) the entry with status `'Posted'`; (2) the entry with status `'Reviewed'`; (3) the entry with the earlier entry date; (4) the entry ingested first. The other entry transitions to `'Duplicate'` with change mechanism `'Deduplicator'`. When both entries are `'Reviewed'`, neither is flagged and the pair is reported.
  - *Why:* A `'Reviewed'` entry is never flagged because the operator outranks the deduplicator (REQ-STG-7.2). Otherwise the earlier date wins because it is when the money left, which is the date the journal entry should carry. (2026-09-26)
- **REQ-STG-7.10** When an entry has more than one possible counterpart, no entry in that group is flagged. The group is reported for the operator.
  - *Why:* Two identical transfers in the same window are indistinguishable by data. Code does not break ties anywhere in this system. (2026-09-26)
- **REQ-STG-7.11** The pairing operation returns every resolved pair (survivor and flagged entry) and every unresolved group.
- **REQ-STG-7.12** Flagging an entry as a transfer duplicate must not alter its lines or their account assignments.


## 8. Shadow post behaviors

- **REQ-STG-8.1** The system must provide a means to simulate posting all postable staged entries without modifying ledger state.
- **REQ-STG-8.2** Shadow post must construct journal entries through the same domain model and validation path used by batch post (§9). The construction occurs within a database transaction that is rolled back after completion.
  - *Why:* A shadow post that skips domain validation gives false confidence. If a staged entry would fail validation (closed fiscal period, inactive account, imbalanced lines), shadow post must surface that failure identically. (2026-08-08)
- **REQ-STG-8.3** Shadow post must produce a trial balance before posting and a trial balance after posting (computed within the rolled-back transaction). The caller derives the delta.
  - *Why:* Original spec required only a delta. The full before/after is more useful — the Saturday routine reconciles against point-in-time account balances, not movements. A delta alone can't be reconciled without a second call. (2026-08-16)
- **REQ-STG-8.4** Shadow post must not modify any staged entry's status or any staging data. It is read-only against the staging tables and write-then-rollback against the ledger.
- **REQ-STG-8.5** The before and after trial balances are computed as of the date the operation runs. The result states that the post was rolled back.


## 9. Batch post behaviors

- **REQ-STG-9.1** The system must provide a means to batch-post all postable staged entries to the ledger.
- **REQ-STG-9.2** For each postable staged entry, the system must construct a journal entry through the domain model (JournalEntryCrud §2), applying all existing JE validations.
- **REQ-STG-9.3** The journal entry header fields are mapped from the staged entry: description from the staged entry's description, entry_date from the staged entry's entry_date. Source is the fixed provenance label "Data ingestion import", describing *how* the entry was created, not which FI it came from — the FI identity lives on the external reference (REQ-STG-9.5). No comments are attached.
- **REQ-STG-9.4** For each staged line, the system must construct a journal entry line with the line's account ID, amount, line_type, and memo. A null account at posting time is a loud failure — it indicates a broken upstream invariant (classification or review allowed an unassigned line through). Invalid non-null account IDs cannot occur: the staged line's account_id is FK-constrained against `ledger.account`.
- **REQ-STG-9.5** The system must construct one external reference on each journal entry: financial_institution from the staged entry's source name, reference from fi_reference.
- **REQ-STG-9.6** Stricken.
- **REQ-STG-9.7** On successful posting, each staged entry's status is set to `'Posted'` and an audit record is created.
- **REQ-STG-9.8** Batch posting is atomic: either all postable staged entries are posted successfully or none are. If any entry fails domain validation, the entire batch rolls back.
  - *Why:* All-or-nothing prevents a half-posted run that requires manual reconciliation to determine what went in and what did not. (2026-08-08)
- **REQ-STG-9.9** The system must produce one journal entry per staged entry. Staged entries are not combined into aggregate journal entries.
  - *Why:* One-to-one mapping preserves auditability. (2026-08-08)
- **REQ-STG-9.10** On posting, the system must record on the staged entry the ID of the journal entry it produced, and on each staged line the ID of the journal entry line it produced. Staged lines are paired to journal entry lines by account, line type and amount, not by position; each journal entry line pairs with exactly one staged line.
  - *Why:* See scope exclusion 2 (reversed). (2026-09-26)
- **REQ-STG-9.11** Batch post returns the same before and after trial balances as shadow post (REQ-STG-8.3, REQ-STG-8.5), and states that the post was not rolled back.


## 10. Staged entry query behaviors

- **REQ-STG-10.1** The system must provide a means to retrieve staged entries matching a combination of filter criteria. All filters are optional; when none are provided, the query returns all staged entries.
- **REQ-STG-10.2** The following filter criteria are supported, applied as a conjunction (AND): staged entry ID, source file, date range (begin and end inclusive) or fiscal period key (resolved to that period's date range), description (case-sensitive partial match), ingestion source, FI reference, status, staged line ID, amount, line type, account, memo, journal entry ID, and journal entry line ID.
  - *Why:* The operator's primary tool for reviewing staged data. Filters that span both header-level and line-level properties allow queries like "show me all Classified entries from TestBank where amount is $50.00." Description uses partial match because FI descriptions are long institution-specific strings; the operator needs to search by merchant name fragments, not exact strings. The journal entry filters trace a posted ledger entry back to the staged data it came from. (2026-08-23, match semantics added 2026-08-25, classification rule ID replaced by journal entry IDs 2026-09-26)
- **REQ-STG-10.3** When any line-level filter is applied (line ID, amount, line type, account, memo, journal entry line ID), the query identifies matching staged entries by their lines, then returns the complete staged entry with all its lines — not just the matching lines.
  - *Why:* A staged entry is the unit of work. Returning partial entries would break downstream operations that expect balanced entries with all legs present. (2026-08-23)
- **REQ-STG-10.4** The query must support sorting. Supported sort options: entry date (ascending/descending), ingestion source (ascending/descending), status (ascending/descending), description (ascending/descending).
- **REQ-STG-10.5** When no filter matches any staged entry, the query returns an empty list, not an error.
- **REQ-STG-10.6** Each returned staged entry must include its full composition: header, all lines, and all status transitions.
  - *Why:* The status transition history is the audit trail. A query result without it forces a second round-trip to understand how an entry reached its current state. (2026-08-23)
- **REQ-STG-10.7** When the account filter names an account code that does not resolve to any existing account in the ledger, the system must produce a typed error.
  - *Why:* The account filter accepts a code at the boundary and resolves it to an internal ID. A code that matches nothing is a caller error, not an empty-result condition — silently returning no results would be indistinguishable from "no staged entries match." This is the same pattern as REQ-CR-4.3 (create rejects unknown account) and REQ-FP-3.6 (operations reject unknown period key). (2026-08-23)


## Waived from testing

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-STG-2.1 | UUID is a value type; uniqueness enforced by PK constraint. Same rationale as REQ-JE-1.1/1.2. | Dan 2026-08-16 |
| REQ-STG-2.10 | Same as REQ-STG-2.1. | Dan 2026-08-16 |
| REQ-STG-2.18 | Same as REQ-STG-2.1. | Dan 2026-08-16 |
| REQ-STG-2.11 | Non-nullable FK; structurally enforced. Same rationale as REQ-JE-1.29. | Dan 2026-08-16 |
| REQ-STG-2.19 | Same as REQ-STG-2.11. | Dan 2026-08-16 |
| REQ-STG-3.5 | UUID generation via Guid.NewGuid() in create functions; uniqueness enforced by PK constraint. Same rationale as REQ-STG-2.1. | Dan 2026-08-16 |
| REQ-STG-3.8 | AccountId is an option type. Null input maps to None by construction; no code path transforms null into a value. | Dan 2026-08-16 |
| REQ-STG-1.1 | Definitional — states the file format (JSONL), not a testable behaviour. The parser reads newline-delimited JSON by construction. | Dan 2026-08-18 |
| REQ-STG-1.2 | Definitional — states what a record represents, not a testable behaviour. The record-to-line mapping is structural (one JSON object → one StagedEntryLine). | Dan 2026-08-18 |
| REQ-STG-1.4 | Structural — group_id is a required string field on BaseStageRawRow. Visible by inspection. | Dan 2026-08-18 |
| REQ-STG-9.9 | postStageEntry takes a single StageEntry and produces one JE. The calling loop is structural; no aggregation code exists. | Dan 2026-08-16 |

## Unenforceable

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| REQ-STG-1.16 | "Unique within the file. Not globally unique" — file-scoped uniqueness is consumed by the grouping step (constructSetFromRaw) and discarded. No persistent state to assert against. | Dan 2026-08-18 |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
| REQ-STG-2.8 | Staged entry journal_entry_id is nullable; set after posting to reference the resulting journal entry header. | Back-trace from staging to ledger is not a system concern. The JE's external reference (constructed from fi_source + fi_reference at posting) provides the link back to source data. (2026-08-08) |
| REQ-STG-2.24 | The status column on a staged entry must always reflect the `to_status` of the entry's most recent audit record. The audit trail is the source of truth; the column is a denormalization for read performance. | Status column removed (Option 4). Status is now derived from the audit trail at read time. The drift risk this requirement guarded against is eliminated by construction. (2026-08-24) |
| REQ-STG-7.4 | Staged entries with null fi_reference are never flagged as duplicate by the automated dedup pass. | fi_reference is now required (REQ-STG-1.11, REQ-STG-2.5). Parsers must produce a deterministic reference for every source, ensuring universal dedup coverage. (2026-08-08) |
| REQ-STG-9.6 | When the staged entry's fi_reference is null, no external reference is created on the journal entry. | fi_reference is now required; an external reference is always created (REQ-STG-9.5). (2026-08-08) |
| REQ-STG-2.16 | Staged line classification_rule_id is nullable. When set, identifies the classification rule that assigned the account. | The staged line no longer carries the rule. Every match is recorded in the classification run under a run ID (REQ-STG-5.10), which keeps losing and tied matches too. (2026-09-26) |
