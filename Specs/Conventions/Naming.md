# Naming

Naming is hard. No rules, just some names that are worse than others.

## When to use "create" vs "from" when constructing types

Does the type *wrap* the input? Use `create` (e.g., `AccountName.create`, `AccountActivityPeriod.create`). Does the input merely *name* one of a fixed set of cases? Use `fromString` (e.g., `AccountType.fromString`, `AccountSubtype.fromString` — parse boundaries from a string label to a DU case). `create` is the only verb that scales to multi-arg constructors; `fromString` is the honest name for parsing an enumeration's label. Don't unify them.
