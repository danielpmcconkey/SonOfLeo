# Temporal Persistence

**Source:** the retired Conventions/Temporal.md (removed 2026-07-30)

The persistence layer stores all `Instant` values as `timestamptz` and all calendar dates as Postgres `date`. No exceptions. The database is never the originator of temporal values.

## Rules

- `Instant` -> `timestamptz`. Always.
- Calendar dates -> Postgres `date`. Always.
- No `now()` in defaults, triggers, or stored procedures — the application layer is the sole originator
- Required (non-nullable) temporal columns carry no defaults; a write that omits the value is rejected, never filled in by the database
- The `Npgsql.NodaTime` plugin handles the mapping so `timestamptz` round-trips as `Instant` without manual conversion

## What doesn't
- `timestamp without time zone` for instants
- Database-side defaults like `DEFAULT now()` or `DEFAULT CURRENT_TIMESTAMP`
- Nullable temporal columns as a substitute for explicit sentinel values
