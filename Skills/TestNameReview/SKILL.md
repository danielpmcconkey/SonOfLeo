---
name: SonOfLeo:TestNameReview
description: >
  Grade draft test names against the requirements they claim to cover, and find requirement
  clauses that no name covers. Step 6 of the slice loop, run after drafting names and before
  showing them to Dan. Triggers on "check my test names", "grade these names", "review the
  test names", or finishing a batch of stubs.
---

# SonOfLeo TestNameReview

Step 6 of the loop in the root `README.md`. Names are drafted from the spec (step 5), graded
here, then approved by Dan (step 7). Nothing is written until they survive both.

## Why this is a separate agent and not a checklist

By the time you have written a name you know what you meant by it, and that knowledge is
exactly what stops you noticing that it doesn't say so. A checklist you apply to your own
names is the thing that already failed: an August 2026 review of a green suite found three
tests with no assertion at all, all of them shipped after a self-review that passed.

So the grading is done by an agent that has never seen your reasoning, cannot see the Src,
and has no stake in the names being good.

## What you send it

Exactly two things. Adding more is not helpful — it is the failure mode.

1. The **verbatim text** of the behavioral requirements the names claim to cover. Not your
   summary of them.
2. The **draft names**, as a plain list, with no commentary, no ordering by confidence, and
   no note about which ones you are sure of.

Do **not** send: the Src, your rationale, the hand-off document, or which names you already
suspect. Any of those turns an independent read into an agreeing one.

## Running it

Spawn one general-purpose agent with the prompt in `references/grader-prompt.md`, with the
two inputs appended. One pass. It is cheap; run it on every batch.

## What to do with the result

The score orders your attention; it does not gate anything. Work the list from the bottom.

For each weak name, either take the agent's proposed replacement, write a better one, or
decide it is right as it stands and say why — a deliberately toothless name is a legitimate
thing to have, and `REQ-STG-9.1 batch post happy path` is one that survived on purpose. What
you may not do is leave a low score unaddressed and silent.

For each uncovered clause, either draft the missing name or establish that the clause needs
no test. A clause with no name is the expensive miss: a hollow name reads wrong to a reviewer,
an absent one is invisible.

Then present names and the agent's findings to Dan together. He is approving the names, not
the grades.

## Iterating

When Dan's step 7 review catches something this agent should have caught, that is a gap in
the rubric. Add the case to the calibration section of `references/grader-prompt.md` with the
ruling, so the next run has it. The calibration is the part that improves; the guidelines are
mostly settled.
