# spec-quality-auditor:Money.md

_No findings._

## Reasoning

Scope: /home/claude/SonOfLeo/Specs/Behavioral/Money.md (29 active, 3 waived, 2 unenforceable, 0 withdrawn). The spec has not changed since commit 22af7f4, which applied the 2026-10-04a dispositions. Before reporting I read the audit-conduct catalog and all 12 of its articles, resolved-findings.md (CV-2, CV-4, AMB-13, MON-2, MON-3, DECIMAL-SCALE), the 2026-10-03a and 2026-10-04a Money quality and efficacy reports, and the dispositions for them (#160, #161, #162, #064, MON-DEF-1 deferred, EFF-MON-7 deferred).

1. Definitions: REQ-MON-2.1 now ties its rule to "ledger currency values" in the Definitions.md Money sense "anywhere in the system". Its "Why ledger currency only" note leaves out the Monte Carlo type, which matches the Definitions.md Money note. REQ-QP-3.5 now says the product "is Money in the Definitions sense but is not the system's Money type", so the earlier clash (QP-DEF-1) is gone. Leftover scoping for future Money types is MON-DEF-1, which is deferred until the Monte Carlo slice, so I did not re-raise it.

2. Internal contradictions: I looked at whether REQ-MON-2.4 (split N ways) conflicts with REQ-MON-2.7 (no division of Money). Splitting a transaction is the textbook unpack, divide and repack case in the money-arithmetic-boundaries learning, and splitByN does exactly that through fromDecimalList. A reasonable reader sees no conflict, so this did not reach finding level. The clause "validate section 1 = 1.2 to 1.4" matches how 2.2.1, 2.3.1, 2.5.1, 2.6.1 and 2.9.1 use the phrase. REQ-MON-2.4.2, 2.4.3 and 2.4.6 overlap but do not conflict (MON-3 ruling).

3. Other specs: REQ-MON-2.1 allows three conversions to decimal: REQ-MON-2.1.1 boundaries, the REQ-MON-2.7.1 route, and the REQ-QP-3.5 / REQ-POS-6.8 comparison. I checked whether Positions slice 2 brought in any other Money-to-decimal comparison or arithmetic.
   - REQ-POS-6.12 checks only lot quantity, which is a Quantity. Lot cost is deliberately not reconciled.
   - The REQ-POS-14.x roll-forward works on Quantities only.
   - REQ-POS-12.7 activity amount and REQ-POS-15.1 pre-ledger balance are plain Money values.
   - Nothing in Positions divides cost by units (AverageCost is recorded as reported).
   - Every `Money.amount` call in Src is a DB parameter, a contract converter or an error payload (all boundaries), or the REQ-POS-6.8 check in AccountSnapshotLine.fs. Comparisons and sign tests go through Money.isEqual, isZero and isNegative, for example InstanceOrchestration.fs:120, AccountDeactivation.fs:53 and PositionsComponent.fs:353.
   I found no SystemWide conflict.

4 and 5. Ambiguity and elaboration: every requirement is concrete. REQ-MON-2.4.4 and 2.4.5 fix the rounding mode and where the residual goes, and 1.2 to 1.4 give literal limits. Questions about trailing-zero scale and intermediate overflow are already ruled on (DECIMAL-SCALE, MON-2).

6. Withdrawn: the table is empty and nothing is withdrawn.

7. Waived and unenforceable:
   - REQ-MON-2.1 and 2.7 are waived as proofs of absence.
   - The 2.7.1 waiver (restated per #162) matches Money.fs: the type is a private record and there are no * or / operators.
   - REQ-MON-1.1 is unenforceable because there is no currency field.
   - REQ-MON-2.1.1 is a design-time permission for developers.
   Every other active ID (1.2, 1.3, 1.4, 2.2 to 2.6.1, 2.8 to 2.11) is cited by at least one test name. I grepped Tests/ for each one, so the three-state rule holds.

Considered and not reported:
- Money.floorAtZero, used at CashFlowOps.fs:781, is a Money operation that no REQ in section 2 covers. That is a missing requirement, which the panel owns, and the 2026-10-04a audit already noted it (MON-WAIVER-271-1).
- The display formatters are deferred under EFF-MON-7.
- Statement delta: Dan's slice 2 claims don't touch Money, and his claim that every active requirement is tested holds for this spec.
