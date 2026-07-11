# Schema-Guaranteed Values Are Not Partial

**Source:** Audit 2026-07-06a — FSDDD-06 overrule, skill improvement item #106a

Before flagging `Option.get` or similar partial operations as unsafe, verify whether the value is guaranteed by schema constraints (NOT NULL, FK) or query structure. Schema-guaranteed values are not "smuggled partiality."

## Example

FSDDD-06 flagged eight `Option.get` calls in AccountActivity's `constructFromRawForDbRead` as unsafe. The auditor called it "smuggling partiality into a Result-returning function." Dan: "when line_id is Some, it means there's a journal entry. All non-optional fields after are ripped open using Option.get. Those that are optional are left as options. What's the problem?"

The columns are NOT NULL in the schema. When `line_id` is present, `amount`, `line_type`, `created_at`, etc. are guaranteed non-null by the DB. `Option.get` on a schema-guaranteed value is a correct use of the operation, not a safety violation.

## The rule

`Option.get` is unsafe when the value's presence is uncertain. It is fine when the value is guaranteed by a NOT NULL column, a FK constraint, or a query structure that only produces the row when the value exists. Check `DbMigrations/` for the relevant table's column definitions before flagging.
