# Validate Then Construct

**Source:** Doctrines.md, Type Validation Doctrine — The Constructor Rule

Every entity type has exactly one private function called `validateThenConstruct`. It takes primitives, validates every single-field and cross-field constraint, and returns `Result<T, string>`. No record literals may appear anywhere outside `validateThenConstruct`.

## What works
- Every construction route — new creation, reconstitution from persistence, assembly for any purpose — calls `validateThenConstruct`
- The function name is reserved: it always means "the single private constructor that validates and assembles a record from primitives"
- Public-facing functions wrap VTC with domain-appropriate names (e.g., `create`, `fromString`, `constructNewAndSaveToDbUsingParentCode`)

## What doesn't
- Record literals outside VTC — even "just for tests" or "just for mapping"
- Multiple construction paths that bypass validation
- Naming the function anything other than `validateThenConstruct`

## Example
`AccountName.create` calls `AccountName.validateThenConstruct` internally. So does the persistence read path when reconstituting an account from the database. Both go through the same validation.
