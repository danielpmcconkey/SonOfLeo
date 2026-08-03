# Debug/Release Access Rules

**Source:** the retired Conventions/BuildAndEnvironment.md (removed 2026-07-30)

Build configuration determines which database an executable may access. This is a hard boundary, not a suggestion.

## Rules

- Any executable in **debug** mode may NEVER access the production database — read or write
- Only executables in **release** mode may access production — read or write

## Agent restrictions

No agent is ever allowed to:

- Manipulate application config files (appsettings, launchSettings, etc.)
- Manipulate environment variables
- Modify DAL functions that are part of the connection string chain

These boundaries exist so that an agent cannot accidentally (or through hallucination) bridge the gap between debug and production. Dan configures these by hand.

## Backstops

Four independent barriers prevent debug-mode or agent-driven code from reaching production data. Auditors should re-verify this posture each run against this baseline.

| # | Backstop | Mechanism | Attested |
|---|----------|-----------|----------|
| 1 | Test appsettings point only at dev/test | `Tests/Tests.Integrated/appsettings*.json` reference `SONOFLEO_TEST_CONNSTR`; CLI `appsettings.Development.json` references `SONOFLEO_DEV_CONNSTR`. Neither names the prod env var. | 2026-08-03 (Hobson, verified by reading files) |
| 2 | Release-config gate | `SonOfLeoCli.fsproj` copies `appsettings.Development.json` in Debug and `appsettings.Production.json` in Release. Build configuration selects the env var name; there is no runtime switch. | 2026-08-03 (Hobson, verified by reading fsproj) |
| 3 | No prod password in the container | BD's sandbox container does not have the production connection string or its credentials. | 2026-08-02 (Dan, verbal attestation) |
| 4 | Network block on prod | The production database is not reachable from the development/agent network. | 2026-08-02 (Dan, verbal attestation) |
