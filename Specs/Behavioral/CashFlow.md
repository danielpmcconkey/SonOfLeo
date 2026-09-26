# Cash Flow

Behavioral specs for the cash flow domain — the mechanism by which the system tracks recurring financial obligations from contractual definition through fulfillment. Cash flow entities model the full lifecycle of a predictable cash movement: an agreement that defines what is owed and when, invoices that establish what is owed for a specific period, and payments that track the movement of funds through to ledger posting.

**Design note — Template vs Event.** Every entity type in this domain is exactly one of two categories:
- **Template:** defines the shape and behavior of a recurring obligation. Templates are created once and describe what *will happen* each period. They do not change period to period.
- **Event:** a concrete occurrence of an obligation. Events are instantiated from templates, one set per period, and track what *did happen*.

The template/event split is the organizing principle. Template entities own the "what and how" — accounts, expected amounts, cadences. Event entities own the "when and whether" — dates, actual amounts, lifecycle states. The two sides meet through foreign-key references from event to template; the template never references its events.

**Design note — relationship to the ledger.** Cash flow is an operational layer, not an accounting layer. Under cash-basis accounting, revenue and expense are recognized when cash moves, not when an obligation is incurred. Cash flow entities therefore do not create journal entries at instantiation. A Payment record is created when cash has been identified as having moved — initially linked to a staged entry (the FI export proves the movement), and later linked to a journal entry after posting. Before a Payment exists, the obligation exists for planning, forecasting, and Saturday review — but it has no ledger footprint. This is by design: the ledger records what happened; cash flow tracks what is expected to happen.

**Design note — naming.** Identifiers in this spec (e.g. `master_agreement`, `payment_agreement`, `invoice`) name domain concepts for readability. They do not prescribe variable names, function names, property names, or any other naming convention in the code or tests.

**Design note — one word, one meaning.** This domain introduces "Payment" as a noun (a receipt record proving that cash moved against an invoice). This is distinct from "posting" (the act of creating a journal entry in the ledger) and from "payment state" (a lifecycle dimension on the Invoice entity). "Transaction" is deliberately avoided as an entity name — it is overloaded across financial systems and this codebase already uses it informally for journal entries.

**Design note — diamond relation.** Invoice references both Instance (an event) and Payment Agreement (a template). Instance and Payment Agreement each independently reference Master Agreement. This creates a diamond in the entity graph. The implicit constraint — that an Invoice's Instance and Payment Agreement must belong to the same Master Agreement — is validated by the orchestrator at creation time, consistent with the infallible-create pattern used throughout SonOfLeo. The schema does not enforce this constraint.

**Design note — partial payments.** A single Invoice may have more than one Payment. When an obligation is partially fulfilled (e.g. $70 paid against a $120 invoice), a second Payment record is created for the remainder. The business rule is: the Invoice's payment state cannot transition to 'FullyPaid' unless the sum of all derived Payment amounts equals the Invoice's amount (§9). This is an Invoice lifecycle constraint, not a Payment creation constraint — Payments record facts about cash movement and are not validated against the Invoice amount at creation time. Consistent with the project's philosophy of structural integrity in the schema and business rules in the app layer.


## 1. Entity model

### Master Agreement (Template)

The top-level template. Defines a recurring contractual obligation: its name, the direction of cash flow, the counterparty, the cadence, and the period over which the agreement is active. A Master Agreement does not reference any other cash flow entity.

Examples: a mortgage note, a tenant lease, a utility autopay arrangement, a mobile phone contract.

### Payment Agreement (Template)

A child of Master Agreement. Defines one leg of the payment structure — which accounts to debit and credit, and optionally the expected amount per period. Every Master Agreement has at least one Payment Agreement. Multi-leg arrangements (e.g. a tenant agreement with separate rent and utility-share legs) have one Payment Agreement per leg.

The Payment Agreement serves as the template for how the journal entry is structured when the obligation is fulfilled. Each Payment Agreement maps to one debit/credit pair and produces one journal entry (or one set of lines within a multi-line journal entry) when its corresponding Payment is posted.

