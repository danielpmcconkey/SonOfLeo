# FMEA — Hobson's Environment Isolation Plan

**Date:** 2026-06-14
**Reviewer:** BD
**Source:** HobsonsNotes/environment-isolation-plan-2026-06-14.md

---

## FM-1: "Forgot the .runsettings" — tests silently hit dev DB

**Severity: HIGH | Likelihood: HIGH | Detection: LOW**

Test projects build as Debug. The `#if DEBUG` default in DAL.fs resolves to
"Development." The *only* thing routing tests to `sonofleo_test` is the
`.runsettings` file injecting `SONOFLEO_ENV=Test`. Every test invocation —
Rider, `dotnet test`, BD in the container — must remember to pass that
settings file.

Run `dotnet test` without `--settings`? Tests execute against the dev
database. No error. No warning. Just wrong data.

pg_hba.conf won't catch this. `leobloom_dev` connecting to `sonofleo_dev` is
a perfectly valid connection. The lockdown is role-to-database, not
"tests must use the test DB."

This is the plan's biggest vulnerability. The build-config-as-selector idea
is elegant for the CLI (Debug=Dev, Release=Prod) but breaks down for tests.
Tests are Debug builds that need a *third* environment, so they require a
side-channel override, which reintroduces exactly the fragility the plan is
trying to eliminate.

---

## FM-2: Rider may not auto-discover .runsettings

**Severity: HIGH | Likelihood: MEDIUM | Detection: HIGH**

The plan claims "Rider picks this up automatically when it's at the solution
root." This is optimistic. Rider *can* use `.runsettings`, but typically
requires explicit configuration in Run/Debug Configurations → Test Settings.
If Dan has to manually configure every run config, it's fragile. And if it's
not configured, you're back to FM-1.

---

## FM-3: `environment.d` might not work on Cinnamon/Mint

**Severity: HIGH | Likelihood: MEDIUM | Detection: HIGH**

`~/.config/environment.d/` is a systemd user session mechanism. Whether
Cinnamon on Linux Mint actually sources this for graphical apps depends on
whether the display manager goes through `systemd --user` import. This is
the *exact same class of problem* as the current `.bashrc` issue — graphical
session env var visibility.

If `environment.d` doesn't work, the plan's entire solution to "Rider can't
see env vars" is dead, and we're back to square one.

**Recommendation:** Before touching any code, set a dummy var in
`environment.d`, logout/login, and check if Rider can see it. Five minutes
of verification before hours of implementation.

---

## FM-4: Passwords duplicated in .bashrc AND environment.d

**Severity: MEDIUM | Likelihood: HIGH | Detection: LOW**

Three connection strings × two config files = six places to update on
password rotation. Forget one, and terminal sessions silently use different
credentials than Rider.

The whole point of this plan is reducing misconfiguration surface. This
doubles it.

**Recommendation:** Single source of truth. Put the vars in `environment.d`
only and source that file from `.bashrc` (the format is compatible with
shell `export` if you prefix appropriately), or accept that `.bashrc`
sources a shared file. Don't maintain two independent copies.

---

## FM-5: The `claude` user still exists

**Severity: MEDIUM | Likelihood: HIGH | Detection: LOW**

The plan reassigns objects from `claude` to `leobloom_dev` but never
addresses `claude`'s continued existence. BD uses `claude` for other projects
in this container (`householdbudget`, `atc`, `leobloom_dev` DB). If `claude`
retains connect privileges on `sonofleo_dev`, the pg_hba lockdown is theater
— any code running as `claude` bypasses the role isolation entirely.

**Recommendation:** Either explicitly
`REVOKE CONNECT ON DATABASE sonofleo_dev FROM claude`, or document that
`claude` is a known backdoor and why that's acceptable.

---

## FM-6: New databases have no schemas

**Severity: MEDIUM | Likelihood: HIGH | Detection: HIGH**

`sonofleo_test` and `sonofleo_prod` are created empty. The connection strings
reference `Search Path=ledger,ops,portfolio,public`. The plan says "schemas
come from migrations" but doesn't specify how or when.

If Dan creates the databases and then runs verification (Phase 5) without
running migrations first, everything fails and the verification step is
useless.

**Recommendation:** Add an explicit Phase 1.8: "Run migrations against
`sonofleo_test` and `sonofleo_prod`."

---

## FM-7: Plan is stale on test project structure

**Severity: LOW | Likelihood: CERTAIN | Detection: HIGH**

Phase 4.1 says to copy `appsettings.Test.json` into `Tests.Model`,
`Tests.Ledger`, and `Tests.Integrated`. The test project structure has since
been reorganized to `Tests.Isolated` (no DB) and `Tests.Integrated` (DB).
The plan references projects that are being deprecated.

Not a risk — just means the plan needs a revision pass before execution.

---

## FM-8: No guardrail if someone puts a connection string in appsettings

**Severity: LOW | Likelihood: LOW | Detection: LOW**

The appsettings value is an env var *name*, not a connection string. If
someone puts an actual connection string there, the code calls
`Environment.GetEnvironmentVariable("Host=localhost;Port=5432;...")`, gets
null, and throws a confusing error.

**Recommendation:** Add a sanity check — if the value contains `=` or `;`,
it's probably a connection string, not an env var name. Make the error
self-diagnosing.

---

## FM-9: Asymmetric build guard

**Severity: LOW | Likelihood: LOW | Detection: MEDIUM**

The `#if DEBUG` guard blocks Debug → Production but doesn't block
Release → Test. A `SONOFLEO_ENV=Test` on a Release build would let prod
code hit the test database. Unlikely scenario, but the guard is
one-directional.

**Recommendation:** If you're going to have a guard, make it symmetric.
Release builds should only connect to Production.

---

## Structural concern

The build-config-as-environment-selector concept is clean for the CLI binary
where Debug/Release maps 1:1 to Dev/Prod. It falls apart for tests, which
are Debug builds that need to reach a third environment. The plan patches
this with `.runsettings`, but that reintroduces a "remember to configure
this" dependency — the exact category of problem this plan exists to solve.

The strongest parts of the plan are the pg_hba lockdown and the
secrets-out-of-repo pattern. The weakest part is the test environment
routing.
