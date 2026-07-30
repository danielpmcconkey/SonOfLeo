# Environment Separation

**Source:** the retired Conventions/BuildAndEnvironment.md (removed 2026-07-30)

The system maintains entirely separate databases for development and production. Cross-contamination of entity data (see `Specs/Definitions.md`, Entity) between environments is strictly prohibited.

## Rules

- Separate databases per environment — no shared instances
- Any layer above persistence must be explicitly aware of its environment via environment variables
- Production database password must be distinct from dev
- The container (BD's workspace) must never have access to the host's environment variables or secrets
