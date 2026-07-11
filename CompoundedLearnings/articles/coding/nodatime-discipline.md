# NodaTime Discipline

**Source:** Temporal.md, Application Layer Tooling; Decisions.md 2026-06-11

The application layer must use NodaTime's `Instant` type for instants. No instantiation of standard .NET `DateTime` or `DateTimeOffset` objects is allowed, except at the I/O edge where external libraries require it — and those must be kept as close to the edge as practical.

## Why NodaTime
- Makes the temporal model compiler-enforced rather than review-enforced
- `Npgsql.NodaTime` plugin maps `timestamptz` to `Instant` end-to-end, avoiding the I/O edge entirely for persistence

## What works
- `Instant` for all instants in the application layer
- Confining raw `DateTime`/`DateTimeOffset` to I/O boundaries that require them
- Using `Npgsql.NodaTime` so persistence reads/writes go through NodaTime types directly

## What doesn't
- `DateTime.Now`, `DateTimeOffset.UtcNow`, or any .NET temporal constructor in domain code
- Passing raw `DateTime` values through the domain layer "because it's easier"