References: Master Agreement, Account (debit), Account (credit).

### Instance (Event)

A single period occurrence of a Master Agreement. One Instance is created per cadence period (e.g. monthly for a monthly agreement). The Instance record itself carries little more than the date that identifies this period — its purpose is to group the Invoices and Payments for a single occurrence.

References: Master Agreement.

### Invoice (Event)

The bridge between the template side and the event side. An Invoice is created for each (Instance, Payment Agreement) combination — one per leg, per period. It carries the *actual* amount owed for this period, which may differ from the Payment Agreement's expected amount (utilities vary month to month) or may be identical (fixed-amount obligations like a mortgage).

For receivables, the Invoice corresponds to the physical or electronic invoice sent to the counterparty. For payables, it corresponds to the bill received. In both directions, the Invoice establishes the amount that Payments must sum to.

The Invoice owns the full obligation lifecycle across three independent dimensions: invoice state (has the bill arrived or been sent?), payment state (has the counterparty paid?), and posted state (has the system recorded the cash movement?). A Blocker may be attached to explain why progress has stalled. Invoice state values are direction-dependent: Income invoices use 'InvoiceGenerated' and 'InvoiceSent'; Outgo invoices use 'InvoiceExpected' and 'InvoiceReceived'.

Every (Instance, Payment Agreement) pair should have exactly one Invoice.

References: Instance, Payment Agreement.

### Payment (Event)

A receipt record. A Payment is created when cash has been identified as having moved — either in the staging area (linked to a staged entry) or in the ledger (linked to a journal entry). A Payment always has a transaction pointer: at least one of a staged entry line ID or a journal entry line ID must be present. Both may be present — the staged line is provenance, the journal entry line is current truth. The pointer resolves to one value (Posted takes precedence). A Payment progresses from Staged to Posted as the underlying data moves through the ingestion pipeline.

Payment amount is derived, not stored: it is the amount of the line the transaction pointer references. When both pointers are present, the journal entry line is authoritative.

Every Invoice has zero or more Payments. An Invoice with no Payments is an unfulfilled obligation. Partial payments produce additional Payment records against the same Invoice.

References: Invoice, Transaction Pointer (staged entry line or journal entry line — required).

### Payment Agreement Link

Records that a staged line is the cash movement for a Payment Agreement. Links are created by classification against payment-agreement rules or by the operator (§12), and are the input to invoice matching (§13).

References: Payment Agreement, staged entry line.


## 2. Valid and invalid data states — Master Agreement

- **REQ-CF-2.1** Master Agreement ID cannot be null
- **REQ-CF-2.2** Master Agreement ID must be unique
- **REQ-CF-2.3** Master Agreement name cannot be null
- **REQ-CF-2.4** Master Agreement name cannot be whitespace only (post-trim, per REQ-SYS-1.1)
- **REQ-CF-2.5** Master Agreement name length cannot exceed 100 characters
- **REQ-CF-2.6** Master Agreement name must be unique
- **REQ-CF-2.7** Flow direction must be one of 'Income' or 'Outgo'
- **REQ-CF-2.8** Cadence must be one of: 'Daily', 'Weekly', 'EveryOtherWeek', 'Monthly', 'Annually'
- **REQ-CF-2.9** When cadence is 'Weekly' or 'EveryOtherWeek', a week day value is required
- **REQ-CF-2.10** When cadence is 'Monthly', a month day specification is required. Month day is one of: a date-in-month number (1–28), an nth-weekday-in-month (week number 1–4 paired with a week day), or 'Last' (the last day of the month).
  - *Why:* Date-in-month is capped at 28 because every month has at least 28 days — no clamping logic needed. For "always the last day" semantics, use 'Last'. Week-in-month is capped at 4 because the 5th occurrence of a weekday does not exist in every month. (2026-08-31)
