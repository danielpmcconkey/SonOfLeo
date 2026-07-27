# Checks Read the Working Tree; Git Records the Index

**Source:** Session 2026-07-25 — commit `da0dc0f` went in unformatted with the pre-commit
hook reporting PASS. Fixed in `85906a5`.

Every check in `Checks/` inspects files as they exist on disk. A commit records the
index. Those are different snapshots, and any gate that assumes they are the same will
eventually bless content it never examined.

## What works

- A pre-commit gate must first prove tree and index agree for every file being
  committed, then run the checks. The hook body in `Checks/install-hooks.sh` does this by
  refusing when any staged file also carries unstaged edits. **Note: the hook is
  currently uninstalled** (see `a-check-verdict-is-evidence-not-truth.md`), so nothing
  enforces this today — the reasoning survives for whoever reinstates it.
- **Detect and refuse — never silently reconcile.** Guessing which version the author
  meant (re-staging, or checking the index instead) is how a gate becomes untrustworthy.
  Name the offending files and stop.
- Hook bodies live in `.git/hooks/`, which is not versioned. After any hook change,
  everyone re-runs `bash Checks/install-hooks.sh`. A hook fix nobody installed is not a
  fix.

## What doesn't

- Writing a check that reads `Src/**/*.fs` and assuming that is what's being committed.
- The sequence `git add -A` → `fantomas` → `git commit`. The formatter rewrites the tree
  after staging; the hook validates the formatted tree while git records the unformatted
  index. Nine green ticks, unformatted commit.

## Example

`da0dc0f` ("refactoring transactions is complete") was committed after exactly that
sequence. `check-format` passed against the reformatted working tree. The commit
contained the pre-format bytes, and `main` carried unformatted F# until a later
`fantomas Src Tests` pass cleaned it up.

The general shape: **a gate is only as good as its knowledge of what it is gating.** Any
new check that reads the filesystem inherits this gap, and the hook — not the check — is
where it gets closed.
