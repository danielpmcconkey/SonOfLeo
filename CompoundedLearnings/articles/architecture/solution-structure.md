# Solution Structure

**Source:** Codebase observation, 2026-07-11

The solution has five projects under `Src/`, with a strict dependency direction: Utilities <- Model <- ModelOrchestrator <- SonOfLeoCli. DevDataStage sits beside the CLI for dev-only data seeding.

## Projects

| Project | Role | Depends on |
|---|---|---|
| **Utilities** | Shared infrastructure with no domain knowledge (Clock, Calendar) | nothing |
| **Model** | Domain modules — types, VTC, persistence. Subdivided by domain: `Ledger/`, `Ops/`, `Portfolio/`, `Reporting/`, `UI/` | Utilities |
| **ModelOrchestrator** | Cross-domain composition — operations that span multiple Model modules | Model, Utilities |
| **SonOfLeoCli** | Interface layer — CLI commands, argument parsing, output formatting | ModelOrchestrator, Model |
| **DevDataStage** | Dev-only test data seeding (not shipped) | ModelOrchestrator, Model, Utilities |

## Where new code goes

- New entity type or single-domain CRUD -> `Model/<domain>/`
- Cross-domain operation -> `ModelOrchestrator/`
- New CLI command -> `SonOfLeoCli/`
- Shared infrastructure (time, config, connection) -> `Utilities/`
- Test data helpers -> `DevDataStage/`