- **REQ-CF-2.11** When cadence is 'Annually', a month and a month day specification are required
- **REQ-CF-2.12** When cadence is 'Daily', no additional cadence fields are required
- **REQ-CF-2.13** Week day must be one of 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'
- **REQ-CF-2.14** Month must be one of 'January' through 'December'
- **REQ-CF-2.15** Date-in-month number must be between 1 and 28 inclusive
  - *Why:* Every month has at least 28 days, so no clamping is needed. Use 'Last' for end-of-month semantics. (2026-08-31, narrowed from 1–31)
- **REQ-CF-2.16** Week-in-month number must be between 1 and 4 inclusive
  - *Why:* Not every month has a 5th occurrence of a given weekday. (2026-08-31, narrowed from 1–5)
- **REQ-CF-2.17** Counterparty cannot be null
- **REQ-CF-2.18** Counterparty cannot be whitespace only (post-trim, per REQ-SYS-1.1)
- **REQ-CF-2.19** Counterparty length cannot exceed 250 characters
- **REQ-CF-2.20** Start date cannot be null. Start date is a Calendar Date.
- **REQ-CF-2.21** End date may be null. When non-null, end date is a Calendar Date and must not be earlier than start date. Equality is permitted (an agreement active for exactly one period).
- **REQ-CF-2.22** Master Agreement memo may be null. When non-null, memo length cannot exceed 2000 characters and cannot be whitespace only (post-trim, per REQ-SYS-1.1).
- **REQ-CF-2.23** When cadence is 'EveryOtherWeek', the start date serves as the phase anchor — the system derives which weeks are on-cycle by counting from the start date.


## 3. Valid and invalid data states — Payment Agreement

- **REQ-CF-3.1** Payment Agreement ID cannot be null
- **REQ-CF-3.2** Payment Agreement ID must be unique
- **REQ-CF-3.3** Payment Agreement must reference a valid Master Agreement ID
- **REQ-CF-3.4** Payment Agreement must reference a valid Account ID for the debit account
- **REQ-CF-3.5** Payment Agreement must reference a valid Account ID for the credit account
- **REQ-CF-3.6** Debit account and credit account must not be the same account
- **REQ-CF-3.7** Expected amount may be null (for variable-amount obligations where the amount is not known until the invoice arrives). When non-null, expected amount must be a valid positive Money value.
- **REQ-CF-3.8** Payment Agreement memo may be null. When non-null, memo length cannot exceed 2000 characters and cannot be whitespace only (post-trim, per REQ-SYS-1.1).


## 4. Valid and invalid data states — Instance

- **REQ-CF-4.1** Instance ID cannot be null
- **REQ-CF-4.2** Instance ID must be unique
- **REQ-CF-4.3** Instance must reference a valid Master Agreement ID
- **REQ-CF-4.4** Instance date cannot be null. Instance date is a Calendar Date.
- **REQ-CF-4.5** Is-fulfilled is a boolean. Default is false.


## 5. Valid and invalid data states — Invoice

- **REQ-CF-5.1** Invoice ID cannot be null
- **REQ-CF-5.2** Invoice ID must be unique
- **REQ-CF-5.3** Invoice must reference a valid Instance ID
- **REQ-CF-5.4** Invoice must reference a valid Payment Agreement ID
- **REQ-CF-5.5** The Invoice's Instance and Payment Agreement must belong to the same Master Agreement
  - *Why:* Diamond-relation consistency. Validated by the orchestrator at creation time. (2026-08-27)
- **REQ-CF-5.6** Invoice amount cannot be null. Invoice amount must be a valid positive Money value.
- **REQ-CF-5.7** Invoice date cannot be null. Invoice date is a Calendar Date.
- **REQ-CF-5.8** Due date cannot be null. Due date is a Calendar Date.
- **REQ-CF-5.9** Invoice state must be one of 'InvoiceGenerated', 'InvoiceSent', 'InvoiceExpected', 'InvoiceReceived'
- **REQ-CF-5.10** Invoice state is constrained by the parent Master Agreement's flow direction. When flow direction is 'Income', invoice state must be 'InvoiceGenerated' or 'InvoiceSent'. When flow direction is 'Outgo', invoice state must be 'InvoiceExpected' or 'InvoiceReceived'.
  - *Why:* Income invoices are generated and sent by the operator. Outgo invoices are expected and received from the counterparty. The two lifecycles are disjoint. (2026-08-27)
