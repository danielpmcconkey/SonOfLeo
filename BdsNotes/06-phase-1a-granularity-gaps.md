# Phase 1A — Interface Contract Granularity Gaps

Produced 2026-08-01 by BD, three parallel agents, one per domain. Strategy context is
in `05-ledger-hardening-strategy.md`. **Findings only. Nothing here is a decision.**

Method: for every field of every input type in
`Src/InterfaceBridge/InterfaceContracts/`, trace from the route handler through the
orchestrator into the model constructors and record which `AppError` cases that field
can produce. Read-only; nothing was executed.

## The headline

Dan asked whether the argument is 17 vs 21 or 17 vs 54. It is neither, because the
gaps are not one population. They are four, and only two of them are answerable by
adding error cases at all.

| Category | Count | Fixable by adding AppError cases? |
|---|---|---|
| Accidental field collisions | 5 | Yes |
| Relational / joint-condition collisions | 7 | Judgment call — may be correct as-is |
| Operation attribution (multi-purpose contracts) | 4 | Yes |
| **List-position loss** | **11 fields** | **No. Structurally impossible.** |

**Narrow reading** — only the accidental collisions matching Dan's `code` vs
`parentCode` example: **5 new cases.**
**Broad reading** — everything above except list position: **~18 new cases.**
Neither number addresses list position.

## Why list position settles the case-vs-payload question

`Src/Utilities/ResultHelper.fs:15` — `convertListOfResultsToResultsList` folds a
`Result list` and returns the first `Error`. The index is discarded. Eleven fields
across four lists depend on it:

- `JournalEntryInput.lines[]` — accountCode, amount, lineType, memo
- `JournalEntryInput.externalReferences[]` — financialInstitution, referenceText
- `JournalEntryInput.comments[]` — secondaryJournalEntryId, commentText
- `AccountBalanceFetchByAccountListInput.codes[]`

For these, the field identity is *already* distinct. `JournalEntryLineMemoTooLong` is
unambiguous about which field failed. What is missing is **which element** — and an
element index is an unbounded integer. There is no finite set of DU cases that
expresses "line 3." A case-per-position design is not merely expensive, it is
impossible.

So the position payload has to exist regardless of how the field-collision question is
settled. That does not automatically decide field granularity too, but it does mean the
codebase acquires an error-context payload either way, and the marginal cost of putting
the field in it is near zero.

Corollary the multi-fail theories don't currently see: both existing theories only ever
place the bad value in element 0.

## Category 1 — accidental field collisions (5)

Two different fields in the same input type producing the same case, with no semantic
reason.

| Input type | Fields | Shared case |
|---|---|---|
| AccountCreateInput | `code`, `parentCode` | `AccountCodeIsEmpty` |
| AccountCreateInput | `code`, `parentCode` | `AccountCodeTooLong` |
| AccountActivityFilterInput | `accountCode`, `accountParentCode` | `AccountCodeIsEmpty` |
| AccountActivityFilterInput | `accountCode`, `accountParentCode` | `AccountCodeTooLong` |
| JournalEntryUpdateCommentInput | `id`, `secondaryJournalEntryId` | `DalResultantRowsDidntMatchExpectation` |

The first four are two fixes, not four — one `AccountParentCodeIsEmpty` and one
`AccountParentCodeTooLong` serve both input types. This is the exact case Dan named,
and it is the smallest category.

`AccountRoutes.fs:33` already remaps `AccountCodeDoesntMatchAccountId` to
`AccountParentCodeInvalid` for the parent slot — but the `AccountCode.create` failures
inside `fallibleConverterAccountCodeStringToAccountUuid`
(`AccountFieldConverters.fs:17`) are not remapped, so Empty/TooLong leak through
identical to `code`'s. The disambiguation pattern exists; it is applied incompletely.

## Category 2 — relational collisions (7). Judgment call.

The error is genuinely *about a pair of fields*, so a single case may be correct.
Splitting these is a design choice, not a defect fix.

| Input type | Fields | Case |
|---|---|---|
| AccountCreateInput | `accountTypeSt`, `subType` | `AccountInvalidTypeSubtypeCombo` |
| AccountCreateInput | `activeBegin`, `activeEnd` | `AccountActiveEndBeforeBegin` |
| AccountCreateInput | `accountTypeSt`, `parentCode` | `AccountParentAndChildTypesDontMatch` |
| AccountDeactivationInput | `code`, `activeEnd` | `AccountDeactivationProposedDateIsInvalid` |
| AccountDeactivationInput | `code`, `activeEnd` | `AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate` |
| JournalEntryVoidInput | `id`, `reason.secondaryJournalEntryId` | `DalResultantRowsDidntMatchExpectation` |
| JournalEntryAddCommentInput | `journalEntryId`, `comment.secondaryJournalEntryId` | `DalResultantRowsDidntMatchExpectation` |

