# Money Arithmetic Boundaries

**Source:** Money.md, Direct Arithmetic / Arithmetic with Money Amounts / Leftovers

Money records have strict arithmetic boundaries. Some operations are prohibited directly; others require unpacking first.

## Direct operations on Money records

- **Addition and subtraction:** permitted, but the result must satisfy all Money type enforcement rules
- **Multiplication and division:** strictly prohibited on Money records

## Unpacking for arithmetic

When multiplication or division is needed (splitting a transaction, applying an interest rate):

1. Unpack the Money amount to a non-money primitive (`decimal`)
2. Perform the arithmetic
3. Repack the result into a valid Money record (or collection of Money records)

## Rounding

- **Half-up** (`MidpointRounding.AwayFromZero`) — always pass the rounding mode explicitly
- .NET's `Math.Round` defaults to banker's rounding (half-to-even), which is **not** what we use
- Keep rounding as close to the non-money-to-money boundary as practical

## Allocation

- When splitting, the parts must sum **exactly** to the original pre-split amount
- Force any residual into one of the resulting parts — no tolerance, no rounding loss

## No intra-system tolerance

Numbers this system computed must agree exactly. An epsilon between them is a bug amnesty, not a materiality judgment. Reconciliation tolerances against external statements are domain data (specced thresholds, per account class), never code epsilons.