- **REQ-CF-5.11** Payment state must be one of 'NotYetPaid', 'PartiallyPaid', 'FullyPaid'
- **REQ-CF-5.12** Posted state must be one of 'NotHandled', 'PartiallyPosted', 'PostedToLedger'
- **REQ-CF-5.13** Blocker state may be null. When non-null, must be one of 'NoFunds', 'Irresponsible', 'NeedsDecision', 'Other'
- **REQ-CF-5.14** Blocker note may be null. When blocker state is 'NeedsDecision' or 'Other', blocker note is required and cannot exceed 500 characters. When blocker state is 'NoFunds' or 'Irresponsible' or null, blocker note must be null.
- **REQ-CF-5.15** Invoice memo may be null. When non-null, memo length cannot exceed 2000 characters and cannot be whitespace only (post-trim, per REQ-SYS-1.1).
- **REQ-CF-5.16** For a given (Instance, Payment Agreement) pair, there must be at most one Invoice
  - *Why:* One Invoice per leg per period. Multiple Invoices for the same leg would create ambiguity about what is owed. (2026-08-27)


## 6. Valid and invalid data states — Payment

- **REQ-CF-6.1** Payment ID cannot be null
- **REQ-CF-6.2** Payment ID must be unique
- **REQ-CF-6.3** Payment must reference a valid Invoice ID
- **REQ-CF-6.4** Payment must have a transaction pointer. At least one of a staged entry line ID or a journal entry line ID must be present. Both may be present: a Payment is created pointing to a staged line, and after posting, the journal entry line ID is added while the staged line ID is retained as provenance. When both are present, the journal entry line (Posted) takes precedence.
  - *Why:* The staged line is provenance (the FI export that proved cash moved). The journal entry line is current truth (the ledger record). Both are worth keeping. Line-level rather than header-level because one entry can satisfy several legs — a split tenant payment carries a rent line and a utility line, each paying a different invoice. (2026-08-28, revised 2026-08-29, moved to line level 2026-09-26)
- **REQ-CF-6.5** Payment amount is derived, not stored. It is the amount of the journal entry line the transaction pointer references when Posted, or of the staged line when Staged.
  - *Why:* The cash amount lives in the ledger/stage data, not duplicated on the Payment record. The Payment is a link, not a copy. A line-level pointer names the leg directly, so no account filtering is needed to find it. (2026-08-29, revised 2026-09-26)
- **REQ-CF-6.6** Posted-to-FI date may be null. When non-null, it is a Calendar Date representing when the financial institution processed the payment.
- **REQ-CF-6.7** Payment memo may be null. When non-null, memo length cannot exceed 2000 characters and cannot be whitespace only (post-trim, per REQ-SYS-1.1).
- **REQ-CF-6.8** *(Withdrawn 2026-08-29 — replaced by REQ-CF-9.1. The sum constraint is an Invoice lifecycle check, not a Payment creation constraint.)*


## 7. Projection sweep

The projection sweep is a deterministic operation that ensures the event side of the entity model is populated for the upcoming planning horizon. Given a horizon (a number of days forward from the current date), the sweep walks every active Master Agreement's cadence and creates any missing Instances and, where amounts are known, their Invoices.

- **REQ-CF-7.1** The projection sweep accepts a horizon parameter expressed as a number of days. The sweep considers all Calendar Dates from the current date through the current date plus the horizon, inclusive.
- **REQ-CF-7.2** A Master Agreement is considered active for the purpose of the sweep when the current date is on or after its start date AND (its end date is null OR its end date is on or after the current date).
  - *Why:* The sweep's active check uses strict temporal gating — both start and end date matter. A future-start agreement is not active for sweeping even though it is available for creation and editing. This is distinct from availability, which allows working with an agreement before its start date. (2026-08-30, reverted from an earlier change that dropped the start-date gate)
