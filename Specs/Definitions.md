# Definitions

Terms with a SonOfLeo-specific meaning, defined once, above the individual domains so that
behavioral specs, conventions, and decisions can all lean on the same words. Admission
rule: a term earns an entry only when its meaning changes which requirements apply or how
they are verified. Plain English stays in the dictionary.

- **Entity** — a record type the system creates or mutates at runtime on behalf of the
  user. Two litmus questions for any table: (1) does any user action ever insert or update
  a row? Yes → entity. (2) Could the table's entire contents be regenerated from spec and
  code alone? Yes → lookup, not an entity. System-wide requirements (REQ-SYS-*) bind to
  entities; lookup tables and infrastructure metadata (e.g., migration history) are out of
  scope. Classification is by behavior, not shape: a lookup-shaped table becomes an entity
  the moment users can extend it at runtime. (Approved: Dan, 2026-06-11)

- **Instant** — a globally agreed-upon point on the timeline, independent of the viewer's
  geography or season. An instant carries no calendar date, no wall-clock face, and no
  offset costume; those are renderings of an instant, not the instant itself. All temporal
  values in this system are instants — there are no date-only or naive local-time values.
  (Approved: Dan, 2026-06-11)

- **System clock** — the system's single source of the current instant, read at the time
  of an operation. Says nothing about presentation: rendering an instant for a human
  (zone, season, format) is a consumer concern, never a system one. (Approved: Dan,
  2026-06-11)

- **Public surface** — the set of functions a consumer of a module can call; the API the
  orchestration and consumption layers see. Requirements phrased as "must not provide a
  user interface for X" constrain the public surface — no public function may expose X —
  not a visual UI, which does not exist. (PENDING — candidate alternative: reword
  REQ-AC-4.22 and REQ-AC-5.1 to say "public means" and drop this entry.)
