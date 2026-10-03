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

### Accounts (create, activity, balances)

File: `Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreateActivityBalance.fs` (28 names, 53 cases). All pass.

Covers REQ-AC-2.22, 2.23, 3.11, 3.12, 3.12.1 to 3.12.4, 3.13, 3.13.1 to 3.13.3. Every test commits its own accounts
(codes are "Z" plus seven hex characters, so they cannot collide with fixture data) and journal entries through a
small local ledger, and a finally deletes them. The activity tests build one tree of parent, child, grandchild,
no-lines and liability accounts with five entries, one of them voided.

The single-filter theory for REQ-AC-3.12.1 does not restate the filter logic from Src. It compares the filtered route
result with the unfiltered result under a predicate written from the spec, and it asserts that the unfiltered rows
include some that fail the predicate, so the filter has something to exclude. Separate cases pin the date range's
inclusive ends, the case-sensitive description substring, and source equality.

For REQ-AC-3.11, an unknown parent account code in a filter is refused with `AccountParentCodeInvalid` naming the
code; the test accepts that or `AccountCodeDoesntMatchAccountId`, since both are typed refusals naming the code. The
REQ-AC-3.13.3 refusal gives "fetchByAccountIdList requires at least one account ID".

Mutation, k = 1 to 4: no mutant survived. The harness could not run the mutants in the two filter theories
(REQ-AC-3.12.1 and 3.12.3). Those were perturbed by hand instead (the set equality inverted, and the no-lines account
asserted present), and every case of both theories failed.

### DataIngestion (staging)

File: `Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs` (28 names, 44 cases). 38 pass and 6
fail against Src (findings F-7 to F-10).

Covers REQ-STG-1.17, 2.25, 2.26, 2.27, 3.11, 3.13, 3.14, 3.15, 4.1.1, 5.11, 6.3.1, 6.3.2, 6.7, 7.5.1, 8.5, 9.10, 9.11.
The file, source and post-result tests go through the Ingestion routes, which commit. Files are written to a scratch
import directory under the system temp directory, and a finally deletes every staged entry from the file, the file
itself, and any journal entries, rules, sources and accounts the test made. The rest run in a rolled-back transaction
and advance the context's instant between operations, because one staged entry cannot hold two status transitions at
one instant (REQ-STG-4.1.2).

Some tests check that their setup does bite, so that a pass isn't vacuous. The REQ-STG-3.14 test runs classification
and dedup afterwards (rolled back) and sees the rule assign its account and the repeat flagged Duplicate. The
REQ-STG-6.7 dedup test has an unpaid repeat beside the paid one, and the unpaid one is flagged. The REQ-STG-8.5 test
creates two fresh accounts, so a trial balance row moves only because of the test's own entries, and runs the shadow
post twice: once before and once after committing a ledger entry dated tomorrow.

Refusal reasons: each column-name spelling is rejected with `IngestionFileRejected`, naming the missing property for
both records. The directory cases give `FileIoDirectoryDoesntExist` or `FileIoFileDoesntExist`. An empty or
whitespace-only source name gives `JournalRefFinancialInstitutionIsEmpty` and a 101-character one
`JournalRefFinancialInstitutionTooLong`. A null name is refused earlier, as `JsonDeserializationFailed`. An update
naming no fields gives `IngestionUpdateStageEntryNoOp`. The REQ-STG-6.3.1 refusal is pinned to
`IngestionUpdateStageEntryLinesMustMatchHeader` with both IDs.

REQ-STG-9.10's clause "including when the staged lines are not in the order the journal entry lines are created in"
is only partly reachable. Posting builds the journal entry lines from the staged lines as read, so a test can't force
the two orders apart. The test stages the lines Credit, Debit, Debit and checks the pairing by account, line type and
amount, whatever the order.

Mutation, k = 1 to 6: no mutant survived. The harness could not run the mutants in the padding theory (REQ-STG-1.17)
or the REQ-STG-6.3.2 theory. Both were perturbed by hand (the line count and the refusal inverted) and all nine cases
failed.

### Classification Rules (claimants and runs)

File: `Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationClaimantsAndRuns.fs` (20 names, 25 cases). All pass.

Covers REQ-CR-1.23, 1.24, 1.25, 3.7, 3.8, 5.6, 8.1, 8.2, 8.3, 8.5. REQ-CR-5.4 was dropped from the batch because it
already had a citing test. Staged entries come from TestCreditCardCo, whose only fixture rule needs an "REI REI Co-op"
description, so no fixture rule matches unless a test means it to. Most tests run inside a rolled-back transaction.
The REQ-CR-8.3 duplicate-row test and the REQ-CR-8.5 tests read through the routes, so their setup commits, and a
finally deletes the run's match rows, then the rules, then the staged entries.

A run advances the context's instant before it starts. A staged entry created and then classified at the same instant
is refused by the audit table's key on entry and instant (REQ-STG-4.1.2), which is the same thing the fixture does
between staging and posting.

Refusals are pinned to their typed errors: a rule row whose claimant columns are both null, or both set, reads back
as `IngestionClassificationRuleInvalidClaimant` naming the rule's ID, by ID, by name and through the filter. For
REQ-CR-5.6, an unknown account code gives `AccountCodeDoesntMatchAccountId`, an unknown payment agreement name gives
`CashflowPaymentAgreementNameDoesntMatchId`, and an unknown claimant type gives
`IngestionInvalidClassificationClaimantType`, each naming the value. (The first run of the account-code case used an
11-character code and got `AccountCodeTooLong`. That was the test's mistake, so the case now uses a well-formed code.)

Mutation, k = 1 to 6: no mutant survived. The harness could not run the mutants in the REQ-CR-1.25 theory or the
REQ-CR-8.3 duplicate-row test. Both were perturbed by hand and every case failed.

### Journal Entries (comments and reads)

File: `Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCommentsAndReads.fs` (10 names, 15 cases). All pass.

Covers REQ-JE-2.15, 3.1.1, 3.5.1, 3.7, 3.7.1, 5.8. The tests go through the JournalEntry routes, which commit, so each
test posts its entries through a local `withEntries` helper and a finally deletes them (and their comments), newest
first. Every entry carries a fresh tag in its description, so a test only ever reads its own rows.

Refusal reasons are pinned to their typed errors: whitespace-only comment text gives `JournalEntryCommentIsEmpty`,
2001 characters `JournalEntryCommentTooLong`, and a dangling secondary `JournalEntryCommentSecondaryJeHeaderIdNotFound`
naming the missing ID (the same for REQ-JE-5.8, with `JournalEntryCommentPrimaryJeHeaderIdNotFound` for the primary).
A reference lookup with neither argument gives `JournalEntryFetchByReferenceBothArgumentsNull`, and a date range
starting after it ends gives `JournalEntryFetchByDateRangeBeginAfterEnd` naming both dates.

Mutation, k = 1 to 5: no mutant survived. The harness could not run the mutants in the REQ-JE-2.15 invalid-comment
theory. It was perturbed by hand twice: with the refusal matches inverted, all three cases failed; with the bad
comment dropped and the refusal assert disabled, the "nothing is stored" assert failed in all three.

### SystemWide (one instant per operation, atomicity) and CLI usage messages

Files: `Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs` (9 names, 13 cases; 12 pass,
1 fails, F-12) and `Tests/Tests.Integrated/SonOfLeoCli/UsageMessages.fs` (2 names, 3 cases; all pass).

Covers REQ-SYS-3.3 (one case fails, F-12), 3.4, 8.1, REQ-NGUI-3.11, 4.6. The REQ-SYS-3.3 and 3.4 tests read the
context's initiation instant from its audit envelope and check that every row the operation wrote carries it. The
update tests set up in one context and update in a second made with `Context.updateInitiationInstant`, so created-at
and modified-at must differ. The REQ-SYS-8.1 tests commit: each builds an operation whose last step fails after
earlier writes were issued (a journal entry whose last comment names a missing secondary; a batch post whose last
staged entry, a Classified one, is dated in the closed fiscal period, behind a Reviewed one dated today), then checks
that none of the earlier writes are in the database. A finally deletes whatever the test made.

**Names re-aimed after the placeholder commit (ea3a17d).** Two things changed once I saw how the behaviour is reached.
Both were regraded before the bodies were written.
- The REQ-SYS-8.1 failure name now names the two triggers, and a success-side sibling was added ("when every step is
  valid, all of the operation's writes are in the database"). The old name said "a failure part-way through", which
  a test could satisfy with a failure that happens before anything is written.
- The midnight REQ-SYS-3.4 name ("the date of an operation spanning midnight is the date of its instant") was
  dropped. The instant comes from `Clock.now()` inside `AuditEnvelope.create`, and nothing lets a test set it. I
  propose the "current date" clause of REQ-SYS-3.4 for the Unenforceable table: "untestable without a settable clock
  seam on the audit envelope".

Mutation, k = 1 to 4: every mutant in the facts was killed. The harness counts a theory as surviving if any case
passes, so the two REQ-SYS-8.1 theories showed as survivors: each assert sits in one case's branch, and the other case
still passes. The REQ-SYS-3.3 theory could not be run. All three were perturbed by hand, one assert at a time (14
perturbations). Each failed the case it belongs to (the REQ-SYS-3.3 created-at and modified-at asserts failed all
four cases and the three passing cases). UsageMessages has its asserts in a shared helper, so the harness had nothing
to mutate. Each of its three asserts was perturbed by hand and all three cases failed each time.

### Plan item 30: revised requirements

These tests cover the clauses of requirements revised since 2026-09-26 that no earlier test reached (an earlier test
may cite the requirement for an older clause). The names were drafted from the spec, graded (9 of 32 revised, 9
added), and committed as placeholders in eaeb56c.

**Accounts and Journal Entries.** File: `Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsLedger.fs`
(11 names, 12 cases). All pass. Covers REQ-AC-4.1, REQ-JE-2.11, 3.1, 3.4, 3.5, 4.4. The account test runs in a
transaction that rolls back. The journal entry tests go through the routes, which commit, and a finally deletes their
entries (newest first) and any account they made. The REQ-JE-3.4 tests post to a fresh account, so its lines are
exactly the test's. Refusals are pinned: a dangling secondary on a post or a void gives
`JournalEntryCommentSecondaryJeHeaderIdNotFound` naming it, and voiding an entry twice gives `JournalEntryVoidingNoOp`
naming the entry. REQ-AC-4.1's expected date is computed from the context's instant in America/New_York, taken from the
tz database rather than the app's configured zone. Soft edge: the Eastern date and the UTC date differ only between
20:00 and midnight Eastern, and the instant can't be set (no clock seam), so the test tells Eastern from UTC only when it
runs in those hours. The "not the UTC date" part the grader asked for is therefore proposed for the Unenforceable table
along with REQ-SYS-3.4's current-date clause. Mutation, k = 1 to 5: no mutant survived.

**Classification Rules.** File: `Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsClassification.fs`
(11 names, 20 cases). 18 pass and 2 fail (F-13). Covers REQ-CR-1.5, 1.14, 3.4, 4.3, 5.3 (two cases fail, F-13), 5.5,
6.1, 6.3. The rule tests go through the Classification routes, which commit. Each test makes its own payment
agreements and rules with a fresh tag, and a finally deletes the rules, then the agreements. The stored claimant is
read back through the model, which refuses a row holding both claimants or neither, so "no account" and "exactly one"
are checked on the stored row. The REQ-CR-1.14 tests evaluate field matches directly against a candidate. The
REQ-CR-3.4 test runs the classifier over a payment agreement rule and a non-matching account rule, made in a
transaction that rolls back. Refusals are pinned: an unknown payment agreement name, on create or on update, gives
`CashflowPaymentAgreementNameDoesntMatchId` naming the name.

**DataIngestion.** File: `Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsStaging.fs` (12 names,
25 cases). All pass. Covers REQ-STG-1.4, 1.6, 2.6, 5.1, 5.2, 7.2, 9.3, 10.2, 10.3. The file tests (1.4, 1.6) go
through the Ingestion route and clean up the file's staged entries. The rest run in a transaction that rolls back.
The posting tests run the real batch post inside that transaction, so the journal entry is fetched back from the same
transaction. The REQ-STG-1.4 refusal comes back as `IngestionFileRejected`, a file with invalid records being refused
as a whole, and every record in it carries `IngestionBaseStageEntryGroupIdTooLong` naming the value and the limit of
36. The REQ-STG-2.6 refusal is `IngestionSourceFileTooLong` with the limit of 150. REQ-STG-7.2's "every status counts
when establishing the original" was already covered for Posted, Reviewed and Duplicate originals; the new test adds
Ignored.

**CLI.** File: `Tests/Tests.Integrated/SonOfLeoCli/FileArgumentPosition.fs` (1 name, 2 cases). Both pass. Covers
REQ-NGUI-3.10's position rule. Each case also runs the file straight after the verb or report name, to show the file
is read there and would change the outcome: for the main CLI the file looks up a different account; for the Reports
CLI it isn't JSON.

**CashFlow.** File: `Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsCashFlow.fs` (6 names, 11
cases). 10 pass and 1 fails (F-14). Covers REQ-CF-8.3, 8.9, 14.2 (one case fails, F-14). All run in a transaction
that rolls back. The REQ-CF-8.3 tests check the account's itemised invoices as well as its known outflows. A fully
paid Invoice contributes nothing to the sum either way, so only the itemised list can show it was left out. The fully
paid Invoice sits on an Instance beside an unpaid one, so that Instance is not fulfilled; a second Instance has every
Invoice paid. REQ-CF-8.9 is observable through the projection because an overpaid Invoice derives PartiallyPaid, not
FullyPaid (REQ-CF-9.8). The flow-direction update test uses an agreement with no Invoices: no Outgo Invoice state is
valid for Income (REQ-CF-5.10), so "every invoice's state is valid for the new direction" is reachable only with no
Invoices.

Stale waivers: the traceability audit now lists REQ-NGUI-3.10 and REQ-STG-1.4 as waived but tested. Their waivers
predate the 2026-09-26 amendments (the position rule and the 36-character limit), so the waiver entries in Specs/ need
narrowing or removing. That is Dan's call; I haven't touched Specs/.

Mutation, k = 1 to 4, over the four files: no mutant survived, apart from one expected case. In the REQ-CF-14.2
no-op test, which already fails at its first assert (F-14), inverting that assert lets the rest pass. The harness
could not isolate theory cases, or tests whose names hold brackets and commas, so each of those was perturbed by
hand, one assert at a time (34 perturbations over the REQ-CR-5.3 claimant filter and REQ-CR-6.1 theories, REQ-STG-1.4,
2.6 and 5.1, REQ-NGUI-3.10, REQ-CF-8.3 and the REQ-CF-14.2 field theory). Each failed the case it belongs to. In the
REQ-CF-14.2 field theory, dropping the update call itself failed all six cases.

## Findings

Each finding gives the requirement, the test, what the spec says, what the code does, and where the bug probably lies.

**Dan's rulings, 2026-10-03 (commit 366b75d).** The findings below are kept as written; this is where each one ended up.
- F-1, ruled again later on 2026-10-03: the spec holds and the test's scenario was wrong. Its entry was an ordinary
  payment, so the Credit-constrained rule claimed the cash leg, which REQ-CF-6.9 rightly refuses at matching. The
  mixed-rules test now uses a refund-shaped entry (Debit F-1280, Credit F-2230) under the same name, and passes against
  the F-1 fix. Removing the constrained rule, or pointing either assertion at the other line, makes it fail.
- Src bugs, now plan item 29.9 (the plan agent's): F-1, F-4, F-5, F-6, F-7, F-9, F-10, F-12, and the payment amount
  observation. Their tests stay red until 29.9 lands, and they don't change.
- F-2: REQ-CF-12.8 now reads each rule's priority as it stands at retrieval, as REQ-CR-8.5 does. The REQ-CF-12.8
  test still matches. The REQ-CR-8.5 test that re-prioritises a rule after the run covers the new clause.
- F-3: REQ-CF-12.5 now says only the highest-priority rule's agreement counts as claimed by the line. The
  REQ-CF-12.2/12.5 test already asserts exactly that, and it passes.
- F-8 and F-14: REQ-STG-6.3.2 and REQ-CF-14.2 now say a field set to its current value counts as named, so it is not
  a no-op. The same-value cases are dropped. The "names no field" case stays for REQ-STG-6.3.2, and REQ-CF-14.2 gets
  one.
- F-11: the plan thread fixed the fixture (c8a175f). There is no guard against posting an entry that already has a
  journal entry ID; reconciliation surfaces a double post.
- F-13: the code is right. REQ-CR-5.6 says a payment agreement name that doesn't resolve fails with a typed error. The
  two cases now expect that error.
- Q-1: REQ-CF-6.4 now says a Payment carries exactly one pointer at creation. The existing REQ-CF-6.4 tests already
  assert that.

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

**F-7. REQ-STG-1.17: a padded account code in a file is not trimmed. Bug in Src.**
- Test: `StagingIngestionRules.fs`, the padding theory, case `accountCode`. The file is rejected: "Account code of
  `   F-2230` doesn't match an Account ID in the database".
- Spec: "Text fields are trimmed before validation (REQ-SYS-1.1)."
- Code: `convert AccountCodeString Option to AccountId Option` looks the code up as it arrived. `AccountCode.create`
  trims, but that path doesn't go through it. The other six text properties are trimmed; their cases pass.

**F-8. REQ-STG-6.3.2: a manual update that sets fields to the values they already hold is accepted. Bug in Src.**
- Test: `StagingIngestionRules.fs`, the REQ-STG-6.3.2 theory, case "setting every named field to its current value".
  The update sets the description, reference and entry date, and one line's amount, account and memo, each to its
  current value. It succeeds.
- Spec: "A manual update that changes nothing is rejected, per REQ-SYS-6.1." Only a status-only update to the current
  status is excepted.
- Code: `updateStageEntry` rejects an update only when every field is `NoChange`
  (`IngestionUpdateStageEntryNoOp`). A field set to its own value counts as a change, and the header and line updates
  are written. The "naming no fields" case is rejected with `IngestionUpdateStageEntryNoOp` and passes.

**F-9. REQ-STG-6.7: an entry with a paid line can be made Duplicate or Ignored. Bug in Src.**
- Tests: `StagingIngestionRules.fs`, the REQ-STG-6.7 theory (Duplicate and Ignored both succeed) and the dedup test
  (the paid repeat is flagged Duplicate; an unpaid repeat beside it is flagged too, as it should be).
- Spec: "A staged entry with any line referenced by a Payment cannot transition to 'Duplicate' or 'Ignored', by any
  operation. The attempt fails with a typed error (manual update) or is reported without flagging (dedup)."
- Code: `updateStageEntry` checks Payments only when a line is removed or its amount, line type or account changes
  (`protectionsOf`). A status change isn't checked. `StageEntryHeader.fetchDuplicates` doesn't look at Payments, so
  `deduplicateStagedEntries` flags the entry.
- Test gap: "reported without flagging" can only be checked in part. The dedup result is a list of the entries still
  Ingested and carries no reason, so the test asserts only that the paid entry keeps its status and is listed. If Dan
  wants the report to say why, the result contract needs a field for it.

**F-10. REQ-STG-3.13: ingestion returns a status transition that was never stored. Bug in Src.**
- Test: `StagingIngestionRules.fs`, "ingestion returns every staged entry it created and no other...". The entries,
  headers and lines match what was stored. The one returned transition per entry has an ID that isn't in the database.
- Spec: "Ingestion returns every staged entry it created, each with its full composition (header, lines, status
  transitions)."
- Code: `constructGroup` builds an Ingested transition with its own ID and returns it on the entry. `persistConstructed`
  never writes that transition. `StageEntryHeader.persist` calls `updateHeaderStatus`, which writes a second Ingested
  transition with a new ID at the same instant. The route returns the constructed entries, not what was stored.
- Fix: return the entries as read back after the write, or persist the constructed transition.

**F-11. Intermittent full-run failures: the shared fixture stamps a staged entry's Classified transition after its
Posted transition, so a later batch post posts it a second time. Bug in the test fixture (Tests.Helpers), with a Src
question.**
- Status: fixed in the fixture by the plan thread (c8a175f, 2026-10-03). The Src question below is still open.
- Symptom: in 2 of 3 full runs, 5 tests fail with counts one entry (two lines) too high: AccountActivity's "by account
  returns all" (55 vs 57), "by amount" (12 vs 14) and "unVoidedOnly", JournalEntryFetching's "fetchByReference with FI
  only" (7 vs 8), and the JournalEntry PostNew route test (21 vs 22). Each passes on its own. Test class order is
  shuffled per run, so it depends on what ran first.
- Cause, seen by polling `ledger.journal_entry` during a failing run: a second journal entry "Fixture agreement B
  payment" appears mid-run. Its staged entry's audit trail reads Ingested (13:21:28.060756), Classified to Posted
  (28.070004), Ingested to Classified (28.070756), then Classified to Posted again (46.738772, a test's batch post).
- `Tests/Tests.Helpers/TestDataStage.fs:961` `transitionsTo` stamps the transitions at `Clock.now()` plus 10 ms per
  step, in the future. The fixture posts in `postingContext` straight after. When that happens within 10 ms, the
  Posted transition is older than the Classified one, the entry's current status reads as Classified, and any later
  committed batch post (`fetchAllForPosting`) posts it again. Whether it happens depends on timing, hence the flicker.
- Fix (not mine to make; Tests.Helpers belongs to the plan work): stamp `transitionsTo` in the past (`start.Minus`),
  or take the posting instant after the last stamped transition.
- Src question for Dan: posting accepts a staged entry that already has a `journal_entry_header_id`, so corrupt
  status history leads to a double post rather than a refusal. REQ-STG / R-17 says Posted is terminal; a guard on the
  header's journal entry ID would make that hold even when the audit order is wrong.

**F-12. REQ-SYS-3.3: re-pointing a payment agreement link leaves its modified-at unchanged. Bug in Src.**
- Test: `OperationInstantAndAtomicity.fs`, "REQ-SYS-3.3 for each update (...)", case "re-pointing a payment agreement
  link". The link's created-at is right. Its modified-at is still the creating instant, not the updating one
  (assert at line 150).
- Spec: every update sets the record's modified-at to the operation's initiation instant; created-at is unchanged.
- Code: `PaymentAgreementLink.update` (Src/Business.FinancialServices.CashFlow/PaymentAgreementLink.fs:180) builds
  its SET clause from the field updates alone and never adds `modified_at = @modified_at`.
- Fix: add the modified-at clause from the context's instant, as the other updates do.

**F-13. REQ-CR-5.3: the payment agreement claimant filter, given a name no payment agreement has, returns an error instead of no rules. Src or spec; Dan to decide.**
- Test: `RevisedRequirementsClassification.fs`, "REQ-CR-5.3 the payment agreement claimant filter given only part of a
  payment agreement's name, or the full name in the wrong case, returns no rule". Both cases fail the same way.
- Spec: rules are retrieved by "payment agreement claimant (by payment agreement name, exact)". A filter nothing meets
  returns no rules.
- Code: the route converts the name to a payment agreement ID before filtering
  (`Src/Ui.InterfaceBridge/BoundaryConverters/ClassificationFieldConverters.fs:259`), and an unknown name fails there
  with `CashflowPaymentAgreementNameDoesntMatchId`. The account code filter goes through the same kind of conversion.
  The exact-match part holds: partial and wrong-case names don't match anything.
- My view: this is a Src bug if a filter should behave like a filter; the error is defensible as a guard against a
  typo. If Dan prefers the error, the spec should say so and the test name changes.

**F-14. REQ-CF-14.2 (with REQ-SYS-6.1): an update setting every master agreement field to its current value is accepted. Bug in Src.**
- Test: `RevisedRequirementsCashFlow.fs`, "REQ-CF-14.2 an update to a master agreement that sets every field to its
  current value is rejected with the no-op error and the agreement is unchanged". The update returns Ok.
- Spec: REQ-CF-14.2 says "An update that changes nothing is rejected (REQ-SYS-6.1)".
- Code: `isThereAMasterAgreementUpdate` (`Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:350`) only
  checks whether any field is `SetTo`, not whether the value differs. This is the same pattern as F-8 for staged
  entries.
- Fix: compare each `SetTo` value with the stored one and treat equal values as no change.

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
- §12–§13: REQ-CF-12.1, 12.2, 12.3 (more cases), 12.4 (the F-1 mixed-rules case, rewritten to a refund), 12.5, 12.6, 12.7, 12.8, 13.1 (more
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
- DataIngestion: REQ-STG-1.17 (one case fails, F-7), 2.25, 2.26, 2.27, 3.11, 3.13 (fails, F-10), 3.14, 3.15, 4.1.1,
  5.11, 6.3.1, 6.3.2 (same-value case dropped after F-8), 6.7 (fails, F-9), 7.5.1, 8.5, 9.10, 9.11.
- Classification Rules: REQ-CR-1.23, 1.24, 1.25, 3.7, 3.8, 5.6, 8.1, 8.2, 8.3, 8.5.
- Item 30 (revised clauses): REQ-AC-4.1; REQ-JE-2.11, 3.1, 3.4, 3.5, 4.4; REQ-CR-1.5, 1.14, 3.4, 4.3, 5.3, 5.5, 5.6 (the
  F-13 cases, re-aimed), 6.1, 6.3; REQ-STG-1.4, 1.6, 2.6, 5.1, 5.2, 7.2, 9.3, 10.2, 10.3; REQ-NGUI-3.10; REQ-CF-8.3, 8.9,
  14.2 (same-value case dropped after F-14; a names-no-field case added).
- Dan's rulings, 2026-10-03: REQ-CF-6.5 and 9.8, a Payment's line amount decides the Invoice's payment state (four
  cases, failing until plan item 29.9).
- SystemWide: REQ-SYS-3.3 (one case fails, F-12), 3.4, 8.1.
- CLI: REQ-NGUI-3.11, 4.6.
- Journal Entries: REQ-JE-2.15, 3.1.1, 3.5.1, 3.7, 3.7.1, 5.8.
- Accounts: REQ-AC-2.22, 2.23, 3.11, 3.12, 3.12.1, 3.12.2, 3.12.3, 3.12.4, 3.13, 3.13.1, 3.13.2, 3.13.3.
- §7: REQ-CF-7.1, 7.2, 7.3, 7.4, 7.6, 7.7, 7.8, 7.9, 7.10, 7.12, 7.14, 7.15, 7.16 (fails, F-4), plus REQ-CF-4.8.

## Requirements still uncovered

None. After Dan's rulings (366b75d) the audit's Invariant 2 listed only REQ-JE-4.13 and REQ-JE-4.14, the plan agent's
void rework; by the end of 2026-10-03's runs those are covered too and Invariant 2 is clean.

- **Waivers:** Dan approved all eleven proposed CashFlow waivers: REQ-CF-2.1, 2.2, 3.1, 3.2, 4.1, 4.2, 4.4, 5.1, 5.11,
  5.12 and 6.1.
- **Unenforceable:** REQ-CF-7.13 and REQ-CF-10.6 were approved. The clock clauses of REQ-SYS-3.4 and REQ-AC-4.1 get no
  entry: the audit works per ID, and both IDs are tested.
- **Stale waivers:** REQ-NGUI-3.10, REQ-STG-1.4, REQ-RPT-3.1 and REQ-SYS-6.1.1 were removed rather than narrowed. The
  audit lists no stale waivers now.
- **Untested clause:** REQ-STG-6.3.2 and REQ-CF-14.2 now say a field set to its current value counts as named. No test
  asserts that such an update succeeds, because Dan said to drop the same-value cases rather than flip them.

**Invariant 1 (withdrawn references).** Clean: the plan agent removed the last citations of withdrawn requirements.

**Contract fields that item 29.9 removes.** The Payment amount fix takes `amount` off the create-Payment contracts, and
the F-5 fix takes `paymentStateUpdate` and `postedStateUpdate` off `UpdateInvoiceInput`. Several existing test files
build those records (`PaymentDataStates.fs`, `InvoiceDataStates.fs`, `InvoiceStateByDirection.fs`,
`MaintenanceOperations.fs`, `CashFlowMaintenance.fs`, `DerivedStateRules.fs` and `StageEntryUpdate.fs`). They won't
compile until the field is dropped from those record literals. That is a mechanical change; what each test asserts
stays the same. The new REQ-CF-6.5/9.8 test sends its payload as JSON, so it compiles with or without the field.

## Suites run

All runs were on 2026-10-03, in the cloud container, against the throwaway Postgres database `sonofleo_test` on
localhost (rebuilt with `Tests/Tests.Integrated/setup-throwaway-test-db.sh`), after the plan agent's item 29.9 (e03a428)
and the F-1 test rewrite.
- `dotnet test Tests/Tests.Isolated`: 349 passed, 0 failed.
- `dotnet test Tests/Tests.Integrated`: 1229 passed, 1 failed, of 1230. The build has no warnings. Every 29.9 test now
  passes, the payment amount cases included. The plan agent's placeholders for the withdrawn REQ-JE-4.11/4.12 are gone.
- The one failure is not from this backlog. `Reports/Program.fs`, "REQ-NGUI-1.3.1, REQ-NGUI-4.4 The stderr will comprise
  the error message", compares the CLI's stderr, stack trace included, with the same error raised in the test process.
  In the full run the two traces differ inside .NET's file-opening frames (`SafeFileHandle.Open(String fullPath,
  FileMode ...` against `Open(String path, OpenFlags ...`). Run on their own, all six `Reports.Program` tests pass,
  twice. So the test depends on run order through runtime internals; it doesn't show a Src bug. I haven't touched it.

## Traceability audit result

`bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh .` reports Invariant 1 clean and Invariant 2 clean, with no
stale waivers. Every active requirement has a citing test, a waiver or an Unenforceable entry, and no test cites a
withdrawn or unknown requirement.
