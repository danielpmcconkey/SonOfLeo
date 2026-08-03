# gaap-domain-auditor

## MAINT-BATCH-1 — maintainability
- **Location:** Src/Model/Ledger/JournalEntryLine.fs:153, Src/Model/Ledger/JournalEntryExternalReference.fs:146, Src/Model/Ledger/JournalEntryComment.fs:148
- **Summary:** The three fetchByJournalEntryHeaderIdList functions produce invalid SQL when called with an empty list, unlike AccountBalance.fetchByAccountIdList which guards explicitly.
- **Resolution:** fix-code

JournalEntryLine.fetchByJournalEntryHeaderIdList, JournalEntryExternalReference.fetchByJournalEntryHeaderIdList, and JournalEntryComment.fetchByJournalEntryHeaderIdList all build a parameterized IN clause from the input list: `let predicate = $"jel.journal_entry_id in ({names})"`. When the input list is empty, `names` is the empty string, producing `IN ()` which is invalid PostgreSQL syntax. This would surface as a cryptic DalErrorDuringNonQueryExecution rather than a typed domain error. Currently all three are called exclusively from JournalEntry.fetchFiltered (line 295), which guards with `if headers |> List.length = 0 then Ok [] else ...`. By contrast, AccountBalance.fetchByAccountIdList (line 39-40) handles this identically-shaped concern with an explicit match: `| [] -> Error(AccountBalanceFetchInvalidArguments)`. The inconsistency means any future call site that omits the empty-list guard will hit an opaque SQL error instead of the typed AppError that AccountBalance provides. This is relevant to forward readiness because trial-balance and period-close features will likely introduce new composition paths that call these batch-fetch functions.

**Action:** Add an empty-list guard at the top of each of the three fetchByJournalEntryHeaderIdList functions, matching the pattern in AccountBalance.fetchByAccountIdList. Return a typed AppError (or Ok [] if the empty-list case is semantically valid for the caller).

**Why:** Defensive coding at the function boundary prevents latent defects from surfacing as cryptic database errors when new call sites are added. The existing guard in the single caller is correct but fragile -- a second caller must independently discover the constraint. The inconsistency with AccountBalance (which guards the same pattern) makes it easy to assume these functions are similarly safe.

