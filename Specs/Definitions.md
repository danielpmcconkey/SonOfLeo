# Definitions

Terms with a SonOfLeo-specific meaning, defined once, above the individual domains so that behavioral specs, conventions, and decisions can all lean on the same words. Admission rule: a term earns an entry only when its meaning changes which requirements apply or how they are verified. Plain English stays in the dictionary.

## The system
Any technology component whose source code or whose configuration exists in the SonOfLeo repository. Includes any binaries or CIL produced by building this solution and any database structure or behaviors defined in this repository. 

## Money (as a variety of number)
An amount denominated purely in currency (USD). Money is the only concept that sums meaningfully: totals, balances, and ledger entries are all sums of Money. Examples of real-world concepts that the system should define as Money:
- Dan paid 600.06 USD at the liquor store (the total accumulated transaction)
- Dan's checking account has a balance of -17.40 USD

Note: the type defined in Model.Money is the only example of a type in this system that fits within the Money definition. However, in future, we will be building out a Monte Carlo simulation that will have its own type that fits within the Money definition. In short, this definition encompasses the Model.Money type *and* future "Money" types but rules governing the Model.Money type do not all apply to all types defined under the "Money" definition.

## Price (as a variety of number)
A ratio of currency to a non-currency unit: USD per share, USD per month. A Price is never summed and never appears in a ledger; its only arithmetic role is converting a Quantity into Money by multiplication. Examples of real-world concepts that the system should define as Price:
- The per-share valuation of a stock
- A per-month rent obligation

## Quantity (as a variety of number)
A count denominated in units other than currency: shares, months, items. A Quantity carries no monetary value of its own; it becomes Money only by multiplication with a Price. Examples of real-world concepts that the system should define as Quantity:
- The number of stock shares purchased
- The maximum number of tenants allowed in a property 

## Rate (as a variety of number)
A dimensionless proportion — a pure multiplier, usually expressed as a percentage and often per time period. A Rate is denominated in neither currency nor count; it scales a Money value without changing its units. Examples of real-world concepts that the system should define as Rate:
- The APR on a loan
- A dividend yield

## Entity (as a variety of record)
A record type the system creates or mutates at runtime on behalf of the user. Two litmus questions for any table: (1) does any user action ever insert or update a row? Yes → entity. (2) Could the table's entire contents be regenerated from spec and code alone? Yes → lookup, not an entity. Classification is by behavior, not shape: a lookup-shaped table becomes an entity the moment users can extend it at runtime.

## Instant (temporal)
A singular and globally agreed-upon point in time, independent of the geography, civil prescript, or calendar convention.

## Date (calendar)
A calendar coordinate: the name of a single day within a specific calendar (e.g., 2026-03-30, Gregorian). A date has no time component and no fixed duration. The span of instants a date covers is determined only when an observer's time zone is applied--and may be 23, 24, or 25 hours when civil clocks shift. The same instant can fall on two different dates in two different places; mapping between dates and instants therefore always requires a declared time zone.

## Calendar period
The frequency of a regular event, expressed only in terms of years, days, months, weeks, or quarters. Never in temporal slices smaller than a single day. These are always relative to a specific calendar.

## Staged entry
A record in `stage.staged_entry` representing one economic event held in the staging area. A staged entry is a draft journal entry: it carries the same header-level fields (date, description, source) and is composed of staged lines that mirror journal entry lines. A staged entry becomes a journal entry only when batch-posted through the domain model. Until then it exists outside the ledger and does not affect balances.

## Staged line
A record in `stage.staged_line` representing one future journal entry line. A staged line belongs to exactly one staged entry and carries an amount, direction (entry_type), and an account assignment that may be null until classification or manual review fills it in.

## Postable (staged entry)
A staged entry whose status is `'classified'` or `'reviewed'` and whose every staged line has a non-null account_id. Only postable entries are eligible for shadow post or batch post.

## Interface
The set of features, functions, services, windows, or reports that actors outside the system will trigger or consume.

## Actors
Humans or systems that interact with the system via the interface layer. Note for any scheduled activities, "Time" may be conceived of as an actor.

## Interface layer
The application components in this system dedicated to the Interfaces (CLI applications, web pages, mobile apps, APIs, request routers, etc.)

## Application layer
The application components in this system dedicated to business logic (class libraries, domain types, orchestration modules, etc.)

## Persistence layer
The application components either within or outside of the SonOfLeo solution responsible for storing information about this system, its records, or its operation (database engine, schemas, logging components, etc.)