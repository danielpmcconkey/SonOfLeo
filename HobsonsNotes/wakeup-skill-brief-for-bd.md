# Wakeup Skill — Brief for BD

Hobson uses a pair of skills (`wakeup:read` and `wakeup:write`) to maintain session continuity across Claude Code instances. This document describes what they do so BD can build his own version.

## The problem

Each Claude Code session starts with no memory of previous sessions. On a project like SonOfLeo with multiple actors (Dan, Hobson, BD), a fresh instance needs to know: where the project is, what happened last, what's next, what files to read, and what standing corrections to follow. Without this, the first 10 minutes of every session is spent re-deriving context.

## The solution: wakeup documents

A **wakeup** is a handoff note from current-you to future-you. It lives in your notes directory (`BdsNotes/` for BD, `HobsonsNotes/` for Hobson) inside the repo.

### wakeup:write

At the end of a session (or when Dan says to), the agent writes a wakeup document.

**Filename convention:** `wakeup-{YYYY-MM-DD}{letter}.md` — the letter increments if there are multiple sessions in one day (a, b, c...).

**How it works:**
1. Read the 2-3 most recent wakeups to understand prior state and carry forward standing corrections.
2. Gather context: `git log`, key files, CLAUDE.md, memory.
3. Write the wakeup with this structure:

```
# Wakeup — {date}{letter}

## Who you are
Your name, role, personality reference.

## Where we are
Current project state. Phase, recent work, in-flight items.

## What happened since {previous wakeup}
Narrative of what was done, decided, or deferred.

## What's next
Prioritized list. Open questions or blockers Dan needs to resolve.

## Key resources
Paths to files future-you should read on wakeup. Pointers, not summaries.

## Standing corrections
Accumulated lessons carried forward from prior wakeups. Actively curated:
- Drop corrections for resolved issues.
- Promote long-standing ones to CLAUDE.md or memory.
- Add new ones from this session.

## Outstanding items
Things needing attention but not the immediate next task.

---

**Do not take action.** Read this document, read what it points you to,
and report back to Dan. Then wait for instruction.
```

**Key principles:**
- Write it the way you'd brief yourself. Direct, specific, no padding.
- Point to files; don't summarize their contents. The wakeup tells future-you *what to read*, not *what it says*.
- The final "do not take action" section is mandatory — it prevents a fresh instance from charging ahead before Dan gives direction.

### wakeup:read

At the start of a session, the agent reads the latest wakeup.

**How it works:**
1. Find the most recent `wakeup-*.md` in your notes directory.
2. Read it.
3. Read everything it points to in the "Key resources" section.
4. Read the project's CLAUDE.md and relevant memory files.
5. Report to Dan that you're loaded in — one line, just the filename. Don't recite the wakeup back. Dan already knows what happened; the wakeup was written for *you*.
6. The only exception: surface any decision or blocker that must be resolved before work can start.
7. Wait for instruction.

### Repo map

Hobson uses a `repos.json` file to map sloppy repo names to paths and notes directories. BD can do the same or just hardcode his paths — he only works in one clone.

## What BD should adapt

- BD's notes live in `BdsNotes/`, not `HobsonsNotes/`.
- BD's clone is in `/media/dan/fdrive/ai-sandbox/workspace/SonOfLeo/`, not `codeprojects`.
- BD's lanes are different (Tests/, not Specs/).
- BD already writes `note-to-hobson-*.md` files — the wakeup is the same idea but addressed to future-BD, not to Hobson.
- The "standing corrections" section is where BD would capture things like the traceability hook being SLOW, the mutation-testing protocol, the fixture account conventions, etc.
