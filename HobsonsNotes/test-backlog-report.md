# Test backlog report

Started 2026-09-27 on `cash-flow`, working through the traceability audit's uncovered requirements alongside the plan
agent. The order is CashFlow §12–§13, §10, §7, §9, §2–§6, §8, §14, then the other domains and plan item 30.

Each batch follows the README's slice loop, steps 5–10. Names are drafted from the spec, graded with
Skills/TestNameReview, and committed as failing placeholders before Src is read. Then the bodies are written, and every
assertion is seen to fail (see "Mutation" below).

## Process note

Before Dan's full brief arrived, this thread read all of `Src/Business.CrossDomainOrchestration/CashFlowOps.fs` while
it was orienting on coverage. That file holds linkage, matching, the sweep, the projection and the payments-to-posted
transition. So the rule that names come before Src was broken for §7, §8 and §10, not only for §12–§13, whose names
were already committed when the file was read. Those names were still drafted from the spec text alone and graded by
an agent that never saw Src. The read is recorded here so the names can be weighed with that in mind.

## Mutation

The brief requires every new assertion to be seen to fail. Each batch is run through a mutation harness. For each k,
it inverts the k-th assertion of every test in the file at once (Equal↔NotEqual, Empty↔NotEmpty,
Contains↔DoesNotContain, True↔False, and `Single(xs)` becomes `Single(xs @ xs)`), then builds and runs the file. Any
mutated test that still passes is a surviving mutant, and it is fixed before the batch is committed.

## Batches

### §12–§13 linkage and matching

File: `Tests/Tests.Integrated/CrossDomainOrchestration/LinkageAndMatching.fs`. There are 43 names (63 cases once the
theories expand). 62 pass and 1 fails against Src (finding F-1). The mutation pass inverted the k-th assertion of every
test for k = 1 through 7, the deepest any test goes, and no mutant survived.

The tests reuse the pattern of `InvoiceMatching.fs`. A local `Scenario` builds agreements, rules, staged entries and
Invoices inside a transaction the test rolls back. Nothing in the shared fixture or `Tests.Helpers` was touched.

One committed name was withdrawn after Src was read, and it is stated here so the withdrawal is on record. The name was
"REQ-CF-12.4 when a line-type-constrained rule matched no line of the entry, the claim creates no link and is reported
unlinked with the no-line reason". It describes a state that can't be reached. A rule that matched no line of an entry
has not claimed the entry, so there is no claim to report. The zero-kept-lines clause of REQ-CF-12.4 can only arise
under the direction default, and a test covers that case. The grader for the next batch proposed a replacement name,
which read the clause as reachable, so the placeholder was dropped rather than renamed. Dan may want the spec to say
that the zero case applies only to the default.

### §10 payments-to-posted

File: `Tests/Tests.Integrated/CrossDomainOrchestration/PaymentsToPosted.fs`. There are 11 tests, and all pass. The
mutation pass ran k = 1 through 7 with no survivors.

REQ-CF-10.6 ("a deterministic [DET] operation") has no test. It is proposed for the Unenforceable table, along with
REQ-CF-7.13 and REQ-CF-8.5, because nothing observable separates a deterministic operation from one that is
deterministic by accident. Dan needs to approve that.

The grader asked for one more name: what happens when an Invoice's re-derived state fails §9 validation during the
transition. No name was drafted for it. Posted state is derived, and a journal entry line posted from a staged line
sits on the same account, so the transition has no reachable way to produce an invalid composite.

## Findings

Each finding gives the requirement, the test, what the spec says, what the code does, and where the bug probably lies.

**F-1. REQ-CF-12.4: a constrained rule does not narrow the lines when an unconstrained rule also claims. Bug in Src.**
- Test: `LinkageAndMatching.fs`, "when one claiming rule constrains line type and another does not, the claim links
  the line the constraining rule matched". It fails with `Assert.Single() Failure: The collection was empty`, which
  means no link is made.
- Spec: "when any claiming rule constrains line type, the lines that rule matched are kept".
- Code: `CashFlowOps.selectLegsOfClaimedEntries` (`if ruleChoseTheLeg then Ok claims`) keeps every claim on the entry
  when any rule constrains line type. That includes the lines only the unconstrained rule matched. Two lines survive,
  the claim is reported `ManyLinesOnAgreementAccount`, and nothing is linked.
- Fix: keep only the lines a line-type-constraining rule matched.

**F-2. REQ-CF-12.8 disagrees with REQ-CR-8.5 about priorities. Spec question.**
- REQ-CF-12.8 says the run's matches, "including the rules that matched each line and their priorities, are recorded
  under a run ID".
- REQ-CR-8.5 says a run's rows carry each rule's priority "as they stand at retrieval time".
- The code follows REQ-CR-8.5: `fetchRunMatchesWithRules` pairs each recorded match with the rule as it is now. So
  re-prioritising a rule changes how an old run reads back.
- The REQ-CF-12.8 test does not re-prioritise between the run and the fetch, so it passes under either reading. Dan
  should say which requirement wins.

**F-3. REQ-CF-12.2 against REQ-CF-12.5 for rules of different priority. Spec gap; the code is reasonable.**
- REQ-CF-12.5 links "a Payment Agreement claimed by exactly one line, where that line's claim is not a tie".
- Suppose one line is claimed by rules of different priority for two agreements. Read literally, both agreements are
  claimed by exactly one line and neither claim is tied, so both would be linked. That breaks REQ-CF-12.2.
- The code counts only the clear winner as claiming (`claimingMatches`), which keeps REQ-CF-12.2. The test asserts that
  behaviour and passes.
- The spec should say that, when priorities differ, only the winning rule's agreement claims.

**Observation, not tested in this batch:**
- `PaymentAgreementLink.update` (operator re-point) sets `payment_agreement_id` but not `modified_at`. That looks like
  it breaks REQ-SYS-3.3. It will get a test when the SystemWide requirements come up.

## Requirements covered

These are cited by passing tests unless noted.
- §12–§13: REQ-CF-12.1, 12.2, 12.3 (more cases), 12.4 (one case fails, F-1), 12.5, 12.6, 12.7, 12.8, 13.1 (more
  cases), 13.2 (more cases), 13.3, 13.4, 13.5, 13.6, 13.8, 13.9.
- §10: REQ-CF-10.1, 10.2, 10.3, 10.4, 10.5, 10.7.

## Requirements still uncovered

## Suites run
