# Data Ingestion

Behavioral specs for the staging and ingestion pipeline — the mechanism by which external financial data enters the SonOfLeo ledger. Data flows through a universal staging area where it is validated, classified, deduplicated, reviewed, and batch-posted as journal entries through the existing JE domain model.

**Design note — naming.** Identifiers in this spec (e.g. `fi_source`, `entry_date`, `line_type`) name domain concepts for readability. They do not prescribe variable names, function names, property names, or any other naming convention in the code or tests.

**Design note — system boundary.** SonOfLeo's ingestion boundary is "a valid base staging format file appeared." The files are produced by bespoke parsers — lightweight, institution-specific scripts that live outside this repository. Each parser reads a financial institution's native export format and converts it to the base staging format. Parsers know nothing about accounts, classification, or the ledger. The staging pipeline knows nothing about CSV column positions, JSON shapes, or FI-specific quirks. The two meet at the file format and nowhere else.

**Design note — file format choice.** The base staging format is JSONL (newline-delimited JSON, one object per line). No industry interchange standard models multi-leg journal entry decomposition from the consumer's perspective. OFX, QIF, FIX, BIAN, and Plaid's transaction model were evaluated; all are single-entry, per-account formats designed for FI-to-consumer or FI-to-FI communication. The staging format's job is different: it carries the parser's decomposition of an economic event into journal entry legs, including legs the parser already knows the account for and legs it leaves for classification.

**Design note — amount sign convention.** The base staging format carries amount as a positive value in every record. Direction is expressed by the line_type field. How the parser derives line_type from the source's own debit/credit/sign conventions is parser-specific — the format constrains only the result: positive amount, explicit direction.

**Design note — parser latitude.** The description field in the base staging format is the raw string from the financial institution, untransformed. However, the parser has full latitude to implement deterministic pre-processing of the overall record set. A payroll parser decomposes a paystub into 10 fully-assigned legs. A tenant-payment parser queries obligation data to produce a rent/utility split. A mortgage parser reads amortization data to split principal from interest. In each case the parser produces the full leg decomposition with account_code populated on every line. The classification step has nothing to do on these entries — it exists for the simple bank transactions where only the cash leg is known and the expense account must be determined by pattern matching.

**Design note — staging schema.** Staged data is stored in normalized tables within the `ingestion` schema: `ingestion.staged_entry` (one row per economic event, carrying entry-level fields), `ingestion.staged_entry_line` (one row per journal entry leg, carrying amount, direction, and account assignment), `ingestion.staged_entry_audit` (one row per status transition, providing a complete audit trail), and `ingestion.source` (a lookup entity identifying financial institution sources). This mirrors the journal entry's header/line structure because a staged entry is a draft journal entry held in a review area.

**Design note — account codes, not IDs.** Staged lines reference accounts by code, not by UUID. Classification rules map description patterns to account codes. The Saturday review surface shows account codes. Account code to account ID resolution happens at posting time, when the staged entry crosses into the ledger domain.

**Design note — classification authority hierarchy.** Three layers assign account codes to staged lines, in descending order of authority: (1) the bespoke parser, which assigns codes only when it knows the answer with certainty (payroll splits, tenant payment decompositions, mortgage allocations); (2) the classification rules engine, which fills null account_codes by pattern matching against the description; (3) the operator, who manually assigns or overrides during review. Each layer acts only where no higher-authority layer has already assigned a code. A parser that cannot determine the account leaves account_code null — it does not guess. This hierarchy is why the classifier cannot override parser assignments (REQ-STG-5.3) and why fully parser-assigned entries skip classification and transition directly to `'Classified'` (REQ-STG-5.8). (2026-08-15)

**Design note — fi_reference is required.** Every parser must produce an fi_reference — the dedup key. For sources without a natural transaction ID (e.g., paystubs), the parser derives a deterministic reference (e.g., the check date). This ensures the dedup pass has universal coverage; no class of transaction is invisible to duplicate detection.

**Scope exclusions — deliberately not supported by the staging pipeline.** These are design decisions, not gaps. Each was evaluated and excluded during the initial design (2026-08-08).

