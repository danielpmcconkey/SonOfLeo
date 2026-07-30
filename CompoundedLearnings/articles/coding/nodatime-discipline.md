# NodaTime Discipline

**Source:** the retired Conventions/Temporal.md (removed 2026-07-30); Specs/Archive/Decisions.md, 2026-06-11

The application layer must use NodaTime's `Instant` type for instants and `LocalDate` for calendar dates (see `Specs/Definitions.md` for the distinction between these two temporal concepts). No instantiation of standard .NET `DateTime`, `DateTimeOffset`, or `DateOnly` objects is allowed, except at the I/O edge where external libraries require it — and those must be kept as close to the edge as practical.

## Why NodaTime
- Makes the temporal model compiler-enforced rather than review-enforced
- `Npgsql.NodaTime` plugin maps `timestamptz` to `Instant` end-to-end, avoiding the I/O edge entirely for persistence

## What works
- `Instant` for all instants, `LocalDate` for all calendar dates in the application layer
- Confining raw `DateTime`/`DateTimeOffset` to I/O boundaries that require them
- Using `Npgsql.NodaTime` so persistence reads/writes go through NodaTime types directly

## What doesn't
- `DateTime.Now`, `DateTimeOffset.UtcNow`, or any .NET temporal constructor in domain code
- Passing raw `DateTime` values through the domain layer "because it's easier"