- **REQ-CF-7.3** For each active Master Agreement, the sweep must enumerate all cadence dates that fall within the horizon window by walking the agreement's cadence rule forward from the start date.
- **REQ-CF-7.4** When cadence is 'EveryOtherWeek', the sweep derives on-cycle weeks by counting from the start date (the phase anchor, per REQ-CF-2.23).
- **REQ-CF-7.5** *(Withdrawn 2026-08-31 — the clamping scenario is now impossible. DateInMonthNumber is capped at 28 (REQ-CF-2.15) and WeekInMonthNumber at 4 (REQ-CF-2.16), so every cadence date is valid in every month. Use the 'Last' MonthDay variant for end-of-month semantics.)*
- **REQ-CF-7.6** For each cadence date in the horizon window, if no Instance exists for that Master Agreement with a matching instance date, the sweep must create one.
- **REQ-CF-7.7** The sweep must not create duplicate Instances. If an Instance already exists for a given (Master Agreement, instance date) pair, it is skipped.
- **REQ-CF-7.8** For each Instance created or found by the sweep, and for each Payment Agreement belonging to the Instance's Master Agreement: if the Payment Agreement has a non-null expected amount and no Invoice exists for that (Instance, Payment Agreement) pair, the sweep must create an Invoice with the expected amount.
  - *Why:* Fixed-amount obligations (mortgage, rent) have a known amount at template time. Creating the Invoice during the sweep means the cash-flow projection can include them immediately without waiting for a bill to arrive or an invoice to be generated. (2026-08-28)
- **REQ-CF-7.9** For Payment Agreements with a null expected amount (variable obligations), the sweep must not create an Invoice. The Invoice is created later when the actual amount becomes known (bill arrives, invoice generated).
  - *Why:* Creating an Invoice with an unknown amount would be misleading. Variable obligations appear in the projection as "known upcoming, amount TBD" via their Instance alone. (2026-08-28)
- **REQ-CF-7.10** Invoices created by the sweep must set invoice state based on the parent Master Agreement's flow direction: 'InvoiceExpected' for Outgo, 'InvoiceGenerated' for Income.
- **REQ-CF-7.11** Invoices created by the sweep must set payment state to 'NotYetPaid' and posted state to 'NotHandled'.
- **REQ-CF-7.12** The sweep is idempotent. Running it multiple times with the same horizon and current date must produce the same result — no duplicate Instances, no duplicate Invoices, no state changes to existing records.
- **REQ-CF-7.13** The sweep is a deterministic `[DET]` operation. It requires no judgment and makes no classification or matching decisions.


## 8. Cash-flow projection

The cash-flow projection is a read-only, deterministic operation that computes the projected cash position per managed account over the planning horizon. It reads Instances, Invoices, and Payments created by the projection sweep and the obligation routine, and produces the "money you need to move" output.

- **REQ-CF-8.1** The projection computes, per managed cash account, the projected low balance over the horizon window: `projected_low = current_balance + known_inflows − known_outflows`.
- **REQ-CF-8.2** Known inflows are the amounts from Income Invoices due within the horizon window where payment state is not 'FullyPaid'.
- **REQ-CF-8.3** Known outflows are the amounts from Outgo Invoices due within the horizon window where payment state is not 'FullyPaid'.
- **REQ-CF-8.4** Payment agreements on unfulfilled Instances that have no Invoice must be surfaced as known upcoming obligations with unknown magnitude. These are the "bills to chase" — obligations the system knows about but cannot include in the arithmetic. An Instance may carry invoices for some of its payment agreements while others are still missing; each missing agreement is a separate bill to chase.
- **REQ-CF-8.5** The projection is a deterministic `[DET]` operation. It performs arithmetic only and makes no judgment calls.


## 9. Invoice lifecycle constraints

The Invoice owns three independent lifecycle dimensions — invoice state, payment state, and posted state — plus an optional blocker. Invoice state and blocker are set by the operator; any valid value may be set directly (e.g. an Outgo invoice may be created as 'InvoiceReceived' without passing through 'InvoiceExpected'). Payment state and posted state are derived from the Invoice's Payments (REQ-CF-9.8 through REQ-CF-9.10) and cannot be set by any caller. (Amended 2026-09-26)

