# Classification Rule CRUD

Service-level behavioral specs for creating, reading, and managing classification rules — the pattern-matching engine that assigns accounts to staged lines during data ingestion. Cross-cutting policies (string trimming, data-state enforcement, audit timestamps) live in SystemWide.md.

**Design note — evaluation domain.** Classification rules are evaluated in F#, not in SQL. The rule body is stored as JSONB and reconstituted into a typed domain model at read time. This eliminates the SQL injection surface that would exist if patterns were interpolated into queries.

**Design note — authority hierarchy.** Classification rules occupy the middle tier of the account-assignment authority hierarchy defined in DataIngestion.md: parser (highest) > classifier > operator (lowest, but can override all). The classifier only fills null account assignments; it never overrides parser assignments (REQ-STG-5.3).


## 1. Valid and invalid data states for the ClassificationRule type and related types

### ClassificationRule

- **REQ-CR-1.1** Classification rule ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-CR-1.2** Classification rule name cannot be null.
- **REQ-CR-1.3** Classification rule name cannot be whitespace only (post-trim per REQ-SYS-1.1).
- **REQ-CR-1.4** Classification rule name length cannot exceed 250 characters.
- **REQ-CR-1.22** Classification rule name must be unique across all classification rules.
  - *Why:* Rules are fetched by name (REQ-CR-5.2). Duplicate names would make the single-result fetch ambiguous. Enforced by a unique constraint in the database. (2026-08-25)
- **REQ-CR-1.5** Classification rule must reference a valid account (`accountIdAtMatch`, foreign key to `ledger.account`). The account must exist in the chart of accounts at creation time and at update time.
- **REQ-CR-1.6** Classification rule priority is an integer. Lower values represent higher priority — when multiple rules match a candidate, the rule with the lowest priority value wins.
- **REQ-CR-1.7** Classification rule must contain at least one rule group.
- **REQ-CR-1.8** Classification rule has an `isActive` boolean flag. Only active rules participate in classification (REQ-STG-5.1, enforced by the classifier filtering to active rules before evaluation).

### ClassificationRuleGroup

- **REQ-CR-1.9** A rule group has a connector that must be one of `'And'` or `'Or'`.
- **REQ-CR-1.10** A rule group has a primary chain (`chainOne`) that is required.
- **REQ-CR-1.11** A rule group has an optional secondary chain (`chainTwo`). When `chainTwo` is absent, the connector is unused and the group's match result is `chainOne`'s result alone.

### FieldMatchChain

- **REQ-CR-1.12** A field match chain is a non-empty list of field matches. All field matches in the chain must evaluate to true for the chain to evaluate to true (AND-connected).

### FieldMatch

- **REQ-CR-1.13** A field match targets exactly one of: `Source`, `Description`, `Memo`, `LineType`, or `Amount`.
- **REQ-CR-1.14** `Source`, `Description`, and `Memo` field matches carry a `StringSearchPattern` evaluated as a regex against the candidate's corresponding field value.
- **REQ-CR-1.15** `LineType` field matches carry a `JournalEntryLineType` value and evaluate by exact equality against the candidate's line type.
- **REQ-CR-1.16** `Amount` field matches carry a `MoneySearchPattern` (a `NumericSearchOperator` and a `Money` value) and evaluate by comparing the candidate's amount against the pattern's amount using the specified operator.

### StringSearchPattern

- **REQ-CR-1.17** String search pattern cannot be null.
- **REQ-CR-1.18** String search pattern cannot be empty. Whitespace is not trimmed — REQ-SYS-1.1 does not apply because whitespace is meaningful in regex patterns.
- **REQ-CR-1.19** String search pattern length cannot exceed 500 characters.

### NumericSearchOperator

- **REQ-CR-1.20** Numeric search operator must be one of: `'GreaterThan'`, `'LessThan'`, `'GreaterThanOrEqualTo'`, `'LessThanOrEqualTo'`, `'ExactlyEqual'`.

### MoneySearchPattern

- **REQ-CR-1.21** The amount field within a money search pattern must satisfy all Money data state requirements (REQ-MON-1.*).


## 2. Rule evaluation behaviors

- **REQ-CR-2.1** A field match evaluates to true when the candidate's field value satisfies the match criterion: regex match for string fields, exact equality for `LineType`, numeric comparison for `Amount`.
- **REQ-CR-2.2** A `Memo` field match evaluates to false when the candidate's memo is absent (None).
- **REQ-CR-2.3** A field match chain evaluates to true only when every field match in the chain evaluates to true.
- **REQ-CR-2.4** When a rule group has no secondary chain (`chainTwo` is None), the group evaluates to the result of `chainOne` alone.
- **REQ-CR-2.5** When a rule group's connector is `'And'`, the group evaluates to true only when both `chainOne` and `chainTwo` evaluate to true.
- **REQ-CR-2.6** When a rule group's connector is `'Or'`, the group evaluates to true when either `chainOne` or `chainTwo` (or both) evaluates to true.
- **REQ-CR-2.7** A classification rule evaluates to true only when every rule group in its `ruleGroups` list evaluates to true (AND-connected across groups).
- **REQ-CR-2.8** A field match chain with no field matches evaluates to false. An empty chain matches nothing rather than everything.
  - *Why:* `List.forall` on an empty list returns true (vacuous truth). Without an explicit guard, an empty chain would silently match every candidate. Construction-time validation (REQ-CR-4.7, REQ-CR-6.4) prevents empty chains from being persisted; this requirement governs the evaluation backstop. (2026-08-21)
