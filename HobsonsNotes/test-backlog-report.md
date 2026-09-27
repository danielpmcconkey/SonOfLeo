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

For §2, some Src had already been read before the names were drafted. Writing the §9 route tests meant reading
`CashFlowContracts.fs`, so it was already known that the agreement contracts model cadence as a union. The §2 payload
names were still drafted from the spec, graded by an agent that saw only the spec, and committed before any §2 Src was
read.

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
REQ-CF-7.13, because nothing observable separates a deterministic operation from one that is
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

### §2 Master Agreement data states

Files: `Tests/Tests.Isolated/Model/CashFlow/MasterAgreementDataStates.fs` (13 names, 22 cases) and
`Tests/Tests.Integrated/CrossDomainOrchestration/MasterAgreementDataStates.fs` (26 names, 28 cases). All pass.

The payload tests and the tests that reject an update go through the CashFlow routes. A route commits when it
succeeds. `AgreementOrchestration.updateAgreement` also writes before it checks the agreement, and leaves the rollback to
its caller. So the only way to see what a refused update leaves behind is to run it through the route and read back
from a fresh context. Every agreement those tests name is deleted in a finally.

Each refusal was checked for its reason, not only that it failed:
- A payload with a null name, a null counterparty, an unknown cadence, or a missing week day, month or month day is
  refused by the JSON reader. It never reaches the model.
- A duplicate name, on create or on rename, is refused by the database's unique constraint
  (`master_agreement_agreement_name_key`). The caller gets a database error, not a cash-flow one.
- The date and cadence refusals come from the model's own typed errors.

The mutation harness now also finds tests declared as module-level `let`s and as `member this.` members, which it missed
before. The earlier batches had no `member this.` tests, so their results still stand. The isolated file ran k = 1 and 2,
and the integrated file k = 1 through 3. No mutant survived.

### §3 Payment Agreement data states

File: `Tests/Tests.Integrated/CrossDomainOrchestration/PaymentAgreementDataStates.fs` (25 names, 34 cases). All pass.

Most tests send a CreateAgreement payload through the route and read back from a fresh context. The route commits, so
every agreement they name is deleted in a finally. No route creates a Payment Agreement apart from its agreement, so
two tests go below it: the REQ-CF-3.3 orphan test writes a Payment Agreement with `PaymentAgreement.persist` under an
unknown Master Agreement ID, and the REQ-CF-3.11 theory calls `constructNewAndPersist` with an unknown account on
either side.

Each refusal was checked for its reason:
- A non-positive or over-precise expected amount, a blank or over-long memo, a blank or over-long name and an
  out-of-range days-due are refused by the model's own typed errors.
- A null name is refused by the JSON reader.
- A duplicate Payment Agreement name, whether in another agreement, padded, or twice in the same payload, is refused by
  the database's unique constraint (`payment_agreement_payment_agreement_name_key`). So is the orphan write, by the
  foreign key `payment_agreement_master_agreement_id_fkey`. The caller gets a database error, not a cash-flow one.
- The unknown account refusals name the side, as REQ-CF-3.11 requires.

Mutation, k = 1 to 3: no mutant survived.

### §4 Instance data states

File: `Tests/Tests.Integrated/CrossDomainOrchestration/InstanceDataStates.fs` (17 names, 23 cases). All pass.

Tests call `InstanceOrchestration` in a transaction that rolls back. The REQ-CF-4.5 test also sends a CreateInstance
payload through the route, which commits, so it deletes its agreement in a finally. Dates are in March 2027, which no
sweep reaches.

Each refusal was checked for its reason:
- A date off the cadence gives the cadence's own error, naming the date and the rule. A date on or before the latest
  Instance gives `CashflowInstanceDateNotAfterLatestInstance`, naming the latest date.
- Two Invoices for one leg give `CashflowInstanceManyInvoicesForPaymentAgreement`. An Invoice for another agreement's
  leg gives `CashflowInvoiceDiamondMismatch`.
- Every attempt to set is-fulfilled, true or false, is refused as a derived field
  (`CashflowInstanceCompositeDerivedFieldSet`). So REQ-CF-4.9 holds because the flag can't be set at all, not because
  the orchestration checks the value.
- An Instance for an unknown Master Agreement ID is refused with the DAL's generic "zero rows" error.
  `createInstanceCompositeAndSaveToDb` doesn't wrap the lookup with `whenNoRows`, so the caller never sees
  `CashflowMasterAgreementIdDoesntExist`. That still satisfies REQ-CF-4.3, but the message is poor.

