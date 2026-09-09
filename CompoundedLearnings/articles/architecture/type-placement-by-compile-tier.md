# Type Placement by Compile Tier

**Source:** CashFlow scaffolding session, 2026-09-08 — five return types declared at the top of
`CashFlowOps.fs` that belonged in `Model/`.

The type-taxonomy article decides *what kind* of type you are building. This one decides *which
file it lives in*: a type belongs in the lowest compile tier that can see every type it
references. Find that by walking the tier order, not by asking which domain owns the concept.

## What works

- Read the `<Compile Include>` order in the `.fsproj` and work down it. The first file that
  already sees every dependency is the home.
- When a slice's own component file can't see one of the dependencies, check the slice *below* it
  before concluding `Model/` is closed. `StageDataClassificationComponent.fs` is the last
  component tier and sees `AccountId`, `PaymentAgreementId`, and `StageEntryLineId` — which is
  why `PaymentAgreementClaimCluster` lives there rather than in `CashFlowComponent.fs`.
- Only a type that references an orchestrator-level type — a composite — is genuinely forced up
  into `ModelOrchestrator/`. Declare that one beside the composite it depends on.

## What doesn't

- Don't place by domain ownership. "This is cash flow's bookkeeping, so it goes in the cash flow
  file" gives the wrong answer whenever the type also carries a classification or ledger id.
- "A domain records its own bookkeeping on its own table" is a rule about **columns on tables**.
  It says nothing about which file declares a type, and applying it to placement inverts the
  answer.
- A clean build is not evidence of correct placement. The top of an orchestrator file sees
  everything, so anything compiles there. That is precisely how basic types accumulate in
  `CashFlowOps.fs`.

## Example

The scaffolding step declared `PaymentAgreementDecision`, `InvoiceDecision`, their two outcome
DUs, and `PaymentAgreementClassificationResult` at the top of `CashFlowOps.fs`. It built clean.
Four of the five belonged in `Model/`: the payment agreement decision log in
`StageDataClassificationComponent.fs` beside `PaymentAgreementClaimCluster`, and the invoice
decision log in `CashFlowComponent.fs`, which already opens `StageEntryComponent` and uses
`StageEntryLineId` for `TransactionPointer`. Only `PaymentAgreementClassificationResult` was
orchestrator-bound — it holds `InstanceComposite list` — and it went under `InstanceComposite` in
`InstanceOrchestration.fs`.

The wrong turn was one unasked question: `CashFlowComponent.fs` can't see `ClassificationRuleId`,
so `Model/` was declared closed without testing the tier below it.
