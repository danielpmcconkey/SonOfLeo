# Panel: Customer Review (Fable 5)

9 findings (2 high, 4 medium, 3 low)

## CUST-1 — HIGH (customer-gap)
**Location:** Src/Model/UI/InterfaceContractTypes.fs:99, Src/ModelOrchestrator/AccountBalance.fs:50-73
**Summary:** Balance query has no as-of date — only inception-to-now balances are retrievable.
**Detail:** FetchBalances takes only a code list and sums every non-voided line with no date bound. Saturday routine needs point-in-time balances: Phase 4b integrity check uses --as-of <period-end-date>, Phase 4a triage asks 'balance as of the recon capture date.'
**Suggested Action:** Add optional asOf LocalDate to the balance fetch contract (predicate: je.entry_date <= @as_of).
**Why:** Reconciliation is the weekly control gate; both checks are keyed to a period-end date. A balance query that can only answer 'now' cannot support either.
**Owner:** fix-spec

## CUST-2 — HIGH (customer-gap)
**Location:** Src/Model/UI/InterfaceContractTypes.fs:86-97, Src/ModelOrchestrator/AccountActivity.fs:125-143
**Summary:** Ledger search lacks amount and description pattern filters — the two recon triage keys.
**Detail:** The #1-ranked ask in the usage memo. Recon triage starts from a dollar delta ('find the $232.01') and review-stack from a merchant string ('the Blumenthal refund'). Both impossible without fetching a whole date range and grepping client-side.
**Suggested Action:** Add optional amount (exact, 2dp) and descriptionPattern (ILIKE) filters.
**Why:** Every unexplained recon delta begins with a lookup by amount or merchant string. The CLI can write entries it cannot find.
**Owner:** fix-spec

## CUST-3 — MEDIUM (customer-gap)
**Location:** Src/ModelOrchestrator/AccountBalance.fs:93 vs REQ-JE-3.6
**Summary:** netBalance sign convention: spec says credits-debits, code computes debits-credits. No test pins it.
**Detail:** REQ-JE-3.6 defines net as 'credits minus debits.' The implementation calls MoneyModule.subtract debits credits (debits-credits — opposite sign). Only netBalance assertion is against zero.
**Suggested Action:** Pick a convention, correct spec or code, add a test asserting non-zero signed balance.
**Why:** A signed field consumed weekly by reconciliation whose sign is documented one way and implemented the other is a latent breaking change.
**Owner:** dan-decides

## CUST-4 — MEDIUM (customer-gap)
**Location:** Src/Model/UI/InterfaceContractTypes.fs:35-45
**Summary:** Account activity return omits the counter-account — the field a spending review actually reads.
**Detail:** To learn that a SECU debit was categorized to 5650 Entertainment, the consumer must issue JournalEntry FetchById per row — N+1 CLI invocations for every weekly review.
**Suggested Action:** Add sibling-line summaries to AccountActivityDetailReturn, or explicitly rule that categorization review uses JE-FetchByDateRange.
**Why:** Categorization review is the point of the weekly walk; a return type that shows the transaction but hides its categorization sends the consumer back to per-row fetches.
**Owner:** dan-decides

## CUST-5 — MEDIUM (customer-gap)
**Location:** Src/SonOfLeoCli/JournalEntryRoutes.fs (no reclass verb), Specs/Behavioral/JournalEntryCrud.md section 4
**Summary:** No atomic reclass (void+repost) verb — the #2-ranked ask from predecessor usage.
**Detail:** Reclass is weekly. Today requires 4 invocations with human-copied fields. An atomic JournalEntry Reclass would also capture entry-linkage signal for the ML engine via automatic secondary-JE comments.
**Suggested Action:** Spec and build a JournalEntry Reclass verb.
**Why:** Weekly operation, historically the largest source of manual error.
**Owner:** fix-spec

## CUST-6 — MEDIUM (customer-gap)
**Location:** Project scope statement
**Summary:** Obligations and portfolio domains appear in neither the done list nor the not-started list.
**Detail:** Saturday Phase 3 (obligations) and Phase 1 portfolio recording are a large fraction of the customer's week. Their absence from both lists is a planning gap.
**Suggested Action:** Confirm they're roadmapped and record where they sit relative to imports/reconciliation.
**Why:** SonOfLeo cannot replace LeoBloom's Saturday without Phase 3.
**Owner:** dan-decides

## CUST-7 — LOW (customer-gap)
**Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-2.6, Src/SonOfLeoCli/FiscalPeriodRoutes.fs:75-89
**Summary:** Fiscal periods must pre-exist — first post of each new month fails until FiscalPeriod Create is run.
**Detail:** Guaranteed monthly friction. A bulk 'create year' verb or auto-create-on-post removes it.
**Suggested Action:** Add a bulk period-creation verb or auto-create for missing future periods.
**Why:** Monthly, predictable, self-inflicted friction in the most-exercised command path.
**Owner:** dan-decides

## CUST-8 — LOW (customer-gap)
**Location:** Src/SonOfLeoCli/Program.fs:18-27
**Summary:** No batch posting — importers would spawn one dotnet process per JE.
**Detail:** Better satisfied by the planned staging domain than a CLI verb. Flagged so the interim is a conscious choice.
**Suggested Action:** Defer the batch verb; record the decision. Staging domain's promote step must post transactionally per file.
**Why:** The underlying need is real but better served by staging.
**Owner:** dan-decides

## CUST-9 — LOW (customer-gap)
**Location:** Specs/Behavioral/JournalEntryCrud.md design notes, Src/Model/UI/InterfaceContractTypes.fs:141-147
**Summary:** Ledger stores no structured counterparty/merchant — ML-grade signal depends on staging retention being first-class.
**Detail:** Mid-horizon docking is clean (external references, source field, comment linkage). But far-horizon: merchant/counterparty signal is free-prose only. Staging retention becomes load-bearing for ML.
**Suggested Action:** When staging is specced, write permanent retention of raw source rows as a requirement.
**Why:** Merchant-level signal is the one unrecoverable input if staging is ever designed as ephemeral.
**Owner:** fix-spec