The batch's names define "nothing is stored" as no Instance and an unchanged next-instance date, so the refusal tests
check both. Mutation, k = 1 to 4: no mutant survived. The next-instance assertions were added afterwards and were each
seen to fail when perturbed (10 cases).

### §5 Invoice data states

File: `Tests/Tests.Integrated/CrossDomainOrchestration/InvoiceDataStates.fs` (35 names, 66 cases). All pass.

Tests go through CreateInvoice, UpdateInvoice and CreateInstance, which commit, so the setup commits too and every
agreement is deleted in a finally. Two tests go below the routes, to `updateInstanceComposite` with an unknown Instance
ID or Payment Agreement ID, in a transaction that rolls back.

Two names were re-aimed after Src was read, and a third was added. They went back through the grader.
- Old: "no blocker, but a note" and "clear the blocker but keep the note".
- Why: the contracts and the model both hold the note inside the `NeedsDecision` and `Other` blocker cases, so no
  payload can express either state.
- New: NoFunds or Irresponsible with a note attached is refused, on create and on update. Clearing a noted blocker
  reads back with neither blocker nor note. That read-back proves the note column was cleared, because the store refuses
  to read a note without a blocker (`blockerFromColumns`).
- The "no blocker, so no note" clause of REQ-CF-5.14 therefore holds by construction. The grader accepted that.

The waiver for REQ-CF-5.2 (Invoice ID unique) was rejected by the grader in favour of a test that two Invoices get
distinct, non-empty IDs.

Each refusal was checked for its reason:
- Nulls, unknown blocker cases, and NoFunds or Irresponsible carrying a note are refused by the JSON reader.
- Everything else is refused by a typed model or orchestration error. That covers amounts, invoice states, note and memo
  lengths and blanks, unknown Instance and Payment Agreement, a leg of another agreement (the diamond check) and a
  second Invoice for one leg.

Mutation, k = 1 to 3: no mutant survived.

### §6 Payment data states

File: `Tests/Tests.Integrated/CrossDomainOrchestration/PaymentDataStates.fs` (24 names, 38 cases). All pass.

Tests go through CreatePayment, CreateInvoice, CreateInstance and TransitionPaymentsToPosted, which commit. The setup
commits too: agreements, journal entries dated today, and staged entries. A finally deletes the agreements, then the
staged entries, then the journal entries. The stored staged-line column is read with
`Payment.fetchStageEntryLineIdById`. The "no Payment points at this line" check uses a one-line raw query on
`cashflow.payment`.

One grader name was not adopted: "a CreatePayment payload carrying both a staged line and a journal entry line …". See
Q-1.

Each refusal was checked for its reason:
- A line on the wrong account gives `CashflowPaymentLineNotOnAgreementAccount`, whether it is the agreement's other
  account or one it doesn't use.
- A line that doesn't exist gives the ledger's or ingestion's "could not locate".
- A posted-to-ledger date that mismatches gives `CashflowPaymentPostedToLedgerDateMismatch`. One given for a staged
  line gives `CashflowPaymentPostedToLedgerDateWithoutJournalEntry`.
- Memo refusals come from `PaymentMemo`. A null pointer is refused by the JSON reader.
- An unknown Invoice ID gets the DAL's generic "zero rows" error, as an unknown Master Agreement ID does in §4.

Mutation, k = 1 to 4: one name was reported as surviving at k = 2 and 3. That is REQ-CF-6.4, "stored with exactly
that pointer and no pointer of the other kind". Its two cases take different branches of a `match`, each with its own
assertion. So a mutant touches one case only, and the harness counts a name as surviving when any of its cases passes.
Each branch's assertion was perturbed by hand and failed its own case. No other mutant survived.

### §8 cash-flow projection

File: `Tests/Tests.Integrated/CrossDomainOrchestration/ProjectionRules.fs` (18 names, 21 cases). All pass.

REQ-CF-8.3, 8.9 and 8.10 were already cited by `CashFlowProjection.fs`, so this batch covers the rest of §8. Each test
builds its own Cash-subtype account and Daily agreements, so the fixture's cash accounts and other tests' Invoices
cannot change the numbers it reads. Most tests call the projection inside a rolled-back transaction. The REQ-CF-8.1
horizon tests and the REQ-CF-8.8 read-only test go through the ProjectCashFlow route. The read-only test takes an md5
digest of the account, journal entry, journal entry line, Master Agreement, Payment Agreement, Instance, Invoice and
Payment tables before and after the call.

REQ-CF-8.5 ("deterministic [DET]") got a test after all: two projections over the same data must give identical
results, order included. It does not prove determinism, but it catches the observable failure. The Unenforceable
proposal for 8.5 is withdrawn.

