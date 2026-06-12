# Temporal Values

- **NodaTime is the temporal vocabulary.** Domain and persistence code represent moments
  as NodaTime `Instant`. `DateTime` and `DateTimeOffset` do not appear in domain code —
  the BCL types let localized "now," naive wall-clocks, and silent `.Date` truncation
  compile; NodaTime makes them unrepresentable.
- **One clock, injected.** Code never reads the clock inline (`SystemClock.Instance`
  directly, `DateTimeOffset.UtcNow`, `.Now`). The clock arrives as a NodaTime `IClock`
  dependency, which is what makes timestamp requirements (REQ-SYS-3.2, REQ-SYS-3.3)
  testable at all.
- **Truncate to microseconds at capture.** The clock wrapper truncates instants to
  microsecond precision before anything sees them. Postgres stores microseconds; capturing
  finer would make round-trips lossy and REQ-SYS-5.1 ("perfectly reconstituted") false.
- **Postgres: `timestamptz` only.** No `timestamp`, `date`, `time`, or `timetz` columns,
  ever — the first is a naive wall-clock, the rest are date-only/time-only values the
  system has banned. `timestamptz` stores exactly the instant, normalized; the original
  offset is deliberately discarded (localization is a presentation concern).
- **No database-side clock.** No `DEFAULT now()` on temporal columns. The injected app
  clock is the only clock; a missing timestamp should fail loudly, not get backfilled by
  the database's opinion.
- **Connections pin `Timezone=UTC`** (Npgsql connection string) so session rendering is
  deterministic, and use the `Npgsql.NodaTime` plugin so `timestamptz` maps to `Instant`
  end-to-end.
- **Period ranges use `tstzrange`** (maps to NodaTime `Interval`), half-open `[)`, with a
  gist exclusion constraint where overlap must be impossible. Don't model ranges as
  begin/end column pairs. (Forward-looking: fiscal periods.)
