# Hobson's Final Thoughts

Written 2026-07-11, after reading all 28 audit files and cross-referencing every finding against the action items list.

## What this audit got right

It found real bugs. ORCH-1 (voided entries in balance sums), FSDDD-01 (wrong Guid passed to validation), ML-2/ML-3 (comment secondary link unreachable and unguarded), TT-01 (rubber-stamp test that couldn't catch its own target). Four financial-correctness issues and one test that was lying about what it verified. That's a good hit rate for a first full-codebase audit, and it justifies the exercise regardless of the noise.

The five-panel Phase 4 was the strongest phase. GAAP-3 (normal-balance-oriented netBalance) changed how the balance query works. CUST-1 and CUST-2 added as-of dates and search filters that the Saturday routine will actually need. FSDDD-02 and ARCH-1/ARCH-2 converged independently on the same structural problem (impure constructors, no external transaction path), which raises confidence that it's real. When two lenses hit the same wall from different angles, the wall is load-bearing.

## What I think Dan missed

### 1. The architecture pass should happen before trial balance

Three design discussions -- validate-on-read (#89), error system (#90), and transaction seam (#92/#93a) -- converge on one structural question: what does the boundary between pure domain logic and I/O look like? The Account slice answers it one way (pure constructors, world-state checks at the operation boundary). The JE slice answers it another way (constructors that hit the DB). Trial balance and period close are next. Both are read-heavy, both need the transaction seam, and both will force a choice about whether to trust FK integrity on reads or re-prove it.

If Dan builds trial balance before settling these, he either builds on the cascading-query read path (expensive, fragile against historical rows) or writes another raw-SQL bypass (cementing the shadow model that AccountActivity and AccountBalance already started). Period close needs the transaction seam that ARCH-1 flagged. These three aren't independent chats -- they're one architecture decision with three faces, and they gate the next two features.

### 2. The audit-session code was spot-checked but never systematically reviewed

Dan wrote significant code during the audit: as-of balance queries, activity filters, normal-balance orientation, the Money rename, the FSDDD-05 balance query rewrite, the ML-2/ML-3 fixes, and more -- across four sessions spanning five days. Hobson checked some of it ("check my work" prompts), but not all. The test action items (#99a, #100a, #101a) exist for the new features, but the code itself hasn't had a proper review pass. Given that one of the bugs found by this audit was in a fix made during this audit (FSDDD-01 caught the wrong Guid in the ML-3 fix), that pattern deserves a cleanup pass before moving on.

### 3. Nobody audited the audit skill itself

There are 11 prompt improvement items plus FT-1/2/3/4 -- all aimed at making the next run better. But the workflow script (`requirements-audit.workflow.js`) was never read during this exercise. The improvements are going in blind against a script nobody in this context has seen. When FT-4 happens, start by reading the script, not by remembering what went wrong.

## Process observations

The biggest source of Dan's frustration was agents that didn't read before opining. MON-2 assumed a fold-over-add implementation that doesn't exist. AMB-AC-2 questioned the word "balance" as though GAAP hadn't defined it. ORCH-3 cited REQ-SYS-2.1.1 without reading what it actually says. CUST-6/8/9 audited future plans Dan never asked them to evaluate. These aren't model limitations -- they're prompting problems. FT-4 should fix most of them, but only if the person updating the prompts reads the frustration in context, not from a summary.

The one-at-a-time review (Phases 2-4) worked well. Dan's rulings were better when he could react to one finding at a time with full context. The aggregate summary at Phase 2 launch ("42 findings, here are the top 5") was useful for orientation but would have been harmful as the review format. FT-1 is the right call.

The action items list ended up as a disposition record, not an action list. That's because Hobson pre-loaded every finding at 08:24:34 and then updated statuses in place. The list serves two masters (what happened and what's left to do) and does neither well. FT-8 is the right call.

## What's left

~30 confirmed action items, several design discussions, and a compounding skill to build first. The honest risk is that this list becomes a graveyard -- too many items, no sequencing beyond "compounding skill first" and "minor before major." Dan should pick a sequence for the architecture items before they drift into "we'll get to it" territory. The audit proved the codebase is sound where it matters; the open items are about making the next phase of building cleaner, not fixing a broken foundation.