Refusals: a horizon of 0, -1 or 366 gives `CashflowProjectionHorizonInDaysBelowMin` or `…ExceededMax`, carrying the
value sent and the bound.

Grading adjustments, made from the spec:
- The grader's "an unpaid Income Invoice on a fulfilled Instance adds nothing" was dropped. REQ-CF-4.9 makes an
  Instance fulfilled only when every Invoice is FullyPaid, so the case can't be built; the FullyPaid test covers it.
- The grader's "a fulfilled Instance with no Invoices yields no bill" was re-aimed. REQ-CF-4.9 forbids a fulfilled
  Instance with no Invoices. The reachable case is a fulfilled Instance whose one Invoice is FullyPaid and whose other
  leg has no Invoice.

Mutation, k = 1 to 4: no mutant survived.

### §14 maintenance operations

File: `Tests/Tests.Integrated/CrossDomainOrchestration/MaintenanceOperations.fs` (19 names, 22 cases). All pass.

REQ-CF-14.2 was already covered. Every test goes through the routes, which commit. The setup commits too, and a
finally deletes the agreements (links included), then the staged entries, then the journal entries. An agreement made
through CreateAgreement is found for clean-up by name.

Four names were re-aimed after the placeholders were committed, and the re-aimed names went back through
TestNameReview and were committed as placeholders again (f0cc596) before any body was written. The first pass
assumed that Payments summing to more than an Invoice's amount make the Instance invalid. The spec says otherwise:
REQ-CF-9.8 derives PartiallyPaid for any sum not equal to the amount, REQ-CF-13.1 speaks of Invoices whose Payments
"already exceed the Invoice amount", and REQ-CF-13.6 reports an overpayment without refusing it. So:
- The CreateInstance, CreateInvoice and UpdateInvoice rejections now use REQ-CF-9.3 (FullyPaid while blocked) and
  REQ-CF-9.4 (a blocker set on a FullyPaid Invoice).
- The CreatePayment overpayment case now asserts what the spec says happens: the Payment is stored, the Invoice goes
  from FullyPaid to PartiallyPaid, and its Instance is no longer fulfilled (REQ-CF-9.10). It passes.
- The grader also proposed a CreatePayment rejection under REQ-CF-9.3. The adopted list already had one.

Other grading adjustments from the first pass:
- The grader's generic "valid on its own but violates an Instance-level rule" name was made concrete with REQ-CF-9.3.
- CreatePayment was dropped from the Payment Agreement name routes of REQ-CF-14.7. Its payload names an Invoice ID,
  not a Payment Agreement.

For REQ-CF-14.6, "the line is a linkage candidate again" is shown by running linkage and matching (rolled back) with a
rule claiming the line: after the delete it links again. With a second Payment on the line, the original link is kept
and a run with a rule for another Payment Agreement leaves it the line's only link. The agreements are daily, dated
today, so the line falls inside the Invoice's dates and the run does not fail on it as an orphan (REQ-CF-13.7).

Each refusal was checked for its reason. The four REQ-CF-9.3 and 9.4 rejections all give "cannot be FullyPaid while a
Blocker is set". The unknown account code gives `AccountCodeDoesntMatchAccountId` with the code; the unknown Payment ID
gives `CashflowPaymentIdDoesntExist` with the ID; unknown names give `CashflowAgreementNameDoesntMatchId` and
`CashflowPaymentAgreementNameDoesntMatchId` with the name.

Mutation, k = 1 to 5: no mutant survived. Two mutants at k = 1 were not run by the harness.

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

**F-6. REQ-CF-5.14: the blocker note's error messages name the wrong field. Bug in Src (message text only).**
- Test: `InvoiceDataStates.fs`, "for each of NeedsDecision and Other, a CreateInvoice payload giving that blocker with
  no note, an empty note, or a whitespace-only note is rejected". It passes: the refusal happens, and for the right reason.
- The message is wrong. An empty or whitespace note is refused with "AgreementMemo cannot be empty. Provided Memo is …",
  and a 501-character note with "BlockerNote cannot exceed 500 characters. Provided Memo is …".
- The operator reading that message would look for a memo, not a blocker note. The text looks copied from the memo
  errors in `CashFlowError`.

**Q-1. REQ-CF-6.4 "both may be present" at creation. Spec question.**
- REQ-CF-6.4 says a Payment may carry both a staged line and a journal entry line, with the journal entry line taking
  precedence.
- The create contracts' `TransactionPointerContract` is `Posted | Staged`, one or the other. So no payload can create a
  Payment with both, and both appear only after payments transition to posted (§10).
