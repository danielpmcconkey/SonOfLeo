# DAL Errors Are Backstops, Not Operator-Facing Errors

**Source:** Dan's clarification, 2026-08-28, from a `Src/ModelOrchestrator` pattern-review
session; layered-authority correction added the same day.

A generic `DataAccessLayer` error like `DalResultantRowsDidntMatchExpectation` is a mechanical
signal ("the query returned a different row count than expected"), not a meaningful error for
the person using the app. Translating it into something meaningful is a matter of *use-case
context* — and use-case context increases as you go up the layers, not just at one designated
translation point.

## A chain of increasing authority, not a single choke point

`Model/` has the least use-case context — it only knows about its own row. `ModelOrchestrator`
knows more — it knows what composite or multi-step operation is underway. `InterfaceBridge`
knows the most — it knows which actual UI or CLI operation the user invoked. Each layer is a
*higher authority* than the one below it, and each is free to translate a lower layer's error
into something more meaningful if — and only if — it actually has enough context to do so
correctly. None of them is uniquely "the" translation point, and a DAL error passing through
orchestration unchanged is not automatically a failure — it may be entirely correct for
`InterfaceBridge` to be the layer that finally knows what to say, because it's the one that
knows what the user was trying to do.

The same DAL error can mean completely different things depending on which call produced it.
"Account code not found" is a fine, complete error for a `JournalEntryLine` construction — there's
only one account reference to be wrong, and `Model/` itself has enough context to say so.
It's a bad error, unresolved, for an `Account` fetch that involves both a primary account and a
parent account lookup — "not found" doesn't say *which* lookup failed, and the orchestrator is
the first layer with enough context to disambiguate. This is why the DAL-error-to-domain-error
match arm shows up repeatedly across `ModelOrchestrator/*.fs` — it's not boilerplate to
eliminate, it's the same generic signal getting a different, specific meaning at whichever layer
first has enough context to give it one.

## The rule

If a raw `DalResultantRowsDidntMatchExpectation` (or any other DAL-level error) ever reaches the
operator, *unretranslated by any layer*, that means one of two things: some layer that had
enough context to translate it didn't do its job, or something has gone seriously wrong with the
data itself. Neither is an acceptable steady state. It does not mean "the orchestrator must
always be the one that translates it" — only that translation has to happen somewhere before the
operator sees it, at whichever layer is the first to actually know what the error should mean.
Roughly 100 tests exist specifically to confirm meaningful, use-case-appropriate errors reach the
operator rather than DAL backstops leaking through — see the `TestWriter` skill and
`catalogs/testing.md` for how that's enforced on the test side.

## What works

The repeated shape, seen throughout `ModelOrchestrator`:

```fsharp
match id |> Entity.fetchById context with
| Ok _ -> Ok ()
| Error (DalResultantRowsDidntMatchExpectation(expected, actual)) ->
    if actual = 0 then Error (EntityIdDoesntExist (id |> Id.value))
    else Error (DalResultantRowsDidntMatchExpectation(expected, actual))
| Error e -> Error e
```

Only `actual = 0` becomes a friendly, specific "doesn't exist" error. Any other row-count
mismatch re-raises the raw DAL error unchanged — that's not "not found," that's a genuine
integrity problem, and it should surface as loudly and unhelpfully as it deserves rather than
being papered over with a wrong-sounding "not found."

## What doesn't

- Assuming a generic DAL error is "good enough" because it's technically accurate. It's
  accurate and useless — the operator needs to know *what* wasn't found, in the context of
  *what they were trying to do*.
- Writing one shared "not found" translator and reusing it verbatim across unrelated use cases.
  The translation is supposed to be use-case-specific; reuse only when two call sites really do
  mean the same thing to the operator.
- Treating "the orchestrator didn't translate this DAL error" as automatically wrong. It's only
  wrong if *no* layer above it translates it either — `InterfaceBridge` may legitimately be the
  layer that finally has enough context, and an orchestrator function passing a DAL error
  through untranslated can be entirely correct if it genuinely doesn't have the use-case context
  yet to know what the error should mean.
