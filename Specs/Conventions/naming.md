# Naming

- **Smart constructor naming — `create` vs `fromString`:** does the type *wrap* the input?
  Use `create` (e.g., `AccountName.create`, `AccountActivityPeriod.create`). Does the input
  merely *name* one of a fixed set of cases? Use `fromString` (e.g., `AccountType.fromString`,
  `AccountSubtype.fromString` — parse boundaries from a string label to a DU case).
  `create` is the only verb that scales to multi-arg constructors; `fromString` is the honest
  name for parsing an enumeration's label. Don't unify them.
