# Money

How the system handles Money values (see Definitions)

## 1. Valid and invalid data states for Money values

- **REQ-MON-1.1** Money values are always denominated in US Dollars.
- **REQ-MON-1.2** The maximum value for a Money value is 9,999,999,999.99 USD.
- **REQ-MON-1.3** The minimum value for a Money value is -9,999,999,999.99 USD.
- **REQ-MON-1.4** Money must never be expressed with a numeric precision greater than two decimal places.

## 2. Operations on or with Money values

- **REQ-MON-2.1** Functions that are intended to operate on or with values that meet the Definitions.md definition for "Money (as a variety of number)" must only take explicit Money type arguments and must only return explicit Money type values.
- **REQ-MON-2.1.1** Exceptions are explicitly designed boundary functions that interface with primitives from other systems, layers, etc.
- **REQ-MON-2.2** The system must allow the conversion of a .NET decimal type into a Money type.
- **REQ-MON-2.2.1** The system must validate that all requirements from section 1 are met when doing so. (Except 1.1, which is unenforceable)
- **REQ-MON-2.3** The system must allow the conversion of a collection of .NET decimals type into a collection of Money types.
- **REQ-MON-2.3.1** The system must validate that all requirements from section 1 are met when doing so.
- **REQ-MON-2.3.2** The system will preserve the sort / positional order when doing so.
- **REQ-MON-2.4** The system will provide a means for other system functions to split a Money value N ways
- **REQ-MON-2.4.1** The system must validate that the sum of the split shares is exactly equal to the original pre-split amount 
- **REQ-MON-2.4.2** The system will reject any attempt to split zero ways
- **REQ-MON-2.4.3** The system will reject any attempt to split one way
- **REQ-MON-2.4.4** The system will round the share amount to 2-decimal precision using mid-point away from zero rounding
- **REQ-MON-2.4.5** Any remainder or difference due to the fractional rounding will be applied (either added or subtracted) to the first share only and in its entirety.
- **REQ-MON-2.4.6** The system will reject any attempt to split into a quantity of shares fewer than 0
- **REQ-MON-2.5** The system will provide a function for adding two Money values directly
- **REQ-MON-2.5.1** When so doing, the system will ensure the result is valid according to all rules stated in section 1
- **REQ-MON-2.6** The system will provide a function for subtracting one Money value from another directly
- **REQ-MON-2.6.1** When so doing, the system will ensure the result is valid according to all rules stated in section 1
- **REQ-MON-2.7** The system will never allow any mathematical operation that results in a multiplication or division of one Money value against any numerical type (including another Money value) 
- **REQ-MON-2.7.1** Any use case that would imply such a need must be met by first converting to a .NET decimal type, then performing necessary mathematics, and finally converting back into a Money type before the system can again treat the value as Money 
- **REQ-MON-2.8** The system will provide a function for converting a Money type to a .NET decimal type
- **REQ-MON-2.9** The system will provide a function for summing the amounts from a list of Money
- **REQ-MON-2.9.1** When so doing, the system will ensure the result is valid according to all rules stated in section 1 

## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-MON-2.1 | You cannot test for the total absence of something | Dan, 2026-06-19 |
| REQ-MON-2.1.1 | You cannot test for the total absence of something | Dan, 2026-06-19 |
| REQ-MON-2.7 | You cannot test for the total absence of something | Dan, 2026-06-19 |
| REQ-MON-2.7.1 | You cannot test for the total absence of something | Dan, 2026-06-19 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| REQ-MON-1.1 | Nothing in the system tracks currency. USD-only is by convention | Dan, 2026-06-19 |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
|  |  |  |