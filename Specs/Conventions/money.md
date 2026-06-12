# Money

- **Exact decimal, end to end.** Money is F# `decimal` in code and `numeric` in Postgres —
  both base-10 and exact; the pair round-trips losslessly. Banned everywhere money could
  appear: `float`/`double` in code; `real`, `double precision`, and the locale-poisoned
  `money` type in the schema. Like the temporal type ban, this is enumerable over
  `information_schema` and can graduate to a tested REQ-DAL requirement.
- **Ledger amounts are USD at scale 2.** Two decimal places, always. Sub-cent precision
  belongs to prices and quantities, which get their own wider types in their own domain
  (portfolio, later) — never to a ledger amount. A multiplication of wide types rounds to
  a ledger amount once, at the domain boundary. (How scale is enforced — at construction
  or elsewhere — is under discussion, 2026-06-12.)
- **Rounding is half-up** (`MidpointRounding.AwayFromZero`), applied once at a boundary,
  never repeatedly mid-calculation. Note that .NET's `Math.Round` default is banker's
  rounding (half-to-even) — always pass the mode explicitly.
- **Allocation must sum exactly.** When a total is split across parts (category splits,
  proration), round the parts and force the residual into the final part so the parts sum
  to the whole. The whole is the truth; per-part prettiness loses.
- **No tolerance in intra-system arithmetic.** Numbers this system computed must agree
  exactly; an epsilon between them is a bug amnesty. Reconciliation tolerances against
  external statements are domain *data* (specced thresholds, per account class), never
  code epsilons — see Decisions, 2026-06-11.
