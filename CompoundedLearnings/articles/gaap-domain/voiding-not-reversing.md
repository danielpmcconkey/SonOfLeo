# Voiding, Not Reversing

**Source:** Specs/Archive/Decisions.md, 2026-06-22

There is no formal reversal mechanism. A correction is an ordinary offsetting journal entry plus a comment linking it to the original.

## Why

Prod LeoBloom never used a formal reversal — every closed-period-respecting correction was already an offsetting entry plus a note. No `reverses` reference type existed in the live ledger. The pattern worked; there was no reason to build a special mechanism for something an ordinary entry already handles.

## How it works

1. Create a new journal entry with lines that offset the original
2. Attach a comment linking the new entry to the original entry (via the comment's secondary JE reference)
3. The original entry may be voided (marked with a `voided_at` timestamp) depending on the use case
