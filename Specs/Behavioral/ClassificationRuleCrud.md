# Classification Rule CRUD

Service-level behavioral specs for creating, reading, and managing classification rules — the pattern-matching engine that proposes a claimant for each staged line during data ingestion. A claimant is either an account (the rule assigns the line's account; DataIngestion.md §5) or a payment agreement (the rule links the line to an obligation; CashFlow.md §12–§13). Cross-cutting policies (string trimming, data-state enforcement, audit timestamps) live in SystemWide.md.

**Design note — evaluation domain.** Classification rules are evaluated in F#, not in SQL. The rule body is stored as JSONB and reconstituted into a typed domain model at read time. This eliminates the SQL injection surface that would exist if patterns were interpolated into queries.

**Design note — authority hierarchy.** Account-claimant classification rules are the second layer in the account-assignment fill order defined in DataIngestion.md: the parser assigns first, and the classifier fills only what the parser left null. Fill order is not authority: the operator is the highest authority and may override any assignment and the deduplicator (DataIngestion.md preamble). (Amended 2026-10-05) Filling only null account assignments, and never overriding a parser assignment, is the account-classification caller's rule (REQ-STG-5.2, REQ-STG-5.3), not the classifier's: the classifier reports which rules matched and does not look at a line's existing account (REQ-CR-3.8). The payment-agreement caller deliberately evaluates lines that already have an account (REQ-CF-12.3).


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
- **REQ-CR-1.25** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
- **REQ-CR-1.6** Classification rule priority is an integer. Lower values represent higher priority — when multiple rules match a candidate, the rule with the lowest priority value wins.
- **REQ-CR-1.7** Classification rule must contain at least one rule group.
- **REQ-CR-1.8** Classification rule has an `isActive` boolean flag. Only active rules participate in classification (REQ-STG-5.2, REQ-CF-12.3, REQ-CR-3.2).

### ClassificationRuleGroup

- **REQ-CR-1.9** A rule group has a connector that must be one of `'And'` or `'Or'`.
- **REQ-CR-1.10** A rule group has a primary chain (`chainOne`) that is required.
- **REQ-CR-1.11** A rule group has an optional secondary chain (`chainTwo`). When `chainTwo` is absent, the connector is unused and the group's match result is `chainOne`'s result alone.

### FieldMatchChain

- **REQ-CR-1.12** A field match chain is a non-empty list of field matches. All field matches in the chain must evaluate to true for the chain to evaluate to true (AND-connected).

### FieldMatch

- **REQ-CR-1.13** A field match targets exactly one of: `Source`, `Description`, `LineType`, or `Amount`. (Amended 2026-10-03 — `Memo` withdrawn as a match target; see REQ-CR-2.2 in the Withdrawn table.)
  - *Why:* Dan ruled Memo out of the match criteria. Amount and LineType stay because they are how a split payment (rent on one line, a utility share on another) matches each line separately (REQ-CF-12.4). (2026-10-03)
- **REQ-CR-1.14** `Source` and `Description` field matches carry a `StringSearchPattern` evaluated as a regex against the candidate's corresponding field value. Matching is case-sensitive and unanchored (the pattern may match anywhere in the value) unless the pattern itself says otherwise. (Clarified 2026-09-26; `Memo` removed 2026-10-03)
- **REQ-CR-1.15** `LineType` field matches carry a `JournalEntryLineType` value and evaluate by exact equality against the candidate's line type.
- **REQ-CR-1.16** `Amount` field matches carry a `MoneySearchPattern` (a `NumericSearchOperator` and a `Money` value) and evaluate by comparing the candidate's amount against the pattern's amount using the specified operator.

### StringSearchPattern

- **REQ-CR-1.17** String search pattern cannot be null.
- **REQ-CR-1.18** String search pattern cannot be empty. Whitespace is not trimmed, and a whitespace-only pattern is legal — neither REQ-SYS-1.1 nor REQ-SYS-1.2 applies, because whitespace is meaningful in regex patterns. (Amended 2026-10-03)
- **REQ-CR-1.19** String search pattern length cannot exceed 500 characters.
- **REQ-CR-1.26** String search pattern must be a valid regular expression. An invalid pattern is rejected with a typed error when the rule is created or updated, and when a stored rule is read. Evaluating a valid pattern never raises an exception. Pattern evaluation is time-limited: a pattern that exceeds the limit fails the whole classification run with a typed error naming the rule. A timeout is never treated as no match. (Amended 2026-10-03)
  - *Why:* The pattern is data from the operator; it is validated at the boundary like any other input, so an invalid one is caught when it is saved rather than discovered mid-run. A valid pattern can still be pathologically slow. Failing the run loudly is the right outcome there: reading a timeout as no match would silently leave lines unclassified, or hand them to a lower-priority rule, with nothing to show why. (2026-09-26, revised 2026-10-03)

### NumericSearchOperator

- **REQ-CR-1.20** Numeric search operator must be one of: `'GreaterThan'`, `'LessThan'`, `'GreaterThanOrEqualTo'`, `'LessThanOrEqualTo'`, `'ExactlyEqual'`.

### MoneySearchPattern

- **REQ-CR-1.21** The amount field within a money search pattern must satisfy all Money data state requirements (REQ-MON-1.*). It is enforced when a rule is created or updated, and again when a stored rule is read (REQ-SYS-2.1), the same as stored text patterns (REQ-CR-1.26). (Amended 2026-10-03)
  - *Why:* Every write path builds the amount through the validating Money conversion, so an invalid stored amount can only come from a direct database edit — an unreachable state for the system. (2026-10-03)


## 2. Rule evaluation behaviors

- **REQ-CR-2.1** A field match evaluates to true when the candidate's field value satisfies the match criterion: regex match for string fields, exact equality for `LineType`, numeric comparison for `Amount`.
- **REQ-CR-2.2** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
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
- **REQ-CR-5.4** Filtered retrieval must support optional sort ordering by account code (ascending or descending, resolved via the account table) or priority (ascending or descending). Under an account-code sort, rules with no account code (payment-agreement claimants) come after every account-claimant rule in both directions, ordered among themselves by rule name. (Amended 2026-10-03) Rules that tie on the sort key — the same account code, or the same priority — are ordered by rule name. With no sort given, the order is unspecified. (Amended 2026-10-05)
- **REQ-CR-5.5** The returned classification rule must identify its claimant in human-readable form: the account's code and name for an account claimant, or the payment agreement's name for a payment agreement claimant.
  - *Why:* The CLI display layer needs the human-readable account name without a second round-trip. The rule stores an ID internally; the boundary layer resolves the name at read time. (2026-08-25, payment agreement claimant added 2026-09-26)


## 6. Update behaviors

- **REQ-CR-6.1** The system must provide a means to update a classification rule's name, claimant, priority, rule groups, and isActive flag. Each field is independently updatable via a FieldUpdate (NoChange or SetTo). Updating the claimant may change its type (account to payment agreement or the reverse); the rule always ends with exactly one claimant (REQ-CR-1.5). (Revised 2026-09-26)
- **REQ-CR-6.2** An update that names no field to change is rejected (REQ-SYS-6.1); a field set to the value it already holds counts as named. (Amended 2026-10-03)
  - *Why:* As REQ-CF-14.2: re-sending a value the rule already holds hides no wrong belief about its state. (2026-10-03)
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
- **REQ-CR-8.5** The system must be able to retrieve all match rows for a run ID. Each returned row includes the rule's name, claimant, and priority as they stand at retrieval time, grouped by staged line ID (the order between lines is unspecified), then sorted by priority, then rule name. (Amended 2026-10-03) A run ID with no rows returns an empty list.
  - *Why:* The match row stores only the rule ID. Renaming, re-pointing, or re-prioritizing a rule after the run therefore changes how an old run reads back; the run records *which* rules matched, not *what they said* at the time. (2026-09-26)


## Waived from testing

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-CR-1.1 | UUID is a value type; uniqueness enforced by PK constraint. Same rationale as REQ-AC-1.21/1.22. | Dan, 2026-08-21 |
| REQ-CR-1.2 | A null name fails loudly: the interface rejects a null or missing name before it reaches the domain, and a null passed to the name constructor throws. Under the no-bubblewrap rule a loud failure needs no test. (Reason restated 2026-10-03; the earlier "won't build" reason was false.) | Dan, 2026-08-21 |
| REQ-CR-1.17 | A null pattern fails loudly: the interface rejects a null or missing pattern before it reaches the domain, and a null passed to the pattern constructor throws. Under the no-bubblewrap rule a loud failure needs no test. (Reason restated 2026-10-03; the earlier "won't build" reason was false.) | Dan, 2026-08-21 |
| REQ-CR-1.10 | chainOne is a non-optional record field — a group cannot be constructed without one. | Dan, 2026-08-21 |
| REQ-CR-1.13 | A field match is one of four mutually exclusive cases — exactly-one targeting is structural. (Four since `Memo` was withdrawn, 2026-10-03.) | Dan, 2026-08-21 |
| REQ-CR-1.21 | The state is unreachable, so it is not tested. Every write path builds the amount through the validating Money conversion, which fails loudly, and an invalid stored amount needs a direct database edit. (Reason restated 2026-10-03, tidied 2026-10-05.) | Dan, 2026-08-21 |
| REQ-CR-4.2 | UUID generation via Guid.NewGuid() in create; uniqueness enforced by PK constraint. Same rationale as REQ-CR-1.1. | Dan, 2026-08-21 |
| REQ-CR-4.8 | A negative existence claim over the entire API surface cannot be proven by a unit test; enforced by code review and periodic adversarial audit. Same rationale as REQ-CR-7.1. | Dan, 2026-08-21 |
| REQ-CR-7.1 | A negative existence claim over the entire API surface cannot be proven by a unit test; enforced by code review and periodic adversarial audit. Same rationale as REQ-AC-5.1. | Dan, 2026-08-21 |
| REQ-CR-8.4 | A negative existence claim over the API surface; the match record type has no update path. Enforced by code review. | Dan 2026-09-26 |


## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
| REQ-CR-1.25 | A rule "constrains line type" when any field match anywhere in any of its rule groups is a `LineType` field match. | Leg selection no longer asks whether a rule constrains line type: the kept lines are the lines the claiming rule matched (REQ-CF-12.4, rewritten 2026-10-03). |
| REQ-CR-2.2 | A `Memo` field match evaluates to false when the candidate's memo is absent (None). | `Memo` is no longer a match target (REQ-CR-1.13). Dan, 2026-10-03: Memo removed from the match criteria. Amount and LineType remain the line-level criteria. |
