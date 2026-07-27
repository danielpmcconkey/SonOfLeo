# A Check Verdict Is Evidence, Not Truth

**Source:** Sessions 2026-07-25 and 2026-07-26 — `check-format` has now produced both a
false PASS and a false FAIL.

`Checks/` scripts are grep-grade tools by design (see `HobsonsNotes/phase3-architecture.md`
§2). They are right nearly always and wrong occasionally, in both directions. Confirm what
a verdict means before you act on it.

## What works

- **On a FAIL: reproduce it before changing anything.** Re-run the single failing check.
  Confirm the named files are actually the ones you are committing, and that the violation
  is real (e.g. run `fantomas` on the file and see whether it rewrites it).
- **On a FAIL you cannot reproduce: record it and proceed.** An unreproducible verdict is
  itself a finding about the guardrail; it belongs in this catalog, not in silence.
- **On a PASS: remember what was inspected.** See
  `checks-read-the-tree-not-the-commit.md`.

## What doesn't

- `--no-verify` to silence a check you have not understood. It is the correct tool for a
  knowingly-broken WIP commit (`8798cbb` used it honestly, saying so in the message). It
  is the wrong tool for "the check is being annoying," and reaching for it reflexively is
  how the false-PASS defect survived as long as it did.
- **Editing files you were not committing in order to appease a check.** If the gate names
  someone else's in-flight work, formatting it "to get green" rewrites their buffer under
  them. Reproduce first; if the finding is real, tell the owner.

## Known flake: check-format false FAIL

2026-07-26, twice within ten minutes, on notes-only commits touching no F# at all:

| Attempt | Files named | Reality |
|---|---|---|
| 1 | `ExecuteScalar.fs`, `ExecuteReader.fs` | identical to HEAD; `fantomas` reported "unchanged" |
| 2 | `_TestDataStage.fs` | identical to HEAD, untouched for 7 hours |

In every case the named file passed `fantomas --check` both individually and in the
directory form moments later, with nothing altered on disk.

**What the evidence supports:** both failures occurred when `run-all.sh` ran *from the
pre-commit hook*. Fourteen consecutive direct runs — eight of `fantomas --check Src
Tests`, six of `bash Checks/run-all.sh --quick` — all passed. The runner is sequential,
so contention between checks is not the explanation.

**What it does not support:** any specific cause. Undiagnosed. Do not repeat a guess as
though it were a finding.

**Current status (2026-07-26): the pre-commit hook is switched off** by Dan's decision,
precisely so this flake cannot refuse a commit that needs making.
`Checks/install-hooks.sh` now requires `--force`. The checks themselves are unaffected
and `bash Checks/run-all.sh` remains mandatory before presenting work. Reinstate the hook
when the flake is diagnosed.

**If you do have the hook installed: retry the commit once.** If it passes on the second
attempt with nothing changed, you have met this flake; note it and carry on rather than
investigating at the moment you are trying to commit something unrelated.

Both tempting responses were wrong: `--no-verify` would have masked a real defect if one
existed, and running `fantomas` over the named files would have rewritten Dan's in-flight
work to fix a violation that did not exist.
