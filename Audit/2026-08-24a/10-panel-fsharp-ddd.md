# idiom-auditor

## IDIOM-FMC-1 — idiom
- **Location:** Src/Model/DataIngestion/Classification/FieldMatchChain.fs, line 14
- **Summary:** FieldMatchChain.create accepts an empty chain list, breaking the smart-constructor discipline the rest of the codebase establishes.
- **Resolution:** fix-code

FieldMatchChain.create (line 14) is `let create (chain: FieldMatch list) = { chain = chain }` -- a bare record construction with no invariant check. The type's own doesMatch function (line 21) has a backstop `if fieldMatchChain.chain |> List.isEmpty then false` that silently converts the illegal state into a 'no match' result instead of erroring. Two creation paths can reach this: (1) the InterfaceBridge converter at IngestionFieldConverters.fs line 206 calls `FieldMatchChain.create chain` on whatever the boundary layer produced, without an empty check at the call site; (2) ClassificationRule.reconstitute (ClassificationRule.fs line 106) deserializes rule groups from JSONB via `fromJson<ClassificationRuleGroup list>`, which constructs FieldMatchChain values directly through System.Text.Json without passing through any validation. The orchestration-layer validation (`ClassificationOrchestration.confirmFieldMatchChain`) runs only on the create/update paths, not on the reconstitution path. Compare with the Ledger domain where every value type (AccountCode.create, AccountName.create, Money.fromDecimal, FiscalPeriodKey.fromString) validates in its constructor and returns Result -- FieldMatchChain's create is the exception that proves the rule.

**Action:** Change FieldMatchChain.create to return Result<FieldMatchChain, AppError>, rejecting empty lists with IngestionFieldMatchChainEmpty. Update the InterfaceBridge converter to bind (let!) the result. The doesMatch backstop can remain as defense-in-depth but should never be the primary guard.

**Why:** The smart-constructor pattern (private type + validating factory) is the F# mechanism for making illegal states unrepresentable. When the constructor skips validation, the type's privacy guarantee is hollow -- callers hold a FieldMatchChain value and believe the invariant holds, but it may not. The silent backstop in doesMatch compounds this: instead of failing fast at construction, the system silently classifies nothing. In a financial classification engine, silent non-matching is indistinguishable from 'the rule just did not match,' making the bug invisible in production.

---

## IDIOM-CR-1 — idiom
- **Location:** Src/Model/DataIngestion/Classification/ClassificationRule.fs, lines 35-53 and 169-170
- **Summary:** ClassificationRule.create accepts an empty ruleGroups list, the same smart-constructor gap as FieldMatchChain but at the rule level.
- **Resolution:** fix-code

ClassificationRule.create (line 35) accepts `ruleGroups: ClassificationRuleGroup list` without validating non-emptiness. The doesMatch function (line 170) has a backstop: `if classificationRule.ruleGroups |> List.isEmpty then false`. Like FieldMatchChain, validation only occurs at the orchestration layer (ClassificationOrchestration.confirmRuleGroups checks `if ruleGroups |> List.isEmpty then Error IngestionClassificationRuleGroupsEmpty`). The reconstitution path at line 106 deserializes from JSONB and passes directly to create, bypassing that check. If the JSONB column were to contain an empty array -- whether through manual DB edit, migration artifact, or future bug in the serialization layer -- the rule would silently fail to match anything.

**Action:** Change ClassificationRule.create to return Result<ClassificationRule, AppError> and reject empty ruleGroups with IngestionClassificationRuleGroupsEmpty. This aligns with the existing error case and the Ledger domain's constructor discipline.

**Why:** Same principle as IDIOM-FMC-1: the smart constructor is the contract between a type and its consumers. An F# private record with a public create that does not validate is a lock with no bolt -- the privacy prevents direct construction but the factory lets anything through. In domain-driven design, the domain model is the authority on what constitutes a valid domain object. Deferring that authority to the orchestration layer means the model can host invalid objects, and downstream code that pattern-matches on them (doesMatch, Classifier.classify) must defensively compensate rather than trusting the type.

---

## MAINT-TZ-1 — maintainability
- **Location:** Src/Utilities/Clock.fs lines 9-11 and Src/Utilities/Calendar.fs lines 8-10
- **Summary:** The localized time zone binding is duplicated between Clock.fs and Calendar.fs -- two independent reads of the same config value in a temporal system.
- **Resolution:** fix-code

Clock.fs (lines 9-11) and Calendar.fs (lines 8-10) each independently declare `let timeZoneLocal = match getConfigValue<string> "LocalizedTimeZone" with ...`. The bindings are character-for-character identical. Calendar.fs already depends on Clock (Calendar.today calls Clock.now at line 15), so referencing Clock.timeZoneLocal would add zero new coupling. Calendar.fs also uses its own timeZoneLocal in dateFromInstant (line 13) and localDateToString (line 21). Today both bindings read the same config key through the same cache, so they will always agree. The risk is that a future edit to one (changing the key name, adding a fallback, switching providers) does not propagate to the other, producing a system where instants and dates resolve to different time zones -- a class of temporal bug that is notoriously difficult to diagnose.

**Action:** Remove the timeZoneLocal binding from Calendar.fs and reference Clock.timeZoneLocal instead (e.g., `let dateFromInstant (i: Instant) : LocalDate = i.InZone(Clock.timeZoneLocal).Date`). Single source of truth for the localized time zone.

**Why:** In a temporal system, the time zone is a fundamental parameter that converts between instants and dates. Having two independent bindings for the same parameter violates the single-source-of-truth principle. In functional programming, referential transparency means that if two expressions denote the same value, they should be the same binding -- duplicating the expression duplicates the maintenance surface without duplicating the information. The config cache makes them referentially equal today, but that equality is incidental (it depends on the cache implementation), not structural (guaranteed by the code).

---