These constraints are validated by the orchestrator after any Invoice creation or update: persist the new state, fetch the resulting composite (Invoice + its Payments), validate the composite, rollback on failure. The diamond-relation constraint (REQ-CF-5.5) is also validated as part of this composite check.

- **REQ-CF-9.1** Payment state cannot be 'FullyPaid' unless the sum of all derived Payment amounts for the Invoice equals the Invoice's amount.
  - *Why:* "Fully paid" is an assertion that the obligation is satisfied. The arithmetic must confirm it. (2026-08-29)
- **REQ-CF-9.2** Posted state cannot be 'PostedToLedger' unless payment state is 'FullyPaid'.
  - *Why:* The obligation cannot be fully posted to the ledger if it hasn't been fully paid. (2026-08-29)
- **REQ-CF-9.3** Payment state cannot be 'FullyPaid' while blocker state is non-null.
  - *Why:* A blocker records an unresolved obstruction. If the obligation is fully paid, the obstruction is resolved — clear the blocker first. (2026-08-29)
- **REQ-CF-9.4** Blocker state cannot be set to a non-null value while payment state is 'FullyPaid'.
  - *Why:* The reverse of REQ-CF-9.3. If it's fully paid, there is nothing to block. (2026-08-29)
- **REQ-CF-9.5** Payment state cannot be 'PartiallyPaid' unless at least one Payment exists for the Invoice.
  - *Why:* "Partially paid" requires evidence of at least one cash movement. (2026-08-29)
- **REQ-CF-9.6** Posted state cannot be 'PostedToLedger' unless all Payments for the Invoice have a journal entry header ID (i.e. all transaction pointers resolve to Posted).
  - *Why:* Full ledger posting means every Payment has been promoted from staging to the ledger. A Payment still pointing only at a staged entry is not posted. (2026-08-29)
- **REQ-CF-9.7** Posted state cannot be 'PartiallyPosted' unless at least one Payment for the Invoice has a journal entry header ID.
  - *Why:* "Partially posted" requires evidence that at least one Payment has reached the ledger. (2026-08-29)
- **REQ-CF-9.8** Payment state is derived: 'NotYetPaid' when the Invoice has no Payments; 'FullyPaid' when the sum of its Payment amounts equals the Invoice amount; otherwise 'PartiallyPaid'.
- **REQ-CF-9.9** Posted state is derived: 'NotHandled' when no Payment is Posted; 'PostedToLedger' when every Payment is Posted and payment state is 'FullyPaid'; otherwise 'PartiallyPosted'.
- **REQ-CF-9.10** Any operation that creates, re-points, or removes a Payment must re-derive the payment state and posted state of the affected Invoice, and the is-fulfilled flag of its Instance, in the same transaction. An Instance is fulfilled when it has at least one Invoice and every Invoice is 'FullyPaid'.
  - *Why:* Derived state that is recomputed only by some operations drifts. Every Saturday decision (bills to chase, cash coverage, what still needs matching) reads these three values. (2026-09-26)
- **REQ-CF-9.11** A caller-supplied value for payment state, posted state, or is-fulfilled is rejected with a typed error.


## 10. Payment-to-posted transition

A deterministic batch operation that runs after staged entries have been posted to the ledger (Phase 7 of the Saturday routine). It ensures that Payments tracking cash movement through staging are updated to reflect the corresponding journal entries, and that Invoice posted states are updated accordingly.

