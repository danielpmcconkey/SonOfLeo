# Temporal Arithmetic

**Source:** the retired Conventions/Temporal.md (removed 2026-07-30). Instant-to-date section rewritten 2026-10-03 for REQ-SYS-3.4 and REQ-SYS-7.1 (audit 2026-10-03a, #292).

Instants and dates (see `Specs/Definitions.md`, Instant and Date) are separate algebras with separate arithmetic rules. Do not mix them.

## Instants

- Arithmetic may use hours, minutes, or seconds
- Arithmetic may **never** use years or months — those require calendar conventions
- Arithmetic using days is **discouraged** — prefer hours, minutes, or seconds where practical
- Precision: the system must reconstitute any instant to seconds precision at minimum. An
  instant arriving from an external system that cannot meet that standard is rejected as
  invalid — unless the interface bringing it in is a component of this system, in which case
  that middleware converts it under its own requirements

## Dates

- Arithmetic may use years, months, or days — calendar units only
- Calendar periods are discriminated unions whose contents may vary by domain
- Calendar period arithmetic always uses standard NodaTime library functions

## Instant-to-date conversion

- Convert in the one configured time zone, `LocalizedTimeZone` in appsettings (REQ-SYS-7.1). `Clock.timeZoneLocal` holds the only binding; never name a zone in code. Example, with the zone set to New York: 2026-07-06 02:00 UTC -> 2026-07-05, because it was still July 5, 10PM there
- An operation's "current date" is the calendar date of its initiation instant (REQ-SYS-3.4): `context |> Context.getInitiationInstant |> Calendar.dateFromInstant`. One operation, one moment — reads included
- Never take a fresh `Calendar.today()` or `Clock.now()` in operation code. `Checks/check-clock.sh` fails on any outside `Clock.fs`, `Calendar.fs` and `AuditEnvelope.fs`
- Conversion should be **rare** — very few, very deliberate points in the code. If you're unsure whether you need it, stop and ask
- Centralize through `Calendar.dateFromInstant`. Don't scatter conversion logic
