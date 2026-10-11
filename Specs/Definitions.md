# Definitions

Terms with a SonOfLeo-specific meaning, defined once, above the individual domains so that behavioral specs, conventions, and decisions can all lean on the same words. Admission rule: a term earns an entry only when its meaning changes which requirements apply or how they are verified. Plain English stays in the dictionary.

## The system
Any technology component whose source code or whose configuration exists in the SonOfLeo repository. Includes any binaries or CIL produced by building this solution and any database structure or behaviors defined in this repository. 

## Money (as a variety of number)
An amount denominated purely in currency (USD). Money is the only concept that sums meaningfully: totals, balances, and ledger entries are all sums of Money. Examples of real-world concepts that the system should define as Money:
- Dan paid 600.06 USD at the liquor store (the total accumulated transaction)
- Dan's checking account has a balance of -17.40 USD

Note: the system's Money type (specified in Money.md) is currently the only type in this system that fits within the Money definition. However, in future, we will be building out a Monte Carlo simulation that will have its own type that fits within the Money definition. In short, this definition encompasses the system's Money type *and* future "Money" types but rules governing the system's Money type do not all apply to all types defined under the "Money" definition.

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

## Pre-ledger date
A Calendar Date earlier than the start date of the earliest fiscal period: a date the ledger holds nothing for. Net worth on a pre-ledger date takes ledger accounts' balances from Pre-ledger Balances (Positions §15) instead of journal entries (Reporting REQ-RPT-8.7). When no fiscal period exists, no date is a pre-ledger date. (2026-10-10)

## Staged entry
A record in `ingestion.staged_entry` representing one economic event held in the staging area. A staged entry is a draft journal entry: it carries the same header-level fields (date, description, source) and is composed of staged lines that mirror journal entry lines. A staged entry becomes a journal entry only when batch-posted through the domain model. Until then it exists outside the ledger and does not affect balances. A staged entry is **not an entity** per this document's Entity definition — it is a transient pipeline artifact with a full audit trail (`ingestion.staged_entry_audit`) that records every status transition. Entity-level policies (e.g. REQ-SYS-3.1 timestamps) do not apply.

## Staged line
A record in `ingestion.staged_entry_line` representing one future journal entry line. A staged line belongs to exactly one staged entry and carries an amount, direction (line_type), and an account that may be null until classification or manual review fills it in. Like its parent staged entry, a staged line is **not an entity** — entity-level policies do not apply.

## Insert-only log records
A record the system appends as a historical log and never updates: a classification match row (`classification.rule_match`, REQ-CR-8.4) and a staged entry status transition (`ingestion.staged_entry_audit`). An insert-only log record is **not an entity** per this document's Entity definition, although a user action inserts it: it has no life after insertion, so a "modified at" would always equal "created at". Entity-level policies (e.g. REQ-SYS-3.1 timestamps) do not apply. (2026-10-03)

## Component rows
A record that exists only as a part of its parent: an account snapshot line (`positions.account_snapshot_line`, part of an account snapshot), an owner set (`positions.investment_account_owner`, `positions.property_owner`) and a property's ledger-account sets (`positions.property_asset_account`, `positions.property_mortgage_account`, part of a property). Component rows are written and removed only together with their parent and are never updated in place; a change replaces them. A component row is **not an entity** per this document's Entity definition — REQ-SYS-3.1 does not apply, and the parent's timestamps cover it. (2026-10-05)

## Postable (staged entry)
A staged entry whose status is `'Classified'` or `'Reviewed'`. Only postable entries are eligible for shadow post or batch post. The posting process validates that every staged line has a non-null account. A manual update can make an entry postable while a line's account is still null (REQ-STG-4.4); such an entry fails shadow post, review and posting loudly (REQ-STG-9.4), and is never silently excluded.

## Person
A human with a relationship to a financial account or property the system tracks — an owner, for example. A Person is a record the system holds *about* someone (Person.md). It is not a User. (2026-10-04)

## User
An actor operating this system (see Actors). Being a User grants no relationship to any account, and being a Person grants no access to the system. The two are never treated as one, even when they are the same human. (2026-10-04)

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