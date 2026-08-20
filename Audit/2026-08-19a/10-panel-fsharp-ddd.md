# code-quality-auditor

## IDIOM-FC-1 — idiom
- **Location:** Src/Model/DataIngestion/Classification/FieldMatchChain.fs:21-26, Src/Model/DataIngestion/Classification/ClassificationRule.fs:303-309
- **Summary:** All-match semantics implemented via map/filter/count instead of List.forall in FieldMatchChain.doesMatch and ClassificationRule.doesMatch.
- **Resolution:** fix-code

Both FieldMatchChain.doesMatch (lines 21-26) and ClassificationRule.doesMatch (lines 303-309) implement 'do all elements satisfy a predicate' by mapping to booleans, filtering for false, counting, and comparing to zero:

```
let failureCount =
    fieldMatchChain.chain
    |> List.map (fun fieldMatch -> fieldMatch |> FieldMatch.doesMatch candidate)
    |> List.filter(fun x -> x = false)
    |> List.length
failureCount = 0
```

The idiomatic expression is `fieldMatchChain.chain |> List.forall (FieldMatch.doesMatch candidate)`. The identical anti-pattern appears in ClassificationRule.doesMatch with ClassificationRuleGroup.doesMatch.

**Action:** Replace both implementations with List.forall. FieldMatchChain: `fieldMatchChain.chain |> List.forall (FieldMatch.doesMatch candidate)`. ClassificationRule: `classificationRule.ruleGroups |> List.forall (ClassificationRuleGroup.doesMatch candidate)`.

**Why:** List.forall is the standard higher-order combinator for universal quantification over a collection. It communicates the intent directly ('do ALL elements satisfy this predicate?') and short-circuits on the first false, avoiding allocation of two intermediate lists. The map/filter/count encoding is the imperative loop-and-counter idiom disguised in pipe syntax. Choosing the right combinator is the core FP skill: the standard library's combinators exist precisely so you express what you mean, not how to compute it.

---

## IDIOM-CL-1 — idiom
- **Location:** Src/Model/DataIngestion/Classification/Classifier.fs:17-24
- **Summary:** Classifier.classifyCandidate matches on List.length instead of pattern matching on list structure, requiring a separate List.head call.
- **Resolution:** fix-code

classifyCandidate computes `matches |> List.length` and then matches on the integer (0, 1, _), with the single-match branch extracting the element via `matches |> List.head`:

```
match matches |> List.length with
| 0 -> { candidate = candidate; outcome = NoMatch; }
| 1 -> { candidate = candidate; outcome = OneMatch (matches |> List.head); }
| _ -> ...
```

The idiomatic F# is to match directly on the list structure:

```
match matches with
| [] -> { candidate = candidate; outcome = NoMatch }
| [single] -> { candidate = candidate; outcome = OneMatch single }
| _ -> ...
```

**Action:** Replace `match matches |> List.length with | 0 -> ... | 1 -> ... | _ -> ...` with `match matches with | [] -> ... | [single] -> ... | _ -> ...`, binding the single element directly.

**Why:** Pattern matching on list structure is a fundamental F# idiom because it solves three problems at once: (1) it avoids traversing the entire list just to distinguish empty/one/many, (2) it binds the element in the single-match case directly, eliminating the need for List.head (a partial function), and (3) the compiler verifies exhaustiveness. When you reach for List.length and then List.head, you are manually encoding what the pattern match gives you for free. The deeper principle is that algebraic data types (lists included) are meant to be destructured, not measured and indexed.

---

## MAINTAINABILITY-TZ-1 — maintainability
- **Location:** Src/Utilities/Clock.fs:6, Src/Utilities/Calendar.fs:6
- **Summary:** The system timezone is independently defined in both Clock.eastern and Calendar.localTimeZone as separate lookups of America/New_York.
- **Resolution:** fix-code

Clock.fs line 6 defines `let eastern = DateTimeZoneProviders.Tzdb.["America/New_York"]` and Calendar.fs line 6 defines `let localTimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]`. These are two independent module-level values that must be semantically equal but are not structurally linked. Calendar.fs already depends on Clock (Calendar.today calls Clock.now), so there is no circular-dependency barrier to having Calendar reference Clock.eastern instead of defining its own constant. If either definition were changed without the other, the system would silently derive dates from one timezone while formatting instants in another.

**Action:** Have Calendar.localTimeZone reference Clock.eastern (e.g., `let localTimeZone = Clock.eastern`) rather than performing an independent TZDB lookup. This establishes a single point of truth for the system timezone.

**Why:** In functional programming, a value that participates in multiple computations should have a single canonical definition that all consumers reference. Two independent definitions of the same constant create an invariant (they must be equal) that the type system does not enforce. This is the FP 'single source of truth' principle: when a domain concept (here, the system's civil timezone) has one meaning, it should have one definition. Duplication of constants is how 'magic number' bugs enter a codebase -- the fix is always to name the value once and reference it everywhere.

---
