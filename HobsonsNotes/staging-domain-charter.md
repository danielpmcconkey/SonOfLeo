# Staging & Import Domain — Project Charter

Draft L1/L2 from the 2026-08-03 design session. Not REQs — this is the
shape of the work before the shape of the specs.

## Purpose

Replace the ad-hoc Python import pipeline with a first-class SonOfLeo
domain that stages, classifies, deduplicates, shadow-posts, and batch-posts
financial transactions from any institution. Ledger transactions only —
portfolio/positions are a separate domain.

---

## L1 — What gets built

### 1. Base Staging Format

A single canonical record shape that every institution's data must be
converted into before SonOfLeo sees it. SonOfLeo has no knowledge of
Synchrony's JSON or Fidelity's CSV — only this format.

Fields (indicative, not final):
- Transaction date
- Amount (signed — positive = money in, negative = money out, from the
  account holder's perspective)
- Description (raw merchant string from the FI)
- FI source identifier (which institution / account)
- FI reference (the institution's own transaction ID, for dedup)
- Source filename / batch tag (provenance)

Open questions:
- Do we need a "direction" field separate from sign, or is sign sufficient?
- Does the FI source identifier map to a specific cash account, or is
  that a classification concern?

### 2. Stage Ingestion (CLI: `stage ingest`)

Reads base-staging-format files from a directory. For each row:
- Validates shape (required fields, types, date format, amount precision)
- Writes to the staging table with status `ingested` and a batch timestamp
- Rejects malformed rows with a typed error — does not skip silently

The ingestion step knows nothing about accounts, classification, or the
ledger. It is purely [DET].

### 3. Vendor Classification Rules Engine

Promotes merchant-to-account mapping from a flat pattern table to a
first-class domain entity with CRUD operations.

A rule maps:
- A match pattern (against the staged row's description)
- To a debit account and a credit account
- With a priority / specificity mechanism for overlapping patterns

The matching must handle:
- Interior wildcards (`%UBER%EATS%` matches "UBER EATS UBER EATS")
- Exact match taking priority over wildcard
- Multiple rules matching the same row → most specific wins, or conflict
  flagged

Open questions:
- Should rules also match on FI source? (e.g., "anything from SECU that
  says ALLSTATE is the auto premium, but from Fidelity Visa it could be
  homeowners")
- Negative rules ("this pattern is NOT this category") — needed, or
  over-engineering?
- Rule versioning / audit trail, or just current state?

### 4. Classification Pass (CLI: `stage classify`)

Runs the rules engine against all staged rows with status `ingested`.
For each row:
- Evaluates all matching rules
- If exactly one match: assigns debit/credit accounts, status → `classified`
- If multiple matches: assigns by priority; if tied, status → `conflict`
- If no match: status → `unclassified`

`unclassified` and `conflict` rows are the exception surface — these are
what the operator reviews and resolves manually.

This step is [DET] (mechanical rule application). The [JUDGE] work is
Dan or Hobson deciding what to do with unclassified/conflict rows.

### 5. Manual Classification (CLI: `stage update`)

Operator assigns or overrides the debit/credit accounts on a staged row.
Status → `reviewed`. This is how unclassified merchants get categorized
and how classification errors get corrected — all before posting.

When the operator classifies an unknown merchant, they can optionally
create a new vendor rule at the same time (or not — one-off transactions
don't need rules).

### 6. Deduplication (CLI: `stage dedup`)

Identifies and flags duplicate staged rows. Dedup operates on:
- FI source + FI reference (same transaction imported twice)
- FI source + date + amount + description (same transaction without a
  stable reference ID)

Flagged duplicates get status → `duplicate`. The operator can override
(legitimate duplicate transactions exist — two identical charges on the
same day).

Also checks staged rows against existing journal entries to prevent
re-importing transactions that were already posted in a prior cycle.

Open questions:
- Is dedup a separate pass, or part of ingestion? (Charter says separate —
  you can re-run it without re-ingesting.)
- How does "already posted" detection work? By FI reference on the
  journal entry's external reference? That's the current LeoBloom
  mechanism and it's proven.

### 7. Shadow Post (CLI: `stage shadow-post`)

Simulates posting all `classified` + `reviewed` rows without writing to
the ledger. Produces:
- A trial balance delta (what the balances would be after posting)
- Per-account impact (which accounts change by how much)
- A diff against the recon file (if provided) — showing which accounts
  would reconcile and which wouldn't

This is the pre-commit review. The operator reads this, adjusts staged
rows (step 5) until satisfied, then commits.

No journal entries are created. No ledger state changes. Read-only
against the ledger, read-write against nothing.

Open questions:
- Does shadow-post also validate JE construction rules (balanced entry,
  period assignment, account activity dates) or just arithmetic?
- Should it produce a structured report object (for programmatic
  comparison) or a human-readable summary (for the Saturday review)?
  Probably both.

### 8. Batch Post from Stage (CLI: `stage post`)

Posts all `classified` + `reviewed` rows to the ledger as journal entries
in a single transaction. For each row:
- Constructs a JE (debit leg + credit leg, or multi-line for splits)
- Attaches the FI reference as an external reference (dedup key for
  future cycles)
- On success: staged row status → `posted`, linked to the JE ID
- On any failure: entire batch rolls back, nothing posts, error surfaced

All-or-nothing. The transaction bracket is the feature.

Open questions:
- One JE per staged row, or batch JEs per FI source? (LeoBloom does
  one-per-row; seems right for auditability.)
- Does the staged row retain its own lifecycle after posting? (For
  historical queries like "show me everything that came in from
  Synchrony in July.")

### 9. Bespoke FI Parsers (outside SonOfLeo)

Lightweight scripts — one per institution — that convert the FI's native
format into the base staging format. These are the only things that break
when an FI changes its export format. They live outside the SonOfLeo repo
(in LeoBloomOps or equivalent).

Each parser:
- Reads the FI's file format (CSV, JSON, PDF-extracted, whatever)
- Writes one or more base-staging-format files
- Knows nothing about accounts, classification, dedup, or the ledger
- Exits non-zero on parse failure

SonOfLeo's boundary is "a valid base-staging-format file appeared."

---

## L2 — Design considerations

### Staging table shape

The staging table is the central artifact. Rough shape:

| Column | Purpose |
|---|---|
| id | PK (UUID) |
| batch_id | Groups rows from a single ingest run |
| fi_source | Institution identifier |
| fi_reference | Institution's transaction ID |
| transaction_date | Date of the transaction |
| amount | Decimal(19,2), signed |
| raw_description | Verbatim from the FI |
| debit_account_id | FK → account (nullable until classified) |
| credit_account_id | FK → account (nullable until classified) |
| status | ingested / classified / reviewed / conflict / unclassified / duplicate / posted |
| journal_entry_id | FK → journal_entry (nullable until posted) |
| batch_timestamp | When ingested |
| source_filename | Provenance |
| notes | Operator notes (optional) |

### Status lifecycle

```
ingested → classified (by rules engine)
         → unclassified (no matching rule)
         → conflict (multiple rules, tied priority)
         → duplicate (dedup pass)

classified → reviewed (operator confirms or overrides)
unclassified → reviewed (operator manually classifies)
conflict → reviewed (operator resolves)

reviewed → posted (batch post)
classified → posted (batch post, if operator trusts the rules)

duplicate → reviewed (operator overrides — legitimate duplicate)
```

### Vendor rules table shape

| Column | Purpose |
|---|---|
| id | PK (UUID) |
| pattern | Match pattern (with wildcard syntax) |
| fi_source | Optional — scope rule to a specific institution |
| debit_account_id | FK → account |
| credit_account_id | FK → account |
| priority | Integer — higher wins on conflict |
| active | Boolean — soft delete |

### What this replaces in LeoBloom

| LeoBloom (current) | SonOfLeo (new) |
|---|---|
| `stage.secu`, `stage.ally`, `stage.synchrony`, etc. (per-FI tables) | One `stage.staged_transaction` table |
| `ledger.vendor_classification_rule` (flat pattern table) | First-class rules entity with priority and FI scoping |
| Python importers doing parse + stage + classify + promote | Parse only (bespoke scripts); everything else via CLI |
| `ledger promote` (per-row, interactive) | `stage post` (batch, all-or-nothing) |
| No dry-run capability | `stage shadow-post` |
| Dedup by checking existing JE external references at promote time | Explicit dedup pass + JE reference check |

### What this does NOT include

- **Portfolio / positions tracking** — separate domain, separate staging needs
- **Obligation management** — existing domain, untouched by this work
- **Reporting** — reads from the ledger; staging doesn't change the reporting surface
- **Period close** — orthogonal; machinery exists, scheduling is Dan's call
- **Bespoke parser implementation** — those stay outside the repo

---

## Phasing (suggested, not committed)

**Phase A — Foundation:** Base staging format spec, staging table schema,
ingestion CLI, status lifecycle. Get rows into the DB in a standardized
shape.

**Phase B — Classification:** Rules entity CRUD, classification pass,
manual update CLI. Get rows categorized.

**Phase C — Dedup + Shadow Post:** Dedup pass, shadow post with trial
balance delta and recon comparison. The review loop.

**Phase D — Batch Post:** All-or-nothing post from stage, JE construction,
external reference attachment, status update.

**Phase E — Migration:** Convert existing LeoBloom Python importers to
thin parsers writing the base staging format. Cut over the Saturday routine.

Each phase is independently shippable and testable. Phase A works without
B (manual classification via `stage update`). Phase C works without D
(shadow post is useful even before batch post exists).
