# Entity Identification by Primary Key Is Obvious

**Source:** Audit 2026-07-06a — skill improvement item #17c

When a requirement says "update X" or "retrieve X," the entity (see `Specs/Definitions.md`, Entity) is identified by its primary key. This does not need to be stated in the spec. Do not flag entity identification as under-elaborated when the entity has a UUID primary key.

## The rule

Every entity in this system has a UUID primary key. "Update an external reference's FI and value" means "identify the reference by its UUID, then update." A spec does not need to spell out that you find a record by its primary key.
