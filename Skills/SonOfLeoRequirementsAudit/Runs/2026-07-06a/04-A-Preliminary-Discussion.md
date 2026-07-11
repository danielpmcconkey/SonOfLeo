# 04-A Preliminary Discussion

2026-07-06

## What's this?

Our real-time discussion about all of phase 3 was in batch that was too difficult for me to separate out into their individual domains, so I'm clubbing it together here.

## Discussion

**[19:53:48 EDT] Dan:**
actually, what's phase 4?

---

**[19:54:04 EDT] Hobson:**
The 5-lens expert panel:

1. **Customer** — Hobson-as-comptroller judging whether the CLI can actually run your Saturday routine (near), whether the ledger docks cleanly with a future staging domain (mid), and whether it captures what the retirement engine will need (far)
2. **GAAP** — accounting soundness, especially trial-balance and period-close readiness given those are next on your dev horizon
3. **F#/DDD** — idiom review, teaching-oriented (since you're using this project to learn to think functionally)
4. **Architecture** — corner-painting, structural decisions that will hurt when staging/reporting/close arrive
5. **AI-maintainability** — whether BD can safely take over coding without the context that lives in your head

These are the "step back and think about the big picture" agents, not the line-by-line annotation checks you just went through. They're the ones most likely to surface design issues worth addressing before you start trial balance.

---

**[19:54:33 EDT] Dan:**
are you able to fire off BG agents as the Fable 5 model?

---

**[19:54:42 EDT] Hobson:**
Yes — the Agent tool has a `model` parameter. I can pass `"fable"` to spawn them on Fable 5. Want me to fire off Phase 4 on Fable now and you review the results tomorrow?

---

**[19:57:31 EDT] Dan:**
Yes, but not in parallel. Do it in series in this order: 3,4,1,2,5 . For posterity, my current session is 7% used, my weekly "all models" limit is 40% used. my weekly Fable limit is 43% used. It's 19:57 EDT. Go

---

**[19:58:17 EDT] Dan:**
Yes, but not in parallel. Do it in series in this order: 3,4,1,2,5 . For posterity, my current session is 7% used, my weekly "all models" limit is 40% used. my weekly Fable limit is 43% used. It's 19:57 EDT. Go. Pause between each so I can re-measure

---


## Dan note after the fact

