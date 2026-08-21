# Classification Rules — Business Brief for BD

## What problem this solves

When Dan imports bank transactions, each transaction needs to be assigned to an account in the chart of accounts. A DoorDash charge goes to Food (F-5350). An REI purchase goes to Entertainment (F-5650). A paycheck gets split across ten accounts.

Some transactions arrive pre-assigned — the parser that reads the bank's CSV knows exactly where every leg goes (payroll splits, mortgage payments, rent receipts). Those don't need classification.

The rest arrive with null account codes on some or all lines. The classification rules engine fills those gaps automatically by pattern-matching against the transaction data.

## What a classification rule is

A rule says: "when a staged transaction line looks like *this*, assign it to *that* account."

Each rule has:
- A **name** — human label, e.g. "Source = TestBank && Desc = DoorDash then 5350"
- A **code-at-match** — the account code to assign when the rule matches (e.g. "F-5350")
- A **priority** — lower number wins. When two rules both match a transaction, the one with the lower priority value takes it
- An **active flag** — inactive rules are ignored during classification
- **Rule groups** — the matching logic (see below)

## How matching works — the four layers

Matching is hierarchical. From bottom to top:

1. **FieldMatch** — a single comparison against one field of the transaction line. Five field types:
   - *Source* — regex against the financial institution name (e.g. "TestBank")
   - *Description* — regex against the transaction description (e.g. "^DoorDash")
   - *Memo* — regex against the line memo (if the line has no memo, the match fails)
   - *LineType* — exact match against Debit or Credit
   - *Amount* — numeric comparison against the dollar amount (greater than, less than, equal, etc.)

2. **FieldMatchChain** — a list of FieldMatches connected by AND. Every match in the chain must be true for the chain to be true. Example: "Source matches TestBank AND Description matches ^DoorDash" — both must hit.

3. **ClassificationRuleGroup** — two chains joined by a connector (AND or OR). If there's only one chain, the connector is irrelevant and the group's result is just that chain's result. If there are two chains with AND, both must match. With OR, either one matching is enough.

4. **ClassificationRule** — a list of groups, all connected by AND. Every group must match for the rule to fire. This is the top level.

In practice most rules are simple — one group with one chain of one or two field matches ("source is TestBank and description starts with DoorDash"). The layering exists for the occasional complex case.

## What the classifier does with the results

The classifier runs all active rules against each unclassified line and produces one of four outcomes:

- **NoMatch** — no rule matched. The staged entry goes to `NoMatch` status for manual review.
- **OneMatch** — exactly one rule matched. The line gets the rule's account code.
- **ManyMatchesClearWinner** — multiple rules matched, but one has a strictly lower priority number. That one wins. The line gets the winner's account code.
- **ManyMatchesTied** — multiple rules matched at the same priority. The staged entry goes to `Conflict` status for manual review.

## Where this sits in the pipeline

The full data ingestion flow is:

1. Parser produces a JSONL file (outside SonOfLeo)
2. Ingestion reads the file, creates staged entries with status `Ingested`
3. Deduplication checks for duplicates
4. **Classification runs against `Ingested` entries** — this is where the rules engine does its work
5. Manual review for anything the classifier couldn't resolve
6. Batch post to the ledger

The classifier only touches lines with null account codes. Parser-assigned codes are never overridden (authority hierarchy: parser > classifier > operator).

## CRUD operations

- **Create** — new rules are always active. The account code is validated against the chart of accounts. Rule groups and chains must be non-empty.
- **Read** — by ID, by name, or filtered (partial name match, account code, source pattern, active-only flag) with optional sort.
- **Update** — any combination of name, code-at-match, priority, rule groups, and isActive via a FieldUpdate pattern (each field is independently NoChange or SetTo). Account code and rule groups are re-validated on update. All-NoChange is rejected as a no-op.
- **No delete** — rules are deactivated, never hard-deleted.

## The spec

Everything above is formalized in `Specs/Behavioral/ClassificationRuleCrud.md` with REQ IDs under the `CR` domain. That's the authoritative source for test writing.
