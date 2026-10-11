# test-efficacy-positions

## EFF-POS-13.3-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Positions/LotsAndActivity.fs:268, :280 (REQ-POS-13.3); Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:151-155
- **Summary:** REQ-POS-13.3 (overlapping ranges for one account in one operation are rejected) is tested only on the helper InvestmentActivity.confirmRangesDontOverlap, called with made-up string keys. No test sends overlapping ranges through recordActivity or the InvestmentActivity Record route.
- **Resolution:** fix-test

Both tests that cite REQ-POS-13.3 call `InvestmentActivity.confirmRangesDontOverlap [ "key-a", account, ...; "key-a", account, ... ]` directly, and the key is the literal "key-a". In production, recordActivity (InvestmentActivityOrchestration.fs:151-155) builds the key from the InvestmentAccountId and passes the account name as the label. Nothing in Tests/ sends two overlapping ranges for one account through recordActivity, the converter, or the route; a grep for PositionsActivityRangesOverlap finds only LotsAndActivity.fs:273. Smell test: delete the `do! ... confirmRangesDontOverlap` step from recordActivity, or key it by something that differs per range (such as the range tuple), and every test stays green. This failure is not loud. Without the guard, recordRange runs in sequence: the second range deletes activities the first range has just written in the overlapping days, then writes its own. The operation succeeds and quietly loses or doubles activity. So Dan's carve-out for error cases that 'only reject loudly' does not apply. It also bears on his statement that 'every active requirement is tested': the requirement is cited, but the behavior the operator would hit is not exercised. By contrast, the parallel repeat rules are tested where the operator meets them: REQ-POS-7.1 at AccountSnapshotRecording.fs:140 and REQ-POS-15.4 at PreLedgerBalances.fs:155.

**Action:** Add an orchestrator test in InvestmentActivityRecording.fs that records two overlapping ranges for one fixture account (for example d1+1..d1+5 and d1+5..d1+6 for Sam 401k). Assert PositionsActivityRangesOverlap with (account name, first range, second range), then assert the account's stored activity over d1+1..d1+6 equals the fixture's activities on those dates.

**Why:** A test of a helper with synthetic inputs proves the helper works. It does not prove the operation calls it, or calls it with the right key. The failure vector is an operator supplying two overlapping ranges for one account, and the lowest layer at which that caller exists is the orchestrator or route. A regression there corrupts activity silently instead of failing.

---

## EFF-POS-13.1-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Positions/LotsAndActivity.fs:241 (REQ-POS-13.1); Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:60
- **Summary:** The REQ-POS-13.1 clause that rejects an Activity dated outside its supplied range is tested only on the helper confirmDatedInRange. No recording test supplies an activity dated outside its range.
- **Resolution:** fix-test

REQ-POS-13.1: 'An Activity dated outside its range is rejected with a typed error naming the account, the range and the date.' The only test of this clause is LotsAndActivity.fs:241, which calls `InvestmentActivity.confirmDatedInRange account beginDate endDate outside` directly. The other integrated 13.1 tests do not reach this clause: InvestmentActivityRecording.fs:172 covers end-after-current-date, and PositionsSliceTwoRoutes.fs:180 and :227 cover multi-account recording and atomicity. Every recording test supplies only activities dated inside their range. PositionsActivityOutsideRange appears in Tests/ only at LotsAndActivity.fs:246. Smell test: remove `do! InvestmentActivity.confirmDatedInRange ...` from buildActivity (InvestmentActivityOrchestration.fs:60) and no test fails. The regression is silent, not loud. An activity dated outside its range but inside the account's active period would be stored. The replacement delete only clears the supplied range, so a later re-supply of the activity's true period would sit beside it and count it twice. That is the double counting REQ-POS-13.2's rationale exists to prevent.

**Action:** Add an orchestrator test in InvestmentActivityRecording.fs that records a range for a fixture account with one activity dated the day after the range's end, still inside the active period. Assert PositionsActivityOutsideRange with (account, begin, end, date), and assert the account's activities over the wider window are unchanged from the fixture.

**Why:** Testing a pure guard in isolation is right for its boundary arithmetic. But the requirement is a property of the recording operation, and if the call site regresses nothing goes red. When the failure mode is silent data corruption, the cited requirement is not behaviorally covered.

---

## EFF-POS-14.2-1 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/UnitRollForward.fs:130, :144, :170, :216 (REQ-POS-14.2)
- **Summary:** REQ-POS-14.2 gives a row only to a Security named by a units-moving Activity 'dated after the first date and on or before the second'. No test checks that a Security on neither snapshot, named only by a units-moving activity outside that window, gets no row.
- **Resolution:** fix-test

