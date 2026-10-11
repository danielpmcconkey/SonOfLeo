# code-truthfulness-auditor:Business.CrossDomainOrchestration

_No findings._

## Reasoning

Before reviewing, I read the audit-conduct catalog, all 12 of its articles and the full resolved-findings.md. I also read the architecture articles on the orchestration layer, DAL errors as backstops and validation layers. Most of the review went to what changed since the last audit (git diff 22af7f4..HEAD over Business.CrossDomainOrchestration: 24 files) and to the slice-2 behaviour, checked against Positions §1, §5–8, §11–15 and Reporting §8–10, which I read in full.

What I checked:

(1) PreLedgerBalanceOrchestration against REQ-POS-15.1 to 15.6.
- Asset/Liability type check with a typed error naming the code and type.
- Linked-account refusal for both Investment Account links and Property asset links, with mortgage accounts left out. Matches 15.3.
- Must be before the first fiscal period, and fails when no fiscal period exists: PreLedgerBalance.confirmBeforeLedger uses balanceDate >= start and None, so 15.2 holds.
- A repeat within one request is rejected.
- Replace-on-existing, with replacedExisting returned. Matches 15.4.
- Delete of a missing balance gives a not-found error naming the code and date (15.5).
- List rejects end before begin and is sorted by code, then date (15.6). The Map.find there is on a value guaranteed by a foreign key. Account.fetchAll is called with activeOnly=false, so inactive accounts cannot make that lookup miss.

(2) FiscalPeriodCreation against REQ-POS-15.7.
- The guard takes the earliest start across the new period and the existing ones, then fetches balances dated on or after it.
- Given the 15.2 invariant and account type being immutable (REQ-AC-4.22), this rejects exactly the balances dated on or after the new start. I found no code that deletes a fiscal period or changes its dates.
- ensureFiscalPeriods goes through the same constructNewAndPersist, and it is the only fiscal-period persist call site.

(3) PositionsLedgerLinks.confirmCarriesNoPreLedgerBalance against REQ-POS-15.8. It is called on create and update for both investment-account links (confirmInvestmentAccountLink) and property asset links (confirmAssetLink). confirmMortgageLink does not call it, as the spec says.

(4) InvestmentActivityOrchestration against REQ-POS-12.1 to 12.9 and 13.1 to 13.5.
- Range checks: end not before begin, end not after the current date taken from the initiation instant. Overlapping ranges are refused. Each date must fall inside its range and inside the account's active period.
- confirmShape covers 12.6 and 12.7. A Security must already be held, and no Holding is created.
- Per-date ordinals match the UNIQUE(account, date, ordinal) constraint in the migration.
- A range is deleted and then re-inserted, and removed/recorded counts are returned.
- The list rejects end before begin and returns rows in date and ordinal order.

(5) UnitRollForward against REQ-POS-14.1 to 14.3.
- first < second is enforced, and each date must have a snapshot.
- The activity window is (first, second], and only units-moving kinds count.
- Keying rows by Holding equals keying by Security, because Holding is unique per account and Security.
- Quantity.sum returns a decimal, so expected and difference are plain decimals.
- A difference is returned as data, and rows are sorted by Security name.
- I considered flagging "the date or dates at fault" when both snapshots are missing. Only the first date gets named. A reasonable reading ties "dates" to the ordering error, so I did not raise it.

(6) AccountSnapshotOrchestration and HoldingsAsOf for lots (REQ-POS-6.9 to 6.13, 7.2, 7.5, 8.3).
- Lots are only allowed on Taxable accounts. Lot quantities are confirmed against the line, and ordinals are assigned in the order supplied.
- On replace, lots are deleted before lines, matching the ON DELETE RESTRICT foreign key in the migration.
- The header update always sets provenance and contribution basis, so the PositionsAccountSnapshotUpdateNoOp error can never block a re-record.
- On read, lots are sorted by line, then ordinal, and grouped onto each line.

(7) InvestmentAccountOrchestration:
- REQ-POS-11.4: the active-period update now covers activities as well as snapshots.
- REQ-POS-5.3: this check covers the withdrawn REQ-POS-5.6 lot case.
- HoldingOrchestration (REQ-POS-11.10): a delete is refused when any activity references the holding.

(8) NetWorth and NetWorthHistory against REQ-RPT-8.1 to 8.8 and 10.1 to 10.4.
- classifyDate: a date inside a fiscal period, or a pre-ledger date; when no fiscal period exists the call fails.
- Balances come from either the ledger (AccountBalance: voided entries excluded, as-of applied, normal-balance direction) or the latest pre-ledger balance on or before the date, never both.
- An account without a pre-ledger balance is left out of the listing, not shown as zero.
- Linked asset accounts are excluded. Mortgages of properties not owned stay in liabilities.
- Equity, investable wealth and the absent-component rules match 8.8.
- History checks every month-end before computing. convertListOfResultsToResultsList returns the first error in list order, so the earliest offending date is the one named.

(9) Non-slice-2 changes since the last audit:
- ClassificationOrchestration: added a rule-name tie-break when sorting.
- StageEntryOrchestration: rejects two groups that share a source and reference; filters by ingestion source id.
- JournalEntryOrchestration: now uses confirmNumRows.
- InstanceOrchestration: removed the 'payment under invoice' check. I traced every caller (the CashFlowOps delete and posting paths, and CashFlowFieldConverters). Each one gets the payment's invoice from the payment itself or passes empty lists, so the removed check could not be reached.

(10) Slice-2 migrations: nullability, unique keys and the RESTRICT foreign keys match the spec.

(11) Dan's statement: nothing derives lots or cost from activity, the guards exist on both fiscal-period creation and ledger linking, and the untested error cases in check-apperror-coverage are all loud rejections. I found nothing in my scope that contradicts the statement.

Nothing I examined broke a spec requirement, a learning-catalog rule, or the schema strongly enough to report.