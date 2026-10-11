# A Failure Vector Is a User Interaction

**Source:** Session 2026-08-17. BD read "test every failure vector once, at the lowest
possible level" as forbidding route-level validation cases, and argued the
`MoneyFailedToConvertImproperPrecision` rows in `JournalEntryRoutes.fs` were redundant with
the isolated `Money` tests. Dan: the rule is fine, the definition of "vector" was wrong.

A failure vector is a **failed user interaction**, not a technical failure mode. Getting back
the wrong error message is its own vector. Count vectors correctly and "test each one once, at
its lowest possible level" needs no exception.

## What works
- Naming a vector as an actor plus an outcome before choosing its layer. "`Money.fromDecimal`
  rejects sub-cent precision" is one vector, and its lowest layer is an isolated `Money` test.
  "A caller posting a journal entry with a bad amount gets an error that names the amount" is
  a *different* vector with a different actor, and its lowest layer is the route — because the
  route is the lowest place a caller exists at all.
- One InterfaceBridge case per (input field, converter) pair where skipping the converter would
  let bad input through silently (see the Example). It proves the converter is reached.
  Where the route would still reject loudly without it, writing nothing, there is no
  silent failure to guard, and no case is needed (amended 2026-10-11, see below).
- Judging error legibility per caller. `AccountCodeDoesntMatchAccountId` answers "post this
  journal entry" fine. As an answer to "create this account" it is ambiguous — the account's
  own code, or the parent's? Same error case, different quality of answer, different vector.

## What doesn't
- Collapsing two vectors into one because they happen to trip the same `AppError` case.
- Deleting a route-level validation case because "the constructor is already tested." A
  correct constructor says nothing about whether anything calls it.
- Asserting only that an error occurred. The identity of the error is the thing under test.

## Example
`ingestRawEntries` deserialized straight into the domain type `BaseStageRawRow` instead of a
primitives contract. `JsonFSharpConverter` built the private types by reflection, so
`Money.fromDecimal`, `JournalEntryDescription.create` and five other constructors were never
called — an over-max amount and a 1001-character description reached staging unchallenged.
Every one of those constructors had passing isolated tests, so the constructor vector was
covered. The user-interaction vector was not, and nothing else could have caught it. Contract
and converter in `989013e4`, tests in `ff85df4`.

## Amended 2026-10-11 (Hobson)
The later loud-failure ruling (2026-09-28, `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`
LOUD-FAILURE) narrows the per-pair rule. A vector still counts as a user interaction, and a
route is still the lowest layer where a caller exists. But a route-level case earns its place
only when the failure it guards is silent, as in the Example: bad values reaching staging
unchallenged. A route that rejects an unknown key or a malformed value with an error, and
writes nothing, fails loudly either way; which error it names is message quality, not a
vector. Audit 2026-10-11a #022/#023.