- **REQ-CR-2.9** A classification rule with an empty rule groups list evaluates to false. An empty groups list matches nothing rather than everything.
  - *Why:* Same vacuous-truth hazard as REQ-CR-2.8. Construction-time validation (REQ-CR-4.6, REQ-CR-6.4) prevents empty groups from being persisted; this requirement governs the evaluation backstop. (2026-08-21)

## 3. Classifier behaviors

- **REQ-CR-3.1** The classifier accepts a list of rules and a list of match candidates and returns one `ClassificationResult` per candidate.
- **REQ-CR-3.2** Before evaluating, the classifier filters the rule list to active rules only.
- **REQ-CR-3.3** When no active rule matches a candidate, the outcome is `NoMatch`.
- **REQ-CR-3.4** When exactly one active rule matches a candidate, the outcome is `OneMatch` carrying the matching rule's account ID, rule ID, and priority.
- **REQ-CR-3.5** When multiple active rules match and one has a strictly lower priority value than all others, the outcome is `ManyMatchesClearWinner` carrying the winner and the full list of matches.
- **REQ-CR-3.6** When multiple active rules match and two or more share the lowest priority value, the outcome is `ManyMatchesTied` carrying all matches.


## 4. Create behaviors

- **REQ-CR-4.1** The system must provide a means to create a new classification rule.
- **REQ-CR-4.2** When creating a classification rule, the system must generate a unique UUID for the ID (new UUIDs may not be passed in).
- **REQ-CR-4.3** When creating a classification rule, the system must validate that the account at match resolves to an existing account in the chart of accounts. If it does not, the creation must fail.
- **REQ-CR-4.4** New classification rules are always created as active (`isActive = true`).
- **REQ-CR-4.8** The system must not provide a mechanism to create a classification rule in an inactive state.
- **REQ-CR-4.5** On successful creation, the system must persist the rule and return the fully constructed classification rule with its generated ID and timestamps.
- **REQ-CR-4.6** When creating a classification rule, the system must validate that the rule groups list is not empty. If it is, the creation must fail.
- **REQ-CR-4.7** When creating a classification rule, the system must validate that every field match chain within every rule group is not empty. If any chain is empty, the creation must fail.


## 5. Read behaviors

- **REQ-CR-5.1** The system must be able to retrieve a classification rule by its ID.
- **REQ-CR-5.2** The system must be able to retrieve a classification rule by its name (exact match).
- **REQ-CR-5.3** The system must be able to retrieve classification rules by a combination of optional filter criteria: rule ID, name (partial match), account-at-match (exact), source pattern (partial match against rule group JSONB), and active-only flag.
- **REQ-CR-5.4** Filtered retrieval must support optional sort ordering by account code (ascending or descending, resolved via the account table) or priority (ascending or descending).
- **REQ-CR-5.5** The returned classification rule must include the resolved account name corresponding to the account at match.
  - *Why:* The CLI display layer needs the human-readable account name without a second round-trip. The rule stores an account ID internally; the boundary layer resolves the name at read time. (2026-08-25)


## 6. Update behaviors

- **REQ-CR-6.1** The system must provide a means to update a classification rule's name, account-at-match, priority, rule groups, and isActive flag. Each field is independently updatable via a FieldUpdate (NoChange or SetTo).
- **REQ-CR-6.2** When updating a classification rule, if all fields are NoChange, the update must fail (no-op rejection).
- **REQ-CR-6.3** When updating the account at match, the system must validate that the new value resolves to an existing account in the chart of accounts. If it does not, the update must fail.
- **REQ-CR-6.4** When updating `ruleGroups`, the system must validate that the new list is not empty and that every field match chain within every rule group is not empty. If either condition fails, the update must fail.
- **REQ-CR-6.5** On successful update, the system must update the `modified_at` timestamp and return the updated rule.


## 7. Deletion behaviors

- **REQ-CR-7.1** The system must not provide a user interface for hard-deleting a classification rule.


## Waived from testing

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-CR-1.1 | UUID is a value type; uniqueness enforced by PK constraint. Same rationale as REQ-AC-1.21/1.22. | Dan, 2026-08-21 |
| REQ-CR-1.2 | Solution won't build if you try to pass a null value to ClassificationRuleName.create. | Dan, 2026-08-21 |
| REQ-CR-1.17 | Solution won't build if you try to pass a null value to StringSearchPattern.create. | Dan, 2026-08-21 |
| REQ-CR-1.10 | chainOne is a non-optional record field — a group cannot be constructed without one. | Dan, 2026-08-21 |
| REQ-CR-1.13 | FieldMatch is a five-case DU — exactly-one targeting is the DU's structural exclusivity. | Dan, 2026-08-21 |
| REQ-CR-1.21 | The only code path that writes this column validates through `Money.fromDecimal`; read-path validation is not performed by design (see Journaling slice precedent). | Dan, 2026-08-21 |
| REQ-CR-4.2 | UUID generation via Guid.NewGuid() in create; uniqueness enforced by PK constraint. Same rationale as REQ-CR-1.1. | Dan, 2026-08-21 |
| REQ-CR-4.8 | A negative existence claim over the entire API surface cannot be proven by a unit test; enforced by code review and periodic adversarial audit. Same rationale as REQ-CR-7.1. | Dan, 2026-08-21 |
| REQ-CR-7.1 | A negative existence claim over the entire API surface cannot be proven by a unit test; enforced by code review and periodic adversarial audit. Same rationale as REQ-AC-5.1. | Dan, 2026-08-21 |
