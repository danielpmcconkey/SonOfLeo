# Classification Rule CRUD

Service-level behavioral specs for creating, reading, and managing classification rules — the pattern-matching engine that proposes a claimant for each staged line during data ingestion. A claimant is either an account (the rule assigns the line's account; DataIngestion.md §5) or a payment agreement (the rule links the line to an obligation; CashFlow.md §12–§13). Cross-cutting policies (string trimming, data-state enforcement, audit timestamps) live in SystemWide.md.

**Design note — evaluation domain.** Classification rules are evaluated in F#, not in SQL. The rule body is stored as JSONB and reconstituted into a typed domain model at read time. This eliminates the SQL injection surface that would exist if patterns were interpolated into queries.

**Design note — authority hierarchy.** Account-claimant classification rules occupy the middle tier of the account-assignment authority hierarchy defined in DataIngestion.md: parser (highest) > classifier > operator (lowest, but can override all). The classifier only fills null account assignments; it never overrides parser assignments (REQ-STG-5.3).


## 1. Valid and invalid data states for the ClassificationRule type and related types

### ClassificationRule

- **REQ-CR-1.1** Classification rule ID is a system-generated UUID. Cannot be null. Must be unique.
- **REQ-CR-1.2** Classification rule name cannot be null.
- **REQ-CR-1.3** Classification rule name cannot be whitespace only (post-trim per REQ-SYS-1.1).
- **REQ-CR-1.4** Classification rule name length cannot exceed 250 characters.
- **REQ-CR-1.22** Classification rule name must be unique across all classification rules.
  - *Why:* Rules are fetched by name (REQ-CR-5.2). Duplicate names would make the single-result fetch ambiguous. Enforced by a unique constraint in the database. (2026-08-25)
- **REQ-CR-1.5** Classification rule must have exactly one claimant: either an account (foreign key to `ledger.account`) or a payment agreement (foreign key to `cashflow.payment_agreement`). The claimant must exist at creation time and at update time. (Revised 2026-09-26 — previously account only.)
  - *Why:* One rule engine serves two questions — which account a line belongs to, and which obligation a line pays. A rule answers exactly one of them. (2026-09-26)
- **REQ-CR-1.23** A rule's claimant type is `'AccountClaimant'` when its claimant is an account and `'PaymentAgreementClaimant'` when its claimant is a payment agreement.
- **REQ-CR-1.24** A stored rule with both or neither claimant set is invalid; reading it fails with a typed error identifying the rule.
- **REQ-CR-1.25** A rule "constrains line type" when any field match anywhere in any of its rule groups is a `LineType` field match.
  - *Why:* Payment-agreement leg selection (REQ-CF-12.4) defers to a rule that constrains line type instead of applying the direction default. The test is "present anywhere", not "present on every path": a rule whose `'Or'` group has one chain without a `LineType` match still counts as constraining. (2026-09-26)
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
- **REQ-CR-1.14** `Source`, `Description`, and `Memo` field matches carry a `StringSearchPattern` evaluated as a regex against the candidate's corresponding field value. Matching is case-sensitive and unanchored (the pattern may match anywhere in the value) unless the pattern itself says otherwise. (Clarified 2026-09-26)
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
- **REQ-CR-3.4** When exactly one active rule matches a candidate, the outcome is `OneMatch` carrying the matching rule's claimant (account ID or payment agreement ID), rule ID, and priority. (Revised 2026-09-26)
- **REQ-CR-3.5** When multiple active rules match and one has a strictly lower priority value than all others, the outcome is `ManyMatchesClearWinner` carrying the winner and the full list of matches.
- **REQ-CR-3.6** When multiple active rules match and two or more share the lowest priority value, the outcome is `ManyMatchesTied` carrying all matches.
- **REQ-CR-3.7** Each classification run evaluates rules of one claimant type only: account classification uses only active account-claimant rules, and payment-agreement classification uses only active payment-agreement-claimant rules.
  - *Why:* Priority resolution does not distinguish claimant types. Mixing them would produce false ties between an account rule and an obligation rule that answer different questions. (2026-09-26)
- **REQ-CR-3.8** The classifier does not consider whether a candidate line already has an account or a link. It reports which rules matched; deciding what to do with the result belongs to the caller (REQ-STG-5.3, REQ-CF-12.3).


## 4. Create behaviors

- **REQ-CR-4.1** The system must provide a means to create a new classification rule.
- **REQ-CR-4.2** When creating a classification rule, the system must generate a unique UUID for the ID (new UUIDs may not be passed in).
- **REQ-CR-4.3** When creating a classification rule, the system must validate that the claimant resolves to an existing account (supplied by account code) or an existing payment agreement (supplied by name). If it does not, the creation must fail. (Revised 2026-09-26)
- **REQ-CR-4.4** New classification rules are always created as active (`isActive = true`).
- **REQ-CR-4.8** The system must not provide a mechanism to create a classification rule in an inactive state.
- **REQ-CR-4.5** On successful creation, the system must persist the rule and return the fully constructed classification rule with its generated ID and timestamps.
- **REQ-CR-4.6** When creating a classification rule, the system must validate that the rule groups list is not empty. If it is, the creation must fail.
- **REQ-CR-4.7** When creating a classification rule, the system must validate that every field match chain within every rule group is not empty. If any chain is empty, the creation must fail.


## 5. Read behaviors

- **REQ-CR-5.1** The system must be able to retrieve a classification rule by its ID.
- **REQ-CR-5.2** The system must be able to retrieve a classification rule by its name (exact match).
- **REQ-CR-5.3** The system must be able to retrieve classification rules by a combination of optional filter criteria: rule ID, name (case-sensitive partial match), account claimant (by account code, exact), payment agreement claimant (by payment agreement name, exact), claimant type, source pattern (case-sensitive partial match against the text of any `Source` field match in the rule), and active-only flag. (Revised 2026-09-26)
- **REQ-CR-5.6** A filter naming an account code, payment agreement name, or claimant type that does not resolve fails with a typed error.
- **REQ-CR-5.4** Filtered retrieval must support optional sort ordering by account code (ascending or descending, resolved via the account table) or priority (ascending or descending).
- **REQ-CR-5.5** The returned classification rule must identify its claimant in human-readable form: the account's code and name for an account claimant, or the payment agreement's name for a payment agreement claimant.
  - *Why:* The CLI display layer needs the human-readable account name without a second round-trip. The rule stores an ID internally; the boundary layer resolves the name at read time. (2026-08-25, payment agreement claimant added 2026-09-26)


## 6. Update behaviors

- **REQ-CR-6.1** The system must provide a means to update a classification rule's name, claimant, priority, rule groups, and isActive flag. Each field is independently updatable via a FieldUpdate (NoChange or SetTo). Updating the claimant may change its type (account to payment agreement or the reverse); the rule always ends with exactly one claimant (REQ-CR-1.5). (Revised 2026-09-26)
- **REQ-CR-6.2** When updating a classification rule, if all fields are NoChange, the update must fail (no-op rejection).
- **REQ-CR-6.3** When updating the claimant, the system must validate that the new value resolves to an existing account or payment agreement. If it does not, the update must fail. (Revised 2026-09-26)
- **REQ-CR-6.4** When updating `ruleGroups`, the system must validate that the new list is not empty and that every field match chain within every rule group is not empty. If either condition fails, the update must fail.
- **REQ-CR-6.5** On successful update, the system must update the `modified_at` timestamp and return the updated rule.


## 7. Deletion behaviors

- **REQ-CR-7.1** The system must not provide a user interface for hard-deleting a classification rule.


## 8. Classification runs

Every classification run leaves a durable record of what matched, so an operator can review a contested or surprising outcome without re-running the classifier.

- **REQ-CR-8.1** Each classification run is assigned a system-generated run ID, returned to the caller.
- **REQ-CR-8.2** For every candidate line, the run records one match row per rule that matched it — including losing and tied rules, not only the winner. A line with no match produces no row. Each row carries: a system-generated ID, the run ID, the staged line ID, the rule ID, and the run's Instant.
- **REQ-CR-8.3** A given (run, staged line, rule) combination is recorded at most once.
- **REQ-CR-8.4** Match rows are a historical record. The system provides no means to update or delete them.
- **REQ-CR-8.5** The system must be able to retrieve all match rows for a run ID. Each returned row includes the rule's name, claimant, and priority as they stand at retrieval time, sorted by staged line, then priority, then rule name. A run ID with no rows returns an empty list.
  - *Why:* The match row stores only the rule ID. Renaming, re-pointing, or re-prioritizing a rule after the run therefore changes how an old run reads back; the run records *which* rules matched, not *what they said* at the time. (2026-09-26)


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
| REQ-CR-8.4 | A negative existence claim over the API surface; the match record type has no update path. Enforced by code review. | *pending Dan* |
