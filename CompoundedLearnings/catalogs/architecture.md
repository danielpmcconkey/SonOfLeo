# Architecture

Settled structural decisions for SonOfLeo — what was decided, why, and what not to re-litigate. Read this catalog before proposing structural changes or placing new code. For the layering itself — which project a file belongs in, dependency direction — and for what infrastructure already exists, the authority is `Src/README.md`.

| Concept | Article | Read when... |
|---|---|---|
| Orchestration layer | `articles/architecture/orchestration-layer.md` | Deciding whether a function belongs in a domain module or in orchestration |
| DAL errors are backstops | `articles/architecture/dal-errors-are-backstops.md` | Writing an orchestrator function that calls `fetchById`/`fetchByX` and needs to decide what error the operator should see on failure |
| Environment separation | `articles/architecture/environment-separation.md` | Working with database connections, environment config, or secrets |
| Debug/release access | `articles/architecture/debug-release-access.md` | Configuring build modes or database connection strings |
| Type taxonomy | `articles/architecture/type-taxonomy.md` | Creating a new type and deciding what category it belongs to |
| Container build discipline | `articles/architecture/container-build-discipline.md` | Running dotnet build inside the Docker container |
| No REQ annotations in source | `articles/architecture/no-req-annotations-in-source.md` | You're about to add a `// REQ-` comment to `.fs` or `.sql`, report a missing source annotation as a finding, or trust one you found in the code |