The last two are *not* really relational — they are the not-found leak below wearing a
relational costume. Both Guids run through the same `validateJournalEntryHeader` and
produce byte-identical errors. Those two should probably move to category 1.

## Category 3 — operation attribution (4)

`FiscalPeriodInput` is one contract serving create, fetch-by-key, close, and reopen.
The caller cannot tell which operation failed, or in two cases what actually went wrong.

1. On fetch/close/reopen, a **malformed** key and a **nonexistent** key both surface as
   `InterfaceBridgeConversionFailure`. Two different faults, one error.
2. The same lookup failure is byte-identical across all three operations.
3. **Closing an already-closed period and reopening an already-open period produce the
   identical error** — `DalResultantRowsDidntMatchExpectation("ExactlyOne", 0)` — which
   is also indistinguishable from a real DAL fault. `toggleOpenFlagById` guards on
   `and is_open = @enforcedCurrentValue`, so the no-op falls out as a row-count miss.
4. Create with a duplicate key surfaces as raw `DalErrorDuringNonQueryExecution`. No
   domain error exists for it.

## The bigger leak, which is not a granularity gap

`DalResultantRowsDidntMatchExpectation` is the universal not-found signal across **8
fields in 6 input types**. It is a DAL row-count assertion doing duty as a domain
error, and it reaches the boundary unmapped.

Fixing the three collisions it causes leaves the leak intact. This is arguably a larger
problem than everything in category 1, and it is upstream of Phase 2 — you cannot write
a test asserting "journal entry not found" today, because that error does not exist.

## Free wins found on the way

**`FiscalPeriodNoPeriodMatchingKey` already exists and is unreachable.** No FiscalPeriod
route calls `FiscalPeriod.fetchIdByKey`, the only site that produces it. The routes go
through `LookupCache.fiscalPeriodKeyToId`, whose miss gets wrapped in
`InterfaceBridgeConversionFailure`. The purpose-built error is dead code. Same for
`FiscalPeriodInvalidKeyString` on that path.

Two routes resolve the same period key from the same cache with different error
vocabularies: `OrchestrationConverters.fs:26-35` yields the good domain errors;
`FiscalPeriodFieldConverters.fs:11-23` wraps both faults in the generic one. This is a
wiring defect, not a missing case — one of the backstops is absorbing a fault that
already has a proper error waiting for it.

## Missing validation — bugs, not granularity gaps

- **`FilterDateRangeInput` has no begin-after-end check anywhere.** An inverted range
  silently returns an empty result set. Both `AccountActivity.fetchFiltered` and
  `JournalEntryOrchestration.fetchHeadersFromFilter` destructure the dates and pass them
  straight to SQL. `AccountActiveEndBeforeBegin` exists for the analogous account case,
  so the asymmetry is unintentional.
- **`JournalEntryFetchByDateRangeInput`** — same, both fields unvalidated.

## No failure vector (9 fields)

Nothing to test, nothing to disambiguate. Useful for scoping Phase 2 so it does not
chase them: `AccountFetchAllInput.activeOnly`, `AccountActivityFilterInput.journalEntryId`,
`AccountActivityFilterInput.unVoidedOnly`, `AccountActivityFetchInput.sort`,
`AccountBalanceFetchByAccountListInput.asOf`, `FiscalPeriodFetchAllInput.openOnly`,
`JournalEntryFetchLinesByAccountInput.nonVoidedOnly`,
`JournalEntryFetchByDateRangeInput.beginDate` and `.endDateInclusive`.

## Unknowns — not asserted, need Dan or a schema check

1. Does `ledger.fiscal_period.period_key` carry a unique constraint? If not, category 3
   item 4 is not a duplicate-key error but a silent duplicate insert, which is worse.
2. Does an FK constraint exist on the comment's secondary journal entry ID?
   `JournalEntryUpdateCommentInput.secondaryJournalEntryId` goes through the infallible
   `JournalEntryHeaderId.fromGuid` and `updateComment` never validates it, so a
   nonexistent ID either FK-violates or silently succeeds. Source alone cannot say which.

Both are answerable from `DbMigrations/`. Not checked — out of the agents' scope.
