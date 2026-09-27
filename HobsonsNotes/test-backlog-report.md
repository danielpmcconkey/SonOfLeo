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

The same thing happened on a smaller scale for §9. The §9 names were drafted and graded before any Src was read. Then,
while the §7 tests were being written, parts of `InstanceOrchestration.fs` were read, including the fulfilment
derivation and the names of its composite checks. The §9 placeholders were committed after that read, with the names
exactly as graded.

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

### §7 projection sweep

Files: `Tests/Tests.Integrated/CrossDomainOrchestration/SweepBehaviour.fs` (20 names, 25 cases) and
`Tests/Tests.Isolated/Model/CashFlow/CashFlowComponent.fs` (1 name, 3 cases). 24 of the integrated cases pass, and 1
fails against Src (finding F-4). All the isolated cases pass.

The sweep reads every active agreement, the fixture's included, so each test reads back only the agreements it made.
The dates a cadence should fall on are worked out in the test day by day, from what the cadence means. They are not
taken from the code's next-date function, which is what the sweep itself calls.

Both REQ-CF-7.15 names turned out to be reachable, but only through model-level edits that no route offers.
- The Instance case puts an agreement's next-instance date back behind an Instance it already has. The sweep's first
  Instance for that agreement is then not after its latest one.
- The Invoice case sets a leg's expected amount to zero. The Invoice the sweep builds for it then fails validation.

Both tests commit their setup and read back from a fresh context, then delete what they made. The sweep doesn't order
the agreements it reads, so neither test can promise that another agreement was swept before the failing one. Each makes
a control agreement just before the failing one and another just after, so that one of them is swept first if the read
follows creation order in either direction. That is likely, but Src doesn't guarantee it.

The mutation pass on the integrated file ran k = 1 through 8, the deepest any test goes. The only mutant that survived
was the REQ-CF-7.16 test's second assertion, and it survived because that assertion already fails against Src (F-4),
which already shows it failing. The harness only finds tests declared as class members, so it could not reach the
isolated test. That test's one assertion was flipped by hand, and all 3 cases failed.

REQ-CF-7.13 ("a deterministic [DET] operation") has no test, for the same reason as REQ-CF-10.6 above.

### §9 derived state

File: `Tests/Tests.Integrated/CrossDomainOrchestration/DerivedStateRules.fs`. There are 16 tests, and all pass.

The three REQ-CF-9.11 tests go through the CashFlow routes, which commit. Each one sets up in a committed transaction,
calls the route, reads back from a fresh context, and deletes the agreement and its journal entries in a finally. The
payloads are built from the real contracts, then the derived fields are added to the JSON by hand, because the create
contracts have no field to hold them.

The mutation pass ran k = 1 through 3, the deepest any test goes, and no mutant survived.

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

**F-4. REQ-CF-7.16: the sweep creates Instances after an agreement's end date. Bug in Src.**
- Test: `SweepBehaviour.fs`, "an agreement whose end date falls inside the horizon, on a day that is not a cadence
  date, gets an Instance on its last cadence date before the end date and none after it". It fails with
  `Assert.Empty() Failure` and lists four Weekly Instances dated after the end date.
- Spec: "The sweep never creates an Instance dated after its Master Agreement's end date, even when that date falls
  inside the horizon."
- Code: `CashFlowOps.spawnInstancesFromAgreement` walks from the next-instance date to today plus the horizon. It never
  looks at the end date. The end date is only used to decide whether the agreement is active today (REQ-CF-7.2).
- Fix: cap the walk at the earlier of the horizon end and the end date.
- The same gap reaches an agreement that ends today. It is swept, because it is still active, and then gets Instances
  for every cadence date through the horizon end. The REQ-CF-7.2 end-today test checks only the Instance dated today,
  so it passes.

**F-5. REQ-CF-9.11: the UpdateInvoice contract still carries payment state and posted state. Code or spec; the
behaviour holds.**
- Test: `DerivedStateRules.fs`, "an UpdateInvoice payload supplying FullyPaid and PostedToLedger for an Invoice with no
  Payments leaves it NotYetPaid and NotHandled when re-fetched". It passes.
- Spec: "No create or update contract carries payment state, posted state, or is-fulfilled."
- Code: `UpdateInvoiceInput` has `paymentStateUpdate` and `postedStateUpdate`. `InstanceOrchestration.updateInstanceComposite`
  rejects any update that sets them (`CashflowInstanceCompositeDerivedFieldSet`), so a caller still can't set them. The
  contract, though, carries them, which the spec says it must not.
- The create contracts behave differently. When CreateInstance or CreatePayment is sent derived fields, it ignores them
  without a word, because the JSON reader skips properties it doesn't know. The call succeeds and the fields are never
  used.
- Suggest removing the two fields from `UpdateInvoiceInput`, or rewording REQ-CF-9.11 to say the system rejects them.

**Observation, not tested in this batch:**
- `PaymentAgreementLink.update` (operator re-point) sets `payment_agreement_id` but not `modified_at`. That looks like
  it breaks REQ-SYS-3.3. It will get a test when the SystemWide requirements come up.

## Requirements covered

These are cited by passing tests unless noted.
- §12–§13: REQ-CF-12.1, 12.2, 12.3 (more cases), 12.4 (one case fails, F-1), 12.5, 12.6, 12.7, 12.8, 13.1 (more
  cases), 13.2 (more cases), 13.3, 13.4, 13.5, 13.6, 13.8, 13.9.
- §10: REQ-CF-10.1, 10.2, 10.3, 10.4, 10.5, 10.7.
- §9: REQ-CF-9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.10, 9.11.
- §7: REQ-CF-7.1, 7.2, 7.3, 7.4, 7.6, 7.7, 7.8, 7.9, 7.10, 7.12, 7.14, 7.15, 7.16 (fails, F-4), plus REQ-CF-4.8.

## Requirements still uncovered

## Suites run
