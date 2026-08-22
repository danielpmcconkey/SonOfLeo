# code-quality

## IDIOM-TZ-1 — maintainability
- **Location:** Src/Utilities/Clock.fs line 9, Src/Utilities/Calendar.fs line 8
- **Summary:** timeZoneLocal is defined independently in both Clock and Calendar modules, duplicating the same config read and time zone construction.
- **Resolution:** fix-code

Clock.fs (line 9-11) and Calendar.fs (line 8-10) each define their own `timeZoneLocal` by independently reading `getConfigValue<string> "LocalizedTimeZone"` and resolving it through `DateTimeZoneProviders.Tzdb`. Calendar already depends on Clock -- it calls `Clock.now()` on line 15 -- so the compile order supports referencing `Clock.timeZoneLocal`, which is a public module value. Calendar's copy is used only in `dateFromInstant` (line 13); Clock's copy is used only in `instantToString` (line 26). Both produce the same value from the same config key, but they are two separate module-level bindings, each performing their own config read and time zone provider lookup at module initialization.

**Action:** Remove the `timeZoneLocal` definition in Calendar.fs and reference `Clock.timeZoneLocal` instead. Calendar already depends on Clock.

**Why:** In functional programming, a value should have exactly one definition. When two modules need the same data, one references the other -- that is what the module system is for. Duplicating a definition creates a maintenance coupling that the compiler cannot see: if the config key name changes, or the resolution logic needs adjustment (e.g. a fallback), the change must be made in two places with no compiler guidance. Calendar already depends on Clock, so the dependency exists; the duplicate just hides it.

---

## IDIOM-FMC-1 — idiom
- **Location:** Src/Model/DataIngestion/Classification/FieldMatchChain.fs line 14, Src/Model/DataIngestion/Classification/ClassificationRuleGroup.fs line 23
- **Summary:** FieldMatchChain.create and ClassificationRuleGroup.create accept illegal states (empty chains/groups) without validation, relying on a silent backstop in doesMatch rather than failing at construction.
- **Resolution:** fix-code

FieldMatchChain.create (line 14) is `let create (chain: FieldMatch list) = { chain = chain }` -- it accepts an empty list without returning a Result or raising an error. The type is private, so `create` is the only non-reflection construction path. The backstop in `doesMatch` (line 21) silently returns `false` for empty chains rather than failing loudly. ClassificationRuleGroup.create (line 23) similarly accepts any inputs without validation.

The actual validation lives in the orchestrator: `ClassificationOrchestration.confirmFieldMatchChain` (line 30) checks for emptiness, and `confirmRuleGroups` (line 47) checks the groups list. This means the invariant (chains must be non-empty) is enforced only at the orchestration layer, not at the type level.

Further, `ClassificationRule.reconstitute` (line 92) deserializes rule groups via `ruleGroupsStr |> fromJson<ClassificationRuleGroup list>`, which uses FSharp.SystemTextJson's JsonFSharpConverter. This constructs the private types through reflection, completely bypassing the module's `create` functions. An empty chain in the JSON payload would produce a valid `FieldMatchChain` value that silently never matches anything.

Per CompoundedLearnings/articles/coding/validation-layers.md: 'Constructors validate the record's shape.' An empty chain is a shape violation.

**Action:** Have FieldMatchChain.create return Result<FieldMatchChain, AppError>, rejecting empty chains. Propagate the Result through ClassificationRuleGroup.create. Either add a post-deserialization validation step in ClassificationRule.reconstitute, or accept that the orchestrator's validation is the guard for persisted data -- but at minimum, the public create function should not allow constructing the illegal state.

**Why:** The core FP discipline 'make illegal states unrepresentable' means the type's constructor is the first and most important guard. When the constructor permits an illegal state and the backstop silently degrades (returning false instead of raising an error), the system absorbs the defect as 'no match found' rather than surfacing it. The validation-layers article makes this explicit: constructors own shape validation, and shape validation is not optional even when an outer layer also validates. The constructor is the contract the type offers to all callers, including future callers who may not know about the orchestrator's separate check.

---

## IDIOM-CR-1 — architecture
- **Location:** Src/Model/DataIngestion/Classification/ClassificationRule.fs lines 92, 119
- **Summary:** ClassificationRule.reconstitute and mapRawForDbRead are public, leaking persistence internals to the orchestration layer, contrary to the pattern established by every other domain module.
- **Resolution:** fix-code

ClassificationRule.fs exposes both `reconstitute` (line 92) and `mapRawForDbRead` (line 119) without a `private` modifier. These are consumed directly by ClassificationOrchestration.fetchRulesFiltered (line 156-158), which passes them to `executeReaderQuery`.

Every other domain module in the codebase keeps these functions private:
- Account.fs: `let private mapRawForDbRead` (line 115), `let private reconstitute` (line 69)
- FiscalPeriod.fs: `let private mapRawForDbRead` (line 74), `let private reconstitute` (line 83)
- JournalEntryLine.fs: `let private mapRawForDbRead` (line 83), `let private reconstitute` (line 100)
- JournalEntryExternalReference.fs: `let private mapRawForDbRead` (line 71), `let private reconstitute` (line 85)
- JournalEntryComment.fs: `let private mapRawForDbRead` (line 66), `let private reconstitute` (line 80)
- StageEntryHeader.fs: `let private mapRawForDbRead` (line 121), `let private reconstitute` (line 89)
- StageEntryLine.fs: `let private mapRawForDbRead` (line 132), `let private reconstitute` (line 104)

When the orchestrator needs custom queries, the established patterns are: JournalEntryHeader exposes `readRowsFromDb` (a higher-level function that still encapsulates the mapper/reconstituter), and StageEntryHeader exposes `fetchByQuery` (which accepts raw SQL but keeps its mapper/reconstituter private).

**Action:** Add a `readRowsFromDb` or `fetchByQuery` function to ClassificationRule.fs that accepts the query, parameters, and expected rows but keeps mapRawForDbRead and reconstitute private. Mark both functions private. ClassificationOrchestration.fetchRulesFiltered would then call the new function instead of directly referencing the internals.

**Why:** Module boundary encapsulation is a core DDD principle: a domain module owns its type, its validation, and its persistence. The mapper and reconstituter embody the contract between the domain type and its database representation -- column names, tuple shapes, the specific smart constructors called during reconstitution. When these are public, the orchestrator can couple to those details, and changes to the persistence schema require changes in two layers instead of one. The established codebase pattern (private mapper/reconstituter, public read functions) already embodies this principle. The ClassificationRule module should follow it.

---
