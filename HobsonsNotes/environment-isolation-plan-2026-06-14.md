# SonOfLeo Environment Isolation Plan

**Date:** 2026-06-14
**Author:** Hobson
**Status:** Complete — all verifications pass (2026-06-14).
**FMEA:** BdsNotes/environment-isolation-fmea-2026-06-14.md

## Problem

Dan develops through Rider on the host, which is the production environment.
Running the app or tests from Rider hits the prod database. The current
environment selector (`LEOBLOOM_ENV`) is an env var that Rider can't see when
launched from a desktop shortcut (Cinnamon doesn't source `.bashrc` for
graphical apps in the way you'd expect).

Secondary issue: tests run against the dev database, which is semantically
wrong — dev is BD's workspace, not a test environment.

## Design

**The build configuration becomes the environment selector.** Debug =
Development, Release = Production. No env var needed for normal operation.

Each `appsettings.{env}.json` file contains the **name** of an env var. That
env var holds the **full connection string** (host, database, user, password —
everything that varies per environment and per user).

**Test projects route to the test DB structurally** — each test project's own
`appsettings.Development.json` contains `SONOFLEO_TEST_CONNSTR` (not
`SONOFLEO_DEV_CONNSTR`). Since tests build as Debug, the code loads
"Development" and finds the test project's appsettings, which points to the
test env var. No `.runsettings` override needed. (Fix for FMEA FM-1/FM-2.)

Three postgres roles, each structurally locked to one database via
`pg_hba.conf` reject rules. Even if every env var on the box is wrong, the
credentials can't authenticate against the wrong database.

### Target state

| Activity | Executable | Build config | Interface | DB server | Database | db_user | password |
|---|---|---|---|---|---|---|---|
| Dan developing | SonOfLeoCli | Debug | Rider | localhost | sonofleo_dev | leobloom_dev | *1 |
| BD developing | SonOfLeoCli | Debug | Bash | 172.18.0.1 | sonofleo_dev | leobloom_dev | *1 |
| Dan testing | Tests | Debug | Rider | localhost | sonofleo_test | leobloom_test | *2 |
| BD testing | Tests | Debug | Bash | 172.18.0.1 | sonofleo_test | leobloom_test | *2 |
| Hobson (prod) | SonOfLeoCli | Release | Bash | localhost | sonofleo_prod | leobloom_hobson | *3 |
| Migrations (dev/test) | manual/script | — | Bash | localhost | sonofleo_dev/test | leobloom_dev/test | *1/*2 |
| Migrations (prod) | manual/script | — | Bash | localhost | sonofleo_prod | leobloom_hobson | *3 |

### Env var flow

```
Build config (Debug/Release)
    |
    v
#if DEBUG -> "Development"
#else     -> "Production"
    |
    v
appsettings.Development.json   (each project has its OWN copy)
    |                           CLI's copy      -> "SONOFLEO_DEV_CONNSTR"
    |                           Test projects   -> "SONOFLEO_TEST_CONNSTR"
    v
Full connection string          (read from the named env var at runtime)
```

---

## Phase 0 — Validate env var delivery to Rider ✅ COMPLETE

**Findings (2026-06-14):**

- `~/.config/environment.d/` does NOT reach Rider. Cinnamon's desktop
  session doesn't propagate `environment.d` vars to graphical apps.
  `systemctl --user import-environment` + `dbus-send
  UpdateActivationEnvironment` also failed — Rider's native ELF launcher
  bypasses D-Bus activation.
- The desktop shortcut (`Rider.cinnamon-generated.desktop`) pointed at the
  ELF binary directly (`bin/rider`), not the shell wrapper (`bin/rider.sh`).
  Also pointed at an old version path (2024.3.6 vs actual 2025.1.3).
- **Solution:** Dan launches Rider from a terminal (`rider` command).
  `.bashrc` sources `~/.config/environment.d/sonofleo.conf`, terminal
  inherits the vars, Rider inherits from the terminal. Confirmed working
  with a smoke test var visible in both Rider's built-in terminal and
  `Environment.GetEnvironmentVariable` in F# code.
- Symlink created: `~/.local/bin/rider` →
  `/media/dan/edrive/JetBrains Rider-2025.1.3/bin/rider.sh`
- `.bashrc` updated: `set -a; . "$HOME/.config/environment.d/sonofleo.conf"; set +a`
- **No logout/login required.** New terminal sessions pick up the vars
  immediately. Rider inherits them when launched from a terminal.

---

## Phase 1 — Postgres (Dan, requires superuser)

Generate three passwords first:

```bash
openssl rand -base64 24  # run 3 times, one per role
```

### 1.1 Create roles

```sql
-- Connect as superuser (postgres)
CREATE ROLE leobloom_dev LOGIN PASSWORD '<password_1>';
CREATE ROLE leobloom_test LOGIN PASSWORD '<password_2>';
CREATE ROLE leobloom_hobson LOGIN PASSWORD '<password_3>';
```

### 1.2 Create databases

`sonofleo_dev` likely already exists — skip the CREATE if so.

```sql
-- CREATE DATABASE sonofleo_dev OWNER leobloom_dev;  -- skip if exists
CREATE DATABASE sonofleo_test OWNER leobloom_test;
CREATE DATABASE sonofleo_prod OWNER leobloom_hobson;
```

If `sonofleo_dev` already exists:

```sql
ALTER DATABASE sonofleo_dev OWNER TO leobloom_dev;
```

### 1.3 Revoke default public access

```sql
REVOKE ALL ON DATABASE sonofleo_dev FROM PUBLIC;
REVOKE ALL ON DATABASE sonofleo_test FROM PUBLIC;
REVOKE ALL ON DATABASE sonofleo_prod FROM PUBLIC;
```

### 1.4 Grant each role only its own database

```sql
GRANT ALL ON DATABASE sonofleo_dev TO leobloom_dev;
GRANT ALL ON DATABASE sonofleo_test TO leobloom_test;
GRANT ALL ON DATABASE sonofleo_prod TO leobloom_hobson;
```

### 1.5 Revoke claude's access to SonOfLeo databases (FMEA FM-5)

The `claude` role stays alive for other projects but must not have a
backdoor into the SonOfLeo databases.

```sql
REVOKE ALL ON DATABASE sonofleo_dev FROM claude;
REVOKE ALL ON DATABASE sonofleo_test FROM claude;
REVOKE ALL ON DATABASE sonofleo_prod FROM claude;
```

### 1.6 Reassign existing objects (if sonofleo_dev has tables owned by `claude`)

```sql
\c sonofleo_dev
REASSIGN OWNED BY claude TO leobloom_dev;
```

For `sonofleo_test` and `sonofleo_prod`, schemas come from migrations run
by the new owners. No reassignment needed.

### 1.7 pg_hba.conf — structural lockdown

Add these lines **above** any broad `all/all` rules in `pg_hba.conf`:

```
# SonOfLeo role lockdown — each role can only reach its own database
host sonofleo_dev   leobloom_dev     127.0.0.1/32   scram-sha-256
host sonofleo_dev   leobloom_dev     172.18.0.0/16  scram-sha-256
host sonofleo_test  leobloom_test    127.0.0.1/32   scram-sha-256
host sonofleo_test  leobloom_test    172.18.0.0/16  scram-sha-256
host sonofleo_prod  leobloom_hobson  127.0.0.1/32   scram-sha-256

# Deny these roles from any other database
host all            leobloom_dev     0.0.0.0/0      reject
host all            leobloom_test    0.0.0.0/0      reject
host all            leobloom_hobson  0.0.0.0/0      reject
```

Reload:

```bash
sudo systemctl reload postgresql
```

### 1.8 Verify role lockdown

```bash
# Should succeed
PGPASSWORD='<password_1>' psql -U leobloom_dev -h localhost -d sonofleo_dev -c "SELECT current_database();"

# Should be rejected
PGPASSWORD='<password_1>' psql -U leobloom_dev -h localhost -d sonofleo_prod -c "SELECT current_database();"
```

### 1.9 Run migrations against new databases (FMEA FM-6)

`sonofleo_test` and `sonofleo_prod` are created empty. Run the full
migration set against both before proceeding to verification:

```bash
# Use whatever the current migration mechanism is — connect as the
# owning role and apply all migrations from DbMigrations/
PGPASSWORD='<password_2>' psql -U leobloom_test -h localhost -d sonofleo_test -f <migration_files>
PGPASSWORD='<password_3>' psql -U leobloom_hobson -h localhost -d sonofleo_prod -f <migration_files>
```

Confirm schemas exist (`ledger`, `ops`, `portfolio`) in both databases.

---

## Phase 2 — Host environment (Dan)

### 2.1 Write connection strings to sonofleo.conf

Replace the smoke test content in `~/.config/environment.d/sonofleo.conf`
with the real connection strings. This is the **single source of truth**
(FMEA FM-4) — `.bashrc` already sources this file (set up in Phase 0).

```
SONOFLEO_DEV_CONNSTR=Host=localhost;Port=5432;Database=sonofleo_dev;Username=leobloom_dev;Password=<password_1>;Search Path=ledger,ops,portfolio,public;Include Error Detail=true
SONOFLEO_TEST_CONNSTR=Host=localhost;Port=5432;Database=sonofleo_test;Username=leobloom_test;Password=<password_2>;Search Path=ledger,ops,portfolio,public;Include Error Detail=true
SONOFLEO_PROD_CONNSTR=Host=localhost;Port=5432;Database=sonofleo_prod;Username=leobloom_hobson;Password=<password_3>;Search Path=ledger,ops,portfolio,public
```

No `Include Error Detail` on prod — don't leak SQL internals in errors.

### 2.2 Verify (no logout needed)

Open a new terminal and check:

```bash
echo $SONOFLEO_DEV_CONNSTR
```

Should print the full dev connection string. Launch Rider from a terminal
(`rider`) and verify the same var is visible in Rider's built-in terminal.

---

## Phase 3 — Container definitions (Dan)

In `/media/dan/fdrive/ai-sandbox/compose.yml`, replace:

```yaml
- LEOBLOOM_ENV=Development
- LEOBLOOM_DB_PASSWORD=claude
```

With:

```yaml
- SONOFLEO_DEV_CONNSTR=Host=172.18.0.1;Port=5432;Database=sonofleo_dev;Username=leobloom_dev;Password=<password_1>;Search Path=ledger,ops,portfolio,public;Include Error Detail=true
- SONOFLEO_TEST_CONNSTR=Host=172.18.0.1;Port=5432;Database=sonofleo_test;Username=leobloom_test;Password=<password_2>;Search Path=ledger,ops,portfolio,public;Include Error Detail=true
```

Keep the old `LEOBLOOM_ENV` and `LEOBLOOM_DB_PASSWORD` vars if other projects
in the container still use them (e.g., the original LeoBloom CLI). Only remove
them when nothing references them.

Rebuild:

```bash
cd /media/dan/fdrive/ai-sandbox && docker compose up -d --build
```

---

## Phase 4 — SonOfLeo code changes

These changes live in `/media/dan/fdrive/ai-sandbox/workspace/SonOfLeo/`.
Dan or BD implements — whoever's driving.

### 4.1 Appsettings files — CLI project

**`Src/SonOfLeoCli/appsettings.Development.json`** — replace current contents:

```json
{
    "ConnectionStrings": {
        "SonOfLeo": "SONOFLEO_DEV_CONNSTR"
    }
}
```

**`Src/SonOfLeoCli/appsettings.Production.json`** — new file:

```json
{
    "ConnectionStrings": {
        "SonOfLeo": "SONOFLEO_PROD_CONNSTR"
    }
}
```

### 4.2 Appsettings files — test projects (FMEA FM-1/FM-2 fix)

Each test project that hits the database gets its own
`appsettings.Development.json` containing the **test** env var name. Since
tests build as Debug, the code loads "Development" and finds this file —
no `.runsettings` override needed.

**`Tests/Tests.Integrated/appsettings.Development.json`** — replace current
contents:

```json
{
    "ConnectionStrings": {
        "SonOfLeo": "SONOFLEO_TEST_CONNSTR"
    }
}
```

Copy the same file into every other test project that hits the DB. Check
with BD on which test projects currently need DB access — the structure
may have changed since Hobson last surveyed it (FMEA FM-7).

`Tests.Isolated` has no DB access — leave it alone.

### 4.3 SonOfLeoCli.fsproj

Replace the current Content block:

```xml
<Content Include="appsettings.Development.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
```

With:

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <Content Include="appsettings.Development.json" CopyToOutputDirectory="Always" />
</ItemGroup>
<ItemGroup Condition="'$(Configuration)' == 'Release'">
  <Content Include="appsettings.Production.json" CopyToOutputDirectory="Always" />
</ItemGroup>
```

### 4.4 Test .fsproj files

Test projects keep their existing `appsettings.Development.json` Content
reference — just update the file contents (step 4.2). The .fsproj doesn't
need to change unless it currently references a different filename.

### 4.5 DAL.fs

Replace `getEnvironment`, `getTemplate`, `injectPassword`, and
`getConnectionString` with:

```fsharp
let private getEnvironment(): Result<string, string> =
#if DEBUG
    Ok "Development"
#else
    Ok "Production"
#endif

let private getConnectionString(): Result<string, string> =
    result {
        let! env = getEnvironment()
        let! envVarName =
            try
                let config =
                    ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile($"appsettings.{env}.json", optional = false)
                        .Build()
                let name = config["ConnectionStrings:SonOfLeo"]
                if String.IsNullOrWhiteSpace(name) then
                    Error $"ConnectionStrings:SonOfLeo not found in appsettings.{env}.json"
                else Ok name
            with ex ->
                Error $"Error reading appsettings.{env}.json: {ex.Message}"
        // FM-8: catch accidental connection strings in appsettings
        if envVarName.Contains(";") || envVarName.Contains("Host=") then
            return! Error
                $"ConnectionStrings:SonOfLeo contains a connection string, not an env var name. Got: '{envVarName.[..39]}...'"
        let! connStr =
            Environment.GetEnvironmentVariable envVarName
            |> Option.ofObj
            |> Option.bind (fun s ->
                let t = s.Trim()
                if t = String.Empty then None else Some t)
            |> function
               | Some cs -> Ok cs
               | None -> Error $"Environment variable {envVarName} not set or empty"
#if DEBUG
        // FM-9: symmetric guard — Debug cannot reach prod
        if envVarName.Contains("PROD") then
            return! Error "Debug builds cannot connect to Production"
#else
        // FM-9: symmetric guard — Release can only reach prod
        if not (envVarName.Contains("PROD")) then
            return! Error "Release builds can only connect to Production"
#endif
        return connStr
    }
```

**Deleted:** `getTemplate` and `injectPassword` — no longer needed. The env
var holds the complete connection string.

**`LEOBLOOM_ENV` eliminated entirely.** No env var selects the environment.
The build configuration is the sole selector. Test projects route to the
test DB via their own `appsettings.Development.json` pointing at
`SONOFLEO_TEST_CONNSTR`.

**`LEOBLOOM_DB_PASSWORD` eliminated entirely.** The password is part of
the connection string in the env var.

**Symmetric build guards (FMEA FM-9):** Debug refuses any env var containing
"PROD". Release refuses any env var NOT containing "PROD".

**Self-diagnosing appsettings check (FMEA FM-8):** If someone puts an actual
connection string in appsettings instead of an env var name, the error
message says what went wrong.

### 4.6 Update REQ references

The following REQ-DAL requirements are affected by these changes. Update the
specs to match the new behaviour:

- **REQ-DAL-1.1** — `LEOBLOOM_ENV` eliminated. Environment derived from
  build configuration.
- **REQ-DAL-1.7, 1.9, 1.10, 1.11** — `LEOBLOOM_DB_PASSWORD` eliminated.
  Password is part of the connection string env var.
- **REQ-DAL-3.3** — Debug/Production guard preserved and made symmetric.
  Debug blocks PROD env vars; Release blocks non-PROD env vars.

---

## Phase 5 — Verify

After the logout/login, run each of these:

| # | Action | Expected result |
|---|---|---|
| 1 | Rider, Debug, Run SonOfLeoCli | Connects to `sonofleo_dev` |
| 2 | Rider, Debug, Run tests | Connects to `sonofleo_test` |
| 3 | Terminal: `dotnet run -c Release` (from SonOfLeoCli dir) | Connects to `sonofleo_prod` |
| 4 | Terminal: `dotnet run -c Debug` | Connects to `sonofleo_dev` (no env var needed) |
| 5 | BD container: `dotnet run` (Debug) | Connects to `sonofleo_dev` via `172.18.0.1` |
| 6 | BD container: `dotnet test` | Connects to `sonofleo_test` via `172.18.0.1` |
| 7 | Terminal: `leobloom_dev` role → `sonofleo_prod` | Rejected by `pg_hba.conf` |
| 8 | Swap test appsettings to `SONOFLEO_PROD_CONNSTR`, Debug build | Fails: "Debug builds cannot connect to Production" |
| 9 | Set Release appsettings to `SONOFLEO_DEV_CONNSTR`, Release build | Fails: "Release builds can only connect to Production" |

---

## What this gives you

- **Dan hits Run in Rider** → dev. Always. No env var needed.
- **Dan runs tests in Rider** → test. Always. Structural, not configured.
- **Hobson runs Release from bash** → prod. Always.
- **BD develops and tests from the container** → dev and test via `172.18.0.1`.
- **Even if every env var is wrong**, `pg_hba.conf` reject rules make it
  physically impossible for the wrong role to reach the wrong database.
- **No secrets in the repo.** Appsettings files contain env var names, not
  values.
- **No `LEOBLOOM_ENV` required.** Build config is the sole selector.
- **No `LEOBLOOM_DB_PASSWORD`.** Eliminated entirely.
- **No `.runsettings` fragility.** Tests route structurally via their own
  appsettings.
- **Single source of truth for credentials.** One file
  (`~/.config/environment.d/sonofleo.conf`), sourced by `.bashrc`, read by
  the desktop session. Password rotation = edit one file + logout/login.
- **Symmetric build guards.** Debug can't reach prod, Release can't reach
  non-prod. Belt and suspenders with the pg_hba lockdown.
- **Self-diagnosing errors.** Wrong appsettings content, missing env var,
  wrong build config — each produces a specific, actionable error message.

---

## FMEA disposition

| ID | Finding | Disposition |
|---|---|---|
| FM-1 | Tests silently hit dev DB via .runsettings fragility | **Fixed.** Test projects' `appsettings.Development.json` points to `SONOFLEO_TEST_CONNSTR`. No .runsettings needed. |
| FM-2 | Rider may not auto-discover .runsettings | **Eliminated.** No .runsettings in the design. |
| FM-3 | environment.d might not work on Cinnamon | **Addressed.** Phase 0 smoke test added. Plan stops if it fails. |
| FM-4 | Passwords duplicated in .bashrc and environment.d | **Fixed.** `.bashrc` sources the environment.d file. Single source of truth. |
| FM-5 | `claude` role retains access | **Fixed.** Explicit REVOKE added (Phase 1.5). |
| FM-6 | New databases have no schemas | **Fixed.** Migration step added (Phase 1.9). |
| FM-7 | Plan stale on test project structure | **Addressed.** Plan defers to BD on which test projects need DB access. |
| FM-8 | No guardrail for connection string in appsettings | **Fixed.** Sanity check in DAL.fs — detects `;` or `Host=` in the value. |
| FM-9 | Asymmetric build guard | **Fixed.** Release blocks non-PROD env vars. |
