# code-outward-coverage-auditor

## COV-SRC-1 — missing-requirement
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs (lines 267-276), Src/Model/DataIngestion/IngestionSource.fs
- **Summary:** The ingestion source creation orchestration function and its underlying model module implement a CLI-observable feature with no behavioral requirement.
- **Resolution:** fix-spec

StageEntryOrchestration.createNewSource (line 267) creates a new IngestionSource entity and persists it. This is exposed to the CLI as the route Ingestion CreateIngestionSource (IngestionRoutes.fs:276). The function is tested via the integration test 'REQ-STG-2.4 CreateIngestionSource route happy path' (IngestionRoutes.fs:506). However, no behavioral requirement describes the ability to create an ingestion source. REQ-STG-2.4 ('Staged entry must reference a source in ingestion.source') specifies the FK constraint on staged entries, not source creation. REQ-STG-3.6 ('When a record's fi_source does not resolve to an existing source, the system must reject the file') specifies source validation during ingestion, not source management. The IngestionSource model module (IngestionSource.fs) defines the entity type with create, insertNewToDb, fetchByName, and accessor functions, none of which are covered by any REQ. Dan's statement confirms this is part of the delivered slice ('We created UI routes for managing... ingestion sources'). The test that covers this route cites REQ-STG-2.4, but that REQ does not describe source creation.

**Action:** Add a section to DataIngestion.md (or a dedicated IngestionSourceCrud spec) with REQs covering: (1) the system must provide a means to create a new ingestion source, (2) ingestion source name constraints (non-empty, max length), (3) ingestion source field requirements (system-generated UUID, name, timestamps). Update the route test tag from REQ-STG-2.4 to the new REQ ID.

**Why:** An observable CLI feature without a behavioral requirement means the test has no authoritative spec to verify against. The test currently cites REQ-STG-2.4 (a FK constraint requirement) as its backing, which means the test's correctness criteria are disconnected from what it actually exercises. If the source creation behavior is later modified, there is no spec to adjudicate whether the change is correct.

---
