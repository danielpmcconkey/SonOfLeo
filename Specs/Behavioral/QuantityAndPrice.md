# Quantity and Price

How the system handles Quantity and Price values (see Definitions). They are peers of Money: a
Quantity counts units that are not currency, a Price converts a Quantity into currency, and
neither is ever Money.

**Design note — exact values.** Quantity and Price hold exact decimal values, as Money does.
They carry figures an institution reported (shares held, the price it used), and the system
must give them back exactly as received. A simulator that needs fast floating-point arithmetic
converts at its own boundary; that is not a reason to loosen these types. (2026-10-04)

**Design note — rejection, not rounding.** A value with more decimal places than a type allows
is rejected, never rounded. The supplier (usually a parser) decides how to round; the system
never silently changes a reported figure. (2026-10-04)

## 1. Valid and invalid data states for Quantity values

- **REQ-QP-1.1** A Quantity value cannot be negative.
- **REQ-QP-1.2** The maximum value for a Quantity value is 9,999,999,999.999999.
- **REQ-QP-1.3** A Quantity value must never be expressed with a numeric precision greater than six decimal places.
  - *Why six:* institutions report share quantities to three or four decimal places; six leaves room for any that report more, without admitting noise. (2026-10-04)

## 2. Valid and invalid data states for Price values

- **REQ-QP-2.1** A Price value cannot be negative.
- **REQ-QP-2.2** The maximum value for a Price value is 9,999,999,999.999999.
- **REQ-QP-2.3** A Price value must never be expressed with a numeric precision greater than six decimal places.

## 3. Operations on or with Quantity and Price values

- **REQ-QP-3.1** The system must allow the conversion of a .NET decimal into a Quantity, validating every requirement in section 1.
- **REQ-QP-3.2** The system must allow the conversion of a .NET decimal into a Price, validating every requirement in section 2.
- **REQ-QP-3.3** The system will provide a function for converting a Quantity to a .NET decimal, and one for converting a Price to a .NET decimal.
- **REQ-QP-3.4** The system will provide a means to compare two Quantity values, and two Price values: equal, less than, and greater than (and their inclusive forms).
- **REQ-QP-3.5** The system will provide a function that multiplies a Quantity by a Price and returns the product as a .NET decimal, exact whenever the product is less than 10^16. The product is Money in the Definitions sense but is not the system's Money type; a caller that needs the Money type converts it under REQ-MON-2.2. (Amended 2026-10-04; 2026-10-05)
  - *Why the bound:* a .NET decimal holds 28 to 29 significant digits, and the largest Quantity times the largest Price needs 32. Every product below 10^16 fits exactly. A larger product exceeds the largest Money value a million times over, so no market value could agree with it and REQ-POS-6.8 rejects it either way. (2026-10-04)

## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
|  |  |  |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
|  |  |  |