- **REQ-CF-10.1** The transition identifies all Payments whose transaction pointer resolves to Staged (staged line ID present, journal entry line ID absent). (Revised to line level 2026-09-26)
- **REQ-CF-10.2** For each such Payment, the transition checks whether the staged line has been posted — that is, whether it records the journal entry line it produced (REQ-STG-9.10).
- **REQ-CF-10.3** When the staged line records a journal entry line, the transition sets the Payment's journal entry line ID. The staged line ID is retained as provenance.
- **REQ-CF-10.4** After updating Payments, the transition updates each affected Invoice's posted state as appropriate, subject to the lifecycle constraints in §9.
- **REQ-CF-10.5** The transition is idempotent. Running it multiple times produces the same result.
- **REQ-CF-10.6** The transition is a deterministic `[DET]` operation.
- **REQ-CF-10.7** The transition returns the list of updated Payments with their agreement name, invoice amount, and journal entry line ID.
  - *Why:* The caller (Hobson) needs this for the Saturday summary's review stack without re-querying. (2026-08-29)


## 11. Staged entry match candidates

*Withdrawn 2026-09-26 — superseded by payment agreement linkage (§12) and invoice matching (§13). See the Withdrawn table.*

- **REQ-CF-11.1** *(Withdrawn 2026-09-26 — see §12–§13.)*
- **REQ-CF-11.2** *(Withdrawn 2026-09-26 — see §12–§13.)*
- **REQ-CF-11.3** *(Withdrawn 2026-09-26 — see §12–§13.)*
- **REQ-CF-11.4** *(Withdrawn 2026-09-26 — see §12–§13.)*


## 12. Payment agreement linkage

Linkage answers "which staged line is the cash movement for which Payment Agreement." It runs against staged data before posting, so obligation matching is settled while everything is still correctable. It runs after account classification and after any line splits (REQ-STG-6.4), because leg selection reads line accounts.

- **REQ-CF-12.1** A Payment Agreement Link carries: a system-generated ID, a Payment Agreement ID, a staged line ID, and created/modified Instants.
- **REQ-CF-12.2** A staged line may be linked to at most one Payment Agreement.
  - *Why:* One leg, one obligation. A cash movement that serves two obligations is split into two lines first (REQ-STG-6.4), and each line is linked separately. (2026-09-26)
- **REQ-CF-12.3** The system must provide a means to classify staged lines against the active rules whose claimant is a Payment Agreement, and to create links from the result. Candidates are the lines of staged entries with status `'Ingested'`, `'Classified'`, `'NoMatch'`, `'Conflict'` or `'Reviewed'` that are not already linked. A line's account assignment does not exclude it.
  - *Why:* Account assignment and obligation linkage are independent questions about the same line. `'Reviewed'` is included because splitting a line (REQ-STG-6.4) is an operator review action and must happen before linkage — a split tenant payment is exactly the entry that most needs linking. (2026-09-26)
- **REQ-CF-12.4** Leg selection. A rule matches an entry's description, source and amount, none of which distinguishes one line of the entry from another. For each (Payment Agreement, staged entry) claim, the claimed line is chosen as follows: when any claiming rule constrains line type, the lines that rule matched are kept; otherwise the kept line is the one on the Payment Agreement's credit account with line type Credit (Income agreements) or on its debit account with line type Debit (Outgo agreements). Exactly one kept line proceeds to REQ-CF-12.5. Zero or more than one kept line means no link, and the claim is reported with the reason.
  - *Why:* The rule author knew something the direction default doesn't — an Outgo agreement taking a refund matches a Credit line, which the default would discard. (2026-09-26)
- **REQ-CF-12.5** Resolution is by Payment Agreement, not by line. A Payment Agreement claimed by exactly one line, where that line's claim is not a tie between rules of equal priority, is linked. A Payment Agreement claimed by more than one line, or by any tied claim, is not linked, and every claimant is reported as contested.
  - *Why:* The dangerous case is one obligation claimed by two cash movements — two $150 lines both claiming the water bill looks like a paid bill and a spare $150. One line ambiguously matching two obligations is merely inconvenient. Code never breaks a tie. (2026-09-26)
- **REQ-CF-12.6** Linkage does not change any staged entry's status.
- **REQ-CF-12.7** The system must provide a means for the operator to create a link, re-point a link to a different Payment Agreement, and delete a link. Creating a link for a line that is already linked is rejected with a typed error naming the existing link's Payment Agreement.
- **REQ-CF-12.8** The classification run's matches, including the rules that matched each line and their priorities, are recorded under a run ID and retrievable by that ID.
  - *Why:* The operator reviewing a contested claim needs to see what matched, not re-run the classifier. (2026-09-26)


