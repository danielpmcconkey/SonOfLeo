# Decision: Account ID → UUID

## Date: 2026-05-31

## The crossroads

LeoBloom uses `serial int` for `account.id` (and likely other PKs). This
means IDs are DB-assigned on INSERT. In F#, that creates a gap: you can't
construct a complete `Account` value until after persistence, because the
ID doesn't exist yet.

UUID eliminates this — generate client-side, construct the full type before
the DB round-trip. No half-built entities, no `option` pollution, no
"creation input vs persisted entity" split.

## Decision lean

Switch to UUID for SonOfLeo. Accepted consequences:

1. **SonOfLeo gets its own database.** The shared `leobloom_dev` DB stays
   intact for the elder. SonOfLeo develops against a fresh DB with the new
   schema.
2. **LeoBloom prod continues on int IDs.** No migration until SonOfLeo is
   feature-complete and replaces LeoBloom entirely.
3. **When that day comes, a migration converts prod.** Int → UUID, all FKs
   follow. This is future work — not trivial, but bounded and one-time.
4. **Charter amendment:** "no schema redesign" is overridden for this
   specific change. Dan sits above the charter.

## Why it's worth it

- Eliminates an entire class of modeling awkwardness at the type level.
- Client-generated IDs are better for testing (deterministic, no DB
  dependency for constructing test values).
- Aligns with the "no illegal data states" principle — an `Account` is
  always fully formed or it doesn't exist.

## Not-now work

- Stand up a SonOfLeo-specific dev database
- Decide if UUID applies to all entity PKs or just `account`
- Actual implementation of the type + schema
