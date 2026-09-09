# Record Field Inference Collision

**Source:** CashFlow scaffolding session, 2026-09-08.

F# infers an unannotated parameter's record type from the field labels used on it, and resolves
those labels to the **last** record type declared in scope that carries them. Adding a new record
with a field name that already exists silently retargets code that compiled yesterday. Same
failure family as `du-case-collision-across-opens.md` — last declaration wins, silently — but it
needs no `open` to fire: one module declaring two records with a shared label is enough.

## What works

Annotate the parameter of any local helper or lambda that reaches into a record:
`let isTied (result: ClassificationResult) : bool = ...`. It costs one type name and pins the
inference where the author meant it.

Treat generic field labels — `outcome`, `status`, `result`, `amount` — the way the DU article
treats generic case names. A new record carrying one is a candidate to break something already
written, so grep the label before settling on it.

## What doesn't

Don't expect the error to name the type you meant. It reports the type it picked, at a line that
used to work, and the intended type appears nowhere in the message.

Don't assume an unused type is inert. Declaring it is enough — nothing has to call it.

## Example

`StageEntryOrchestration.fs` held
`let isTied result = match result.outcome with | ManyMatchesTied _ -> true | _ -> false`,
inferring `ClassificationResult` from the `outcome` label. Adding `PaymentAgreementDecision` —
also carrying an `outcome` field, declared later in `StageDataClassificationComponent.fs` —
flipped the inference, and the file failed with "expected `PaymentAgreementDecisionOutcome` but
here has type `ClassifierOutcome`". `isMatch`, four lines above and doing the same work, was
annotated and unaffected.
