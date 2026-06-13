# Money

## The Money type

Being an accounting system, money is the singularly most important concept to get right. Therefore, we insist that the system create and enforce a specific Money type any time it represents Money. 

## Currency and precision

All Money entities are considered denominated in USD. There will be no indication of this in the persistence layer or application code. It is the expectation that outside actors will convert any non-USD-denominated monies to USD prior to engaging the interface layer.

All Money entities will be represented at penny precision in all layers (interface, application, and persistence).

No layer in this system will track or persist fractions of pennies.

## System primitives

Money amounts will be represented as F# `decimal` types in the application layer.

Money amounts will be persisted using a Postgres `numeric (12,2)` column type.

Any other primitive type or column types are prohibited from representing Money amounts.

## Direct arithmetic on Money records

Multiplication and division operations are strictly prohibited with Money records

Addition and subtraction operations are permitted, though the system must ensure all type enforcement rules are enforced on the result. 

## Arithmetic with money amounts

For certain use cases, it may be necessary for the system to perform division or multiplication operations on the underlying amount. Examples include splitting a transaction 3 ways or applying an interest rate to a monthly mortgage payment. The rules in this section govern how the system must interact with the Money record in such cases: 

The system must first unpack the Money amount down to a non-money primitive type before such operations.

The system must finally repack the result into a valid Money record (or collection of valid Money records) when done. 

Each function doing so may determine when in their flow rounding is most appropriate, though it is encouraged for functions to keep all rounding as close to the non-money-to-money boundary.

When rounding is required the system must employ a "half-up" rules (e.g.: `MidpointRounding.AwayFromZero`). Note that .NET's `Math.Round` default is banker's rounding (half-to-even), so the system should always pass the rounding mode explicitly.

When splitting transactions, the allocation must sum exactly to the original pre-split amount. Therefore, the system must force any residual into one of the resultant part.

## Leftovers

- **No tolerance in intra-system arithmetic.** Numbers this system computed must agree
  exactly; an epsilon between them is a bug amnesty. Reconciliation tolerances against
  external statements are domain *data* (specced thresholds, per account class), never
  code epsilons — see Decisions, 2026-06-11.