## 13. Invoice matching

Matching turns links into Payments. It runs in the same operation as linkage (§12), immediately after links are written.

- **REQ-CF-13.1** Candidate invoices are those on unfulfilled Instances whose payment state is not 'FullyPaid' and whose Payments do not already exceed the Invoice amount. They are considered in order of due date, oldest first.
  - *Why:* The oldest bill gets first claim on a line two invoices could both take. Fetch order must never decide it. An overpaid invoice is excluded so it doesn't absorb further payments. (2026-09-26)
- **REQ-CF-13.2** A linked line is a candidate for an Invoice when: it is linked to the Invoice's Payment Agreement; no Payment already references it; no earlier Invoice in this run claimed it; and its entry date falls between the Invoice date minus the grace period and the due date plus the grace period, inclusive.
- **REQ-CF-13.3** The grace period is derived from the Master Agreement's cadence: Daily 0 days, Weekly 2, EveryOtherWeek 4, Monthly 7, Annually 7.
- **REQ-CF-13.4** When an Invoice has exactly one candidate line, a Payment is created against it with a Staged pointer to that line, and the line is claimed.
- **REQ-CF-13.5** When an Invoice has more than one candidate line, no Payment is created for it and the Invoice is reported with every candidate.
- **REQ-CF-13.6** When a Payment brings an Invoice's paid total above its amount, the Invoice is reported as an overpayment. The system takes no ledger action and creates no further records.
  - *Why:* Under GAAP the excess is a credit that needs a ledger-level treatment this system does not model yet. The operator decides. (2026-09-26)
- **REQ-CF-13.7** When a linked line is not a candidate for any Invoice in the run — no Invoice would even consider it — the operation fails and rolls back in full. It must not create an Instance or Invoice to absorb the line. The error must identify every such line and its Payment Agreement, not only the first.
  - *Why:* A linked line with nowhere to go means an upstream gap: the sweep did not run far enough, a cadence is wrong, or a bill has not been entered. Creating records on the fly masks it. Naming every orphan at once means one fix cycle, not one per orphan. (2026-09-26)
- **REQ-CF-13.8** A line that was a candidate and lost (REQ-CF-13.5) is not an orphan under REQ-CF-13.7.
- **REQ-CF-13.9** The linkage-and-matching operation returns: the classification run ID; every link created; every claim not linked, with its reason (REQ-CF-12.4, REQ-CF-12.5); every Payment created; every Invoice with multiple candidates; every overpayment; and the open Instances after matching.
  - *Why:* This result is the Saturday review stack for obligations. Everything the operator must decide is in it; nothing requires a second query. (2026-09-26)

## Withdrawn

| ID | Reason | Date |
|---|---|---|
| REQ-CF-6.8 | Replaced by REQ-CF-9.1. The sum constraint is an Invoice lifecycle check, not a Payment creation constraint. | 2026-08-29 |
| REQ-CF-7.5 | Clamping scenario eliminated by type constraints. DateInMonthNumber capped at 28 (REQ-CF-2.15), WeekInMonthNumber capped at 4 (REQ-CF-2.16). 'Last' handles end-of-month. | 2026-08-31 |
| REQ-CF-11.1 | Superseded by §12–§13. The operator no longer searches for candidates per obligation; linkage and matching produce them in batch. | 2026-09-26 |
| REQ-CF-11.2 | Superseded by REQ-CF-12.4 and REQ-CF-13.2. | 2026-09-26 |
| REQ-CF-11.3 | Superseded by §12–§13, which do create links and Payments. | 2026-09-26 |
| REQ-CF-11.4 | Superseded by §12–§13. | 2026-09-26 |

## Waived from testing

| ID | Reason | Approved |
|---|---|---|
| *(none yet)* | | |

## Unenforceable

| ID | Why | Approved |
|---|---|---|
| *(none yet)* | | |