- The §6 grader proposed "a CreatePayment payload carrying both … is stored with both". It was not adopted, because no
  payload can express it. Dan should say whether creation with both is meant to be possible, or whether the spec should
  say that both arise only from the transition.

**Observation, inferred from reading Src and not tested: a Payment's payload amount drives the payment state it is
created with.**
- REQ-CF-6.5 says the amount is derived from the line. The §6 test confirms the amount reads back as the line's.
- But when a Payment is created, `preConstructInvoiceComposite` and `preConstructNewInvoiceComposite` derive the
  Invoice's payment state from the amount in the payload, not the line's.
- Two consequences follow:
  - A payload that claims more than the line holds, enough to reach FullyPaid, is refused. The re-check after the write
    sees the real amount.
  - A payload that claims less than a line which fully pays the Invoice leaves the Invoice PartiallyPaid, although its
    Payments sum to its amount. Nothing re-derives the state.
- The §6 amount test avoids both cases on purpose, by keeping both amounts PartiallyPaid. If Dan wants, a REQ-CF-9.x
  test can pin it down.

**Observation, not tested in this batch:**
- `PaymentAgreementLink.update` (operator re-point) sets `payment_agreement_id` but not `modified_at`. That looks like
  it breaks REQ-SYS-3.3. It will get a test when the SystemWide requirements come up.

## Requirements covered

These are cited by passing tests unless noted.
- §12–§13: REQ-CF-12.1, 12.2, 12.3 (more cases), 12.4 (one case fails, F-1), 12.5, 12.6, 12.7, 12.8, 13.1 (more
  cases), 13.2 (more cases), 13.3, 13.4, 13.5, 13.6, 13.8, 13.9.
- §10: REQ-CF-10.1, 10.2, 10.3, 10.4, 10.5, 10.7.
- §2: REQ-CF-2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 2.10, 2.11, 2.12, 2.13, 2.14, 2.15, 2.16, 2.17, 2.18, 2.19, 2.23, 2.25,
  2.26, 2.27.
- §9: REQ-CF-9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.10, 9.11.
- §3: REQ-CF-3.3, 3.4, 3.5, 3.7, 3.8, 3.9, 3.10, 3.11.
- §4: REQ-CF-4.3, 4.5, 4.6, 4.7, 4.9, 4.10.
- §5: REQ-CF-5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.13, 5.14, 5.15, 5.16.
- §6: REQ-CF-6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.9, 6.10, 6.11.
- §8: REQ-CF-8.1, 8.2, 8.4, 8.5, 8.6, 8.7, 8.8.
- §14: REQ-CF-14.1, 14.3, 14.4, 14.5, 14.6, 14.7 (with more cases for REQ-CF-9.3, 9.4, 9.8, 9.10).
- §7: REQ-CF-7.1, 7.2, 7.3, 7.4, 7.6, 7.7, 7.8, 7.9, 7.10, 7.12, 7.14, 7.15, 7.16 (fails, F-4), plus REQ-CF-4.8.

## Requirements still uncovered

**Proposed waivers, pending Dan's approval.** These follow the AccountCrud precedent.
- REQ-CF-2.1: the Master Agreement ID is a Guid, a value type, so it can't be null. This follows REQ-AC-1.21.
- REQ-CF-2.2: the Master Agreement ID is generated by the system. The grader agreed on one condition: no create or
  update path may let a caller supply the ID. The condition holds: `CreateAgreementInput` has no ID, and
  `constructNewAndPersist` generates one.
- REQ-CF-3.1 and REQ-CF-3.2, the Payment Agreement ID: the same reasons as REQ-CF-2.1 and REQ-CF-2.2.
- REQ-CF-4.1 and REQ-CF-4.4: the Instance ID is a Guid and the Instance date is a NodaTime `LocalDate`. Both are value
  types, so neither can be null.
- REQ-CF-4.2: the Instance ID is generated by the system, so it is unique by construction (REQ-AC-1.22 precedent).
  (The grader rejected the same argument for REQ-CF-5.2, which has a test instead; 4.2 could get one the same way.)
- REQ-CF-5.1: the Invoice ID is a Guid value type.
- REQ-CF-6.1: the Payment ID is a Guid value type (the grader accepted it; Guid.Empty is covered by the REQ-CF-6.2 test).
- REQ-CF-5.11 and REQ-CF-5.12: payment state and posted state are closed unions that are only ever derived (REQ-CF-9.8
  to 9.11), never read from a caller.
- REQ-CF-2.3 and REQ-CF-2.17 (a null name or counterparty) have no waiver. Instead, they each have a payload test,
  because a null can arrive at run time through a deserialized payload.

## Suites run