REQ-POS-14.2 sets two qualifiers on row membership for a Security not on either snapshot: the activity must be of a units-moving kind, and it must be dated in the window. The kind qualifier is tested: UnitRollForward.fs:216 shows a Security named only by Dividend, Interest or CapitalGainDistribution has no row. The window qualifier is tested only for totals, and only on Total Market, which is on both snapshots anyway. UnitRollForward.fs:130 and :144 assert unitsIn/unitsOut of PF.totalMarket around d1 and d3. :170 adds a Security on neither snapshot, but its Purchase and Sale are inside the window. Smell test: an implementation that built the row set from all of the account's units-moving activities, but applied the window only when summing, would add a spurious row (start 0, in 0, out 0, expected 0, end 0, difference 0) for a Security traded only before the first date or after the second. Every current test would pass, because none sets up that case.

**Action:** Add a UnitRollForward test: give Sam 401k a Holding of International, record a units-moving activity for it dated d1 (or d3+1) only, and assert the d1-to-d3 rows equal sam401kRows exactly.

**Why:** When a requirement has two qualifiers on one rule, each needs its own case that fails when only that qualifier is broken. Here a regression would add zero-filled rows to a roll-forward that is meant to make no judgment and show only what moved, which hides real signal among noise rows.

---

## EFF-POS-EMPTY-1 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:139; Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs:101 (REQ-POS-13.1, REQ-POS-15.4 with REQ-SYS-6.1)
- **Summary:** Recording investment activity with no ranges, and recording pre-ledger balances with no balances, are each rejected by a typed error. No test covers either rejection, although the equivalent snapshot rejection is tested.
- **Resolution:** fix-test

recordActivity returns `error PositionsActivityRangeListIsEmpty` for an empty ranges list, and recordPreLedgerBalances returns `error PositionsPreLedgerBalanceListIsEmpty` for an empty list. Both enforce REQ-SYS-6.1 (no silent no-op) for the 'record one or more' operations of REQ-POS-13.1 and REQ-POS-15.4. A grep of Tests/ for either case name finds nothing. The snapshot equivalent is tested: PositionsRoutes.fs:1151, ``REQ-POS-7.1 REQ-SYS-6.1 an AccountSnapshot Record payload with an empty list of snapshots is rejected with a typed no-snapshots error``. Dan's carve-out for 'error cases that only reject loudly' does not fit these two. If either guard were deleted, the empty request would succeed silently, returning an empty list and writing nothing, which is exactly the silent no-op REQ-SYS-6.1 forbids. Note that an empty range inside a non-empty request is legitimate (REQ-POS-13.2: 'which may be none'). Only the empty list of ranges is a no-op, so the test must tell the two apart.

**Action:** Add two route tests in PositionsSliceTwoRoutes.fs mirroring PositionsRoutes.fs:1151: an InvestmentActivity Record payload with `ranges = []` asserting PositionsActivityRangeListIsEmpty, and a PreLedgerBalance Record payload with `balances = []` asserting PositionsPreLedgerBalanceListIsEmpty. Each takes both escape arms and cites REQ-SYS-6.1 with the Positions REQ.

**Why:** A guard whose regression leads to silent success needs a test, because nothing else in the system will notice it is gone. Slice 1 set the pattern for this guard and slice 2 added two more instances without following it.

---

## EFF-POS-COUNT-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/PositionsSliceTwoRoutes.fs:197 (REQ-POS-13.1, REQ-POS-13.4); Tests/Tests.Integrated/CrossDomainOrchestration/InvestmentActivityRecording.fs:178 (REQ-POS-13.1)
- **Summary:** Two activity-recording tests check what was stored by count alone and never read the stored activities' values (Specimen 3).
- **Resolution:** fix-test
- **Prior ruling:** Not suppressed: 2026-10-04a #075 accepted a count-only Holdings assertion as a finding. This applies the same rule to new slice-2 tests.

PositionsSliceTwoRoutes.fs:197 asserts `Assert.Equal((1, 3), (firstStored.Length, secondStored.Length))` after recording ranges for two route-made accounts, and its name claims the payload 'records both'. The amounts sent are 3.00 for the first account and 1.00, 2.00, 3.00 for the second; they are never compared. A route that stored the second account's activities under the first account's amounts, or that dropped or reordered fields, passes as long as the counts hold. InvestmentActivityRecording.fs:178 records `interest currentDate 1.00M` and asserts only `Assert.Equal(1, stored.Length)`. The listing is filtered to currentDate..currentDate, so the date is fixed by the locator (Specimen 8) and the amount, kind and description go unchecked. Precedent: the 2026-10-04a disposition #075 (EFF-POS-4) accepted the same pattern, a count-only check on stored Holdings, as a finding and required value assertions.

**Action:** At PositionsSliceTwoRoutes.fs:197, compare `firstStored |> List.map activityOf` and `secondStored |> List.map activityOf` with the exact tuples sent. At InvestmentActivityRecording.fs:178, assert `stored` equals `[ currentDate, "Interest", "Interest on cash", None, None, None, None, 1.00M ]`.

**Why:** A count says something came back, not that the right thing did. Tests/README: 'Counts only in addition to values, never instead of them.' These are the only checks of what the multi-account route call and the boundary-date call actually stored.

---

