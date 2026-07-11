# Compounded Learnings

Operational knowledge accumulated through building, auditing, and using SonOfLeo. This is the jurisprudence — how we interpret, apply, and work with the behavioral specs. The specs are the law; this is how the law gets practiced.

## How it works

**Catalogs** (`catalogs/`) are lightweight indexes. One per domain. Each row names a concept, points to an article, and says when to read it. An agent starting a task reads the catalogs relevant to that task — nothing more.

**Articles** (`articles/<domain>/`) are atomic. One concept, one file. The full explanation: what works, what doesn't, examples where they help, provenance. An agent reads an article only when the catalog's "when to read" trigger fires during the task.

## Domains

| Domain | Catalog | What it covers |
|---|---|---|
| audit-conduct | `catalogs/audit-conduct.md` | How to behave as an auditor — judgment standards, scope discipline, verification rules |
| gaap-domain | `catalogs/gaap-domain.md` | Accounting concepts agents must understand to work with this ledger |
| architecture | `catalogs/architecture.md` | Settled structural decisions — what was decided, why, and what not to re-litigate |
| coding | `catalogs/coding.md` | F# idioms, naming, temporal handling, constructor discipline, conventions |
| testing | `catalogs/testing.md` | Test writing doctrine, fixture rules, coverage accounting |
| process | `catalogs/process.md` | Operational choreography — how audits run, how migrations get reviewed, traceability |

## For skill authors

Every SonOfLeo skill should start by having the agent read the catalogs relevant to the task. Add this to your SKILL.md:

```
## Before you begin
Read the CompoundedLearnings catalogs relevant to this task:
- `CompoundedLearnings/catalogs/<domain>.md`
```

If your skill discovers something worth compounding, invoke the CreateLearning skill or note it for Dan to record later.

## Authority

CompoundedLearnings is operational guidance. It does not override Specs/Behavioral/ (the requirements). When a learning and a requirement appear to conflict, the requirement wins and the learning needs updating.