1. **Journal entry comments.** The staging format carries no comment fields. Comments are editorial artifacts that describe relationships and corrections which do not exist until after posting. Attach comments after posting via the CLI (JournalEntryCrud §5).
2. **Back-trace from staging to ledger.** The staged entry does not record which journal entry it became after posting. The staged line does not record which journal entry line it became. Tracing from staging to ledger is not a system concern — the staging pipeline's job ends at posting. If provenance is needed, the journal entry's external reference (constructed from the staged entry's fi_source and fi_reference at posting time) provides the link back to the original FI data.
3. **Multiple external references per journal entry.** A staged entry carries one fi_source and one fi_reference, producing exactly one external reference on the resulting JE. An imported transaction has one financial-institution identity. Additional external references can be attached after posting (REQ-JE-4.10).
4. **Obligation instance linking.** The staging pipeline has no mechanism for linking journal entries to obligation instances. Obligation linking requires knowledge of the instance ID, which comes from the obligation domain, not the FI data. Link after posting via the CLI.
5. **Voids, adjustments, and corrections.** Staging handles incoming financial data only. Voids and corrections operate on existing ledger state via dedicated CLI commands (JournalEntryCrud §4).
6. **Period close entries.** Closing and reversing entries are domain operations, not imported data.
7. **Classifier override of parser-assigned accounts.** The classification rules engine can only assign an account where the staged line's account_code is null. It cannot override a value assigned by the parser. Manual override is available via the review step (§6). This ensures that a parser's deterministic decomposition is authoritative within its domain.


## 1. Base staging file format

The base staging format is the interface contract between bespoke parsers and the ingestion step. These requirements define what the system accepts; a file that violates any requirement is rejected at ingestion.

- **REQ-STG-1.1** The base staging format is JSONL: one JSON object per text line, newline-delimited.
- **REQ-STG-1.2** Each record in the file represents one future journal entry line.
- **REQ-STG-1.3** Records sharing a `group_id` value within a single file form one economic event. All records in a group will produce one journal entry when posted.
- **REQ-STG-1.4a** `group_id`: required, string.
- **REQ-STG-1.4b** `group_id` is unique within the file. Not globally unique — the ingestion step replaces it with a system-generated staged entry ID.
  - *Why:* group_id is a local association mechanism for the parser. Global identity is the staged entry's UUID, assigned at ingestion. (2026-08-08)
- **REQ-STG-1.5** `entry_date`: required, ISO 8601 calendar date (`yyyy-MM-dd`). Must parse to a valid Calendar Date.
- **REQ-STG-1.6** `amount`: required, positive decimal with exactly two decimal places. Maximum value 9,999,999,999.99. Direction is expressed by `line_type`, not by sign.
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


## 2. Staged data states

### Staged entry (`ingestion.staged_entry`)

- **REQ-STG-2.1** Staged entry ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.2** Staged entry date is a Calendar Date. Cannot be null.
- **REQ-STG-2.3** Staged entry description cannot be null and cannot be whitespace only (post-trim per REQ-SYS-1.1). Maximum 1000 characters.
- **REQ-STG-2.4** Staged entry must reference a source in `ingestion.source` (source_id foreign key, not null).
- **REQ-STG-2.5** Staged entry fi_reference cannot be null. Maximum 100 characters.
- **REQ-STG-2.6** Staged entry source_file cannot be null. Records the filename (not path) of the base staging format file that produced this entry.
- **REQ-STG-2.7** Staged entry status cannot be null. Must be one of the values defined in §4.
- **REQ-STG-2.8** Stricken.
- **REQ-STG-2.9** A staged entry must have at least two staged lines.

### Staged line (`ingestion.staged_entry_line`)

- **REQ-STG-2.10** Staged line ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.11** Staged line must belong to exactly one staged entry (entry_id foreign key, not null).
- **REQ-STG-2.12** Staged line amount is a positive decimal(12,2) value (greater than zero).
- **REQ-STG-2.13** Staged line line_type must be `'Debit'` or `'Credit'`.
- **REQ-STG-2.14** Staged line account_code is nullable. When set, holds the account code string identifying the target account.
- **REQ-STG-2.15** Staged line memo is optional (nullable). When provided, cannot be whitespace only (post-trim per REQ-SYS-1.1). Maximum 1000 characters.
- **REQ-STG-2.16** Staged line classification_rule_id is nullable. When set, identifies the classification rule that assigned the account_code. The vendor classification rules entity is specified separately.
- **REQ-STG-2.17** Within a staged entry, the sum of all line amounts where line_type is `'Debit'` must equal the sum of all line amounts where line_type is `'Credit'`.

### Staged entry audit (`ingestion.staged_entry_audit`)

- **REQ-STG-2.18** Audit record ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-STG-2.19** Audit record must reference a staged entry (entry_id foreign key, not null).
- **REQ-STG-2.20** Audit record from_status is nullable (null on the initial ingestion transition).
- **REQ-STG-2.21** Audit record to_status cannot be null.
- **REQ-STG-2.22** Audit record changed_at is a non-null Instant.
- **REQ-STG-2.23** Audit record change_mechanism cannot be null. Must be one of: `'StageIngestion'`, `'Classifier'`, `'Deduplicator'`, `'Operator'`, `'LedgerPoster'`.


## 3. Ingestion behaviors

- **REQ-STG-3.1** The system must provide a means to ingest a base staging format file.
- **REQ-STG-3.2** The system must validate every record in the file against the format requirements in §1. A record that fails validation must be rejected with a typed error identifying the record and the violation.
- **REQ-STG-3.3** If any record in the file fails validation, the entire file is rejected. No staged entries or lines are created. Partial ingestion is not permitted.
  - *Why:* A group that loses a record to validation cannot produce a balanced entry. All-or-nothing prevents orphaned legs. (2026-08-08)
- **REQ-STG-3.4** For each group in the file, the system must create one staged entry and one staged line per record. The staged entry's entry_date, description, and fi_reference are populated from the group's shared values. The source_id is resolved from the group's fi_source. The source_file is the filename of the ingested file.
- **REQ-STG-3.5** The system must generate a UUID for each staged entry and each staged line.
- **REQ-STG-3.6** When a record's fi_source does not resolve to an existing source in `ingestion.source`, the system must reject the file.
  - *Why:* An unrecognized source indicates a parser misconfiguration, not a classification concern. (2026-08-08)
- **REQ-STG-3.7** When a record's account_code is non-null, the system must validate it resolves to an existing account in the chart of accounts. If it does not, the system must reject the file. The staged line stores the account code (not the resolved account ID); code-to-ID resolution occurs at posting time.
  - *Why:* A parser-assigned account code that does not exist is a parser defect. Fail fast. Account codes (not IDs) are stored because the review surface and classification rules operate on codes. (2026-08-09)
- **REQ-STG-3.8** When a record's account_code is null, the staged line's account_code is set to null.
- **REQ-STG-3.9** On successful ingestion, every staged entry's status is set to `'ingested'` and an audit record is created (from_status null, to_status `'ingested'`).
- **REQ-STG-3.10** Ingestion is atomic: either the entire file is ingested (all entries and lines persisted) or no rows are created.


## 4. Status lifecycle

- **REQ-STG-4.1** A staged entry's status must be one of: `'Ingested'`, `'Classified'`, `'NoMatch'`, `'Conflict'`, `'Reviewed'`, `'Duplicate'`, `'Posted'`, `'Ignored'`.
- **REQ-STG-4.2** `'Posted'` is a terminal status. No transitions out of `'Posted'` are permitted.
- **REQ-STG-4.3** Every status transition must create an audit record in `ingestion.staged_entry_audit`.
- **REQ-STG-4.4** A staged entry is postable when its status is `'Classified'` or `'Reviewed'`. No additional filtering (e.g. line-level account_code presence) is applied — if the upstream invariants are sound, all lines are coded by the time an entry reaches these statuses. If they are not, posting fails loudly at account_code resolution (REQ-STG-9.4) rather than silently excluding the entry.
- **REQ-STG-4.5** `'Ignored'` marks an entry that should not be posted due to data problems at the source. The deduplication pass must treat `'Ignored'` entries as matches — re-importing a transaction that was deliberately ignored must flag the new entry as duplicate, not silently re-admit it.
  - *Why:* Without this, voiding a bad JE and ignoring its staged source would cause the next overlapping file import to re-ingest the same bad data. (2026-08-09)

Valid transitions:

```
Ingested   → Classified  (all lines have account_codes after classification)
Ingested   → NoMatch     (at least one line has no rule match after classification)
Ingested   → Conflict    (at least one line has multiple rule matches at equal priority)
Ingested   → Duplicate   (dedup identifies this entry as a duplicate)
Ingested   → Ignored     (operator deliberately excludes the entry)
Classified → Duplicate   (dedup re-run finds a match after classification)
Classified → Reviewed    (operator confirms or adjusts)
Classified → Ignored     (operator deliberately excludes the entry)
Classified → Posted      (batch post)
NoMatch    → Duplicate   (dedup re-run finds a match)
NoMatch    → Reviewed    (operator manually assigns missing accounts)
NoMatch    → Ignored     (operator deliberately excludes the entry)
Conflict   → Duplicate   (dedup re-run finds a match)
Conflict   → Reviewed    (operator resolves the conflict)
Conflict   → Ignored     (operator deliberately excludes the entry)
Duplicate  → Reviewed    (operator overrides — legitimate duplicate)
Duplicate  → Ignored     (operator deliberately excludes the entry)
Ignored    → Reviewed    (operator resurrects a previously ignored entry)
Reviewed   → Ignored     (operator deliberately excludes the entry)
Reviewed   → Posted      (batch post)
```


## 5. Classification behaviors

The classification step runs the vendor classification rules engine against staged entries. The rules entity (pattern, priority, FI scoping, account mapping) is specified separately. These requirements govern how the staging pipeline interacts with the rules engine.

- **REQ-STG-5.1** The system must provide a means to run automated classification against staged entries with status `'ingested'`.
- **REQ-STG-5.2** Classification evaluates each staged line whose account_code is null against the vendor classification rules, matching on the staged entry's description.
- **REQ-STG-5.3** Classification must not modify a staged line whose account_code is already non-null.
  - *Why:* Parser-assigned accounts are authoritative. The classifier fills gaps; it does not override. (2026-08-08)
- **REQ-STG-5.4** When exactly one rule matches and the line's account_code is null, the classifier assigns the rule's account code to the line and records the classification_rule_id on the staged line.
- **REQ-STG-5.5** When multiple rules match and one has strictly higher priority, the classifier assigns the highest-priority rule's account code.
- **REQ-STG-5.6** When multiple rules match with equal priority for a line with null account_code, the staged entry's status is set to `'conflict'`.
- **REQ-STG-5.7** When no rule matches a line with null account_code, the staged entry's status is set to `'NoMatch'`.
  - *Why:* `'NoMatch'` means the classifier ran and found nothing — it is distinct from "not yet classified." The name was chosen over "unclassified" to avoid ambiguity. (2026-08-09)
- **REQ-STG-5.8** When classification completes and every line in the staged entry has a non-null account_code, the entry's status is set to `'classified'`.


## 6. Manual review behaviors

- **REQ-STG-6.1** The system must provide a means for an operator to assign or override the account_code on a staged line, regardless of whether the account was previously set by a parser or the classifier.
- **REQ-STG-6.2** The manual update mechanism allows the operator to set any field on the staged entry and its lines, including status. The system validates the result (balanced entry, valid account codes, legal status transition) but does not infer or auto-assign status from the operator's changes.
  - *Why:* Original spec auto-transitioned to `'Reviewed'` on any line modification. Overruled — manual intervention is the highest authority tier, and the operator knows the intended status. Inferring it revokes that authority. (2026-08-16)
- **REQ-STG-6.3** The operator may override a duplicate flag, transitioning the entry's status from `'duplicate'` to `'reviewed'`.
  - *Why:* Legitimate duplicate transactions exist (two identical charges on the same day). The operator, not the system, makes this call. (2026-08-08)


## 7. Deduplication behaviors

- **REQ-STG-7.1** The system must provide a means to run deduplication against staged entries.
- **REQ-STG-7.2** A staged entry is flagged as duplicate when another staged entry shares the same source_id and fi_reference values. This includes `'Ignored'` entries (per REQ-STG-4.5).
  - *Why:* The original parenthetical excluded `'Posted'` entries on the assumption that REQ-STG-7.3 covered them via the ledger. Removed — each requirement should stand alone, and a Posted staged entry with the same key is evidence of a duplicate regardless of whether 7.3 exists. (2026-08-15)
- **REQ-STG-7.3** A staged entry is flagged as duplicate when a posted journal entry in the ledger carries an external reference whose financial_institution and reference values match the staged entry's source and fi_reference.
  - *Why:* Prevents re-importing transactions that were posted in a prior cycle. (2026-08-08)
- **REQ-STG-7.4** Stricken.
- **REQ-STG-7.5** Flagging a staged entry as duplicate must not alter its lines or their account assignments.


## 8. Shadow post behaviors

- **REQ-STG-8.1** The system must provide a means to simulate posting all postable staged entries without modifying ledger state.
- **REQ-STG-8.2** Shadow post must construct journal entries through the same domain model and validation path used by batch post (§9). The construction occurs within a database transaction that is rolled back after completion.
  - *Why:* A shadow post that skips domain validation gives false confidence. If a staged entry would fail validation (closed fiscal period, inactive account, imbalanced lines), shadow post must surface that failure identically. (2026-08-08)
- **REQ-STG-8.3** Shadow post must produce a trial balance before posting and a trial balance after posting (computed within the rolled-back transaction). The caller derives the delta.
  - *Why:* Original spec required only a delta. The full before/after is more useful — the Saturday routine reconciles against point-in-time account balances, not movements. A delta alone can't be reconciled without a second call. (2026-08-16)
- **REQ-STG-8.4** Shadow post must not modify any staged entry's status or any staging data. It is read-only against the staging tables and write-then-rollback against the ledger.


## 9. Batch post behaviors

- **REQ-STG-9.1** The system must provide a means to batch-post all postable staged entries to the ledger.
- **REQ-STG-9.2** For each postable staged entry, the system must construct a journal entry through the domain model (JournalEntryCrud §2), applying all existing JE validations.
- **REQ-STG-9.3** The journal entry header fields are mapped from the staged entry: description from the staged entry's description, entry_date from the staged entry's entry_date. Source is a fixed provenance label (e.g. "Data ingestion import") describing *how* the entry was created, not which FI it came from — the FI identity lives on the external reference (REQ-STG-9.5).
- **REQ-STG-9.4** For each staged line, the system must resolve the line's account_code to an account ID via the chart of accounts and construct a journal entry line with the line's amount, line_type, resolved account ID, and memo. A null account_code at posting time is a loud failure — it indicates a broken upstream invariant (classification or review allowed an uncoded line through). Invalid non-null codes cannot occur: the chart of accounts is FK-constrained.
- **REQ-STG-9.5** The system must construct one external reference on each journal entry: financial_institution from the staged entry's source name, reference from fi_reference.
- **REQ-STG-9.6** Stricken.
- **REQ-STG-9.7** On successful posting, each staged entry's status is set to `'posted'` and an audit record is created.
- **REQ-STG-9.8** Batch posting is atomic: either all postable staged entries are posted successfully or none are. If any entry fails domain validation, the entire batch rolls back.
  - *Why:* All-or-nothing prevents a half-posted run that requires manual reconciliation to determine what went in and what did not. (2026-08-08)
- **REQ-STG-9.9** The system must produce one journal entry per staged entry. Staged entries are not combined into aggregate journal entries.
  - *Why:* One-to-one mapping preserves auditability. (2026-08-08)


## Waived from testing

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-STG-2.1 | UUID is a value type; uniqueness enforced by PK constraint. Same rationale as REQ-JE-1.1/1.2. | Dan 2026-08-16 |
| REQ-STG-2.10 | Same as REQ-STG-2.1. | Dan 2026-08-16 |
| REQ-STG-2.18 | Same as REQ-STG-2.1. | Dan 2026-08-16 |
| REQ-STG-2.11 | Non-nullable FK; structurally enforced. Same rationale as REQ-JE-1.29. | Dan 2026-08-16 |
| REQ-STG-2.19 | Same as REQ-STG-2.11. | Dan 2026-08-16 |
| REQ-STG-3.5 | UUID generation via Guid.NewGuid() in create functions; uniqueness enforced by PK constraint. Same rationale as REQ-STG-2.1. | Dan 2026-08-16 |
| REQ-STG-3.8 | AccountCode is an option type. Null input maps to None by construction; no code path transforms null into a value. | Dan 2026-08-16 |
| REQ-STG-1.1 | Definitional — states the file format (JSONL), not a testable behaviour. The parser reads newline-delimited JSON by construction. | Dan 2026-08-18 |
| REQ-STG-1.2 | Definitional — states what a record represents, not a testable behaviour. The record-to-line mapping is structural (one JSON object → one StagedEntryLine). | Dan 2026-08-18 |
| REQ-STG-1.4a | Structural — group_id is a required string field on BaseStageRawRow. Visible by inspection. | Dan 2026-08-18 |
| REQ-STG-9.9 | postStageEntry takes a single StageEntry and produces one JE. The calling loop is structural; no aggregation code exists. | Dan 2026-08-16 |

## Unenforceable

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| REQ-STG-1.4b | "Unique within the file. Not globally unique" — file-scoped uniqueness is consumed by the grouping step (constructSetFromRaw) and discarded. No persistent state to assert against. | Dan 2026-08-18 |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
| REQ-STG-2.8 | Staged entry journal_entry_id is nullable; set after posting to reference the resulting journal entry header. | Back-trace from staging to ledger is not a system concern. The JE's external reference (constructed from fi_source + fi_reference at posting) provides the link back to source data. (2026-08-08) |
| REQ-STG-7.4 | Staged entries with null fi_reference are never flagged as duplicate by the automated dedup pass. | fi_reference is now required (REQ-STG-1.11, REQ-STG-2.5). Parsers must produce a deterministic reference for every source, ensuring universal dedup coverage. (2026-08-08) |
| REQ-STG-9.6 | When the staged entry's fi_reference is null, no external reference is created on the journal entry. | fi_reference is now required; an external reference is always created (REQ-STG-9.5). (2026-08-08) |
