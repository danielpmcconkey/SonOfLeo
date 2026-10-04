# ClassificationRuleCrud spec-quality auditor

## WAIVE-CR-1.21 — stale-ruling
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md — REQ-CR-1.21 and its row in the Waived table
- **Summary:** The waiver for REQ-CR-1.21 says no test can provoke the read-side rejection and that the write side needs no test. Both claims are wrong: this suite already provokes the same kind of unreachable stored state by editing the database directly, and the write-side rejection can be reached through the interface.
- **Resolution:** dan-decides

Waiver reason (restated 2026-10-03): "Every write path builds the amount through the validating Money conversion, and the read path re-checks it. An invalid stored amount needs a direct database edit, which is an unreachable state, so no test can provoke the read-side rejection."

(1) Tests in this spec already reach equivalent states with direct database edits inside a rolled-back transaction. Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationClaimantsAndRuns.fs:227 (REQ-CR-1.24) inserts a classification_rule row with both claimants set, or neither, using raw SQL. Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:866 (REQ-CR-1.26) rewrites rule_groups with an update statement so the stored pattern becomes an invalid regex. Its comment reads: "A stored pattern can only turn invalid by being written outside the application, so the test does exactly that." REQ-CR-1.21's own text ties itself to REQ-CR-1.26: "the same as stored text patterns (REQ-CR-1.26)". Yet 1.26's read side is tested and 1.21's is waived on the stated ground that such a test is impossible. The read path does re-check the amount (ClassificationRule.fs fromStoredFieldMatch "Amount" arm, Money.fromDecimal), so a test would exercise real code.

(2) The waiver also covers the write side, which REQ-CR-1.21 states first: "It is enforced when a rule is created or updated". That behaviour is reachable through the interface. An Amount field match carrying 1.005 goes through ClassificationFieldConverters `convert [FieldMatchContract] to [FieldMatch]`, which calls Money.fromDecimal. The waiver's reason ("every write path builds the amount through the validating Money conversion") restates the behaviour under test; it is not a reason the behaviour cannot be tested. No test cites REQ-CR-1.21 (grep of Tests/ finds none). Nothing checks that a rule create or update rejects an invalid amount, as opposed to Money.fromDecimal on its own.

No ruling on REQ-CR-1.21 appears in resolved-findings.md.

**Action:** Remove REQ-CR-1.21 from the Waived table. Add tests (a) creating and updating a rule with an Amount pattern of more than 2 decimal places, rejected through the route; and (b) a direct-edit read test modelled on the REQ-CR-1.26 stored-pattern test. If Dan wants to keep a waiver for the read side alone, restate the reason without the false claim that no test can provoke it.

**Why:** Under the three-state rule an active requirement is waived only when testing it is genuinely impractical or meaningless. Here a waiver reason that the suite's own practice contradicts leaves a reachable, operator-facing validation (create or update with a bad amount) with no test at all.

---

## CON-CR-1.18-SYS — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-1.18 vs Specs/Behavioral/SystemWide.md REQ-SYS-1.1, REQ-SYS-1.2
- **Summary:** REQ-CR-1.18 exempts search patterns from REQ-SYS-1.1 and 1.2, but SystemWide states both rules for "all" raw string inputs and every required text field, and has no mechanism for an entity spec to exempt a field.
- **Resolution:** fix-spec

REQ-SYS-1.1: "All raw string inputs must be trimmed of leading and trailing white space at the system boundary". REQ-SYS-1.2: a required text field "may never hold a value that is empty or whitespace-only post-trim." REQ-CR-1.18 (amended 2026-10-03): "Whitespace is not trimmed, and a whitespace-only pattern is legal — neither REQ-SYS-1.1 nor REQ-SYS-1.2 applies." SystemWide does allow entity-level exceptions for no-op rejection: REQ-SYS-6.1.1 says an exception "must be stated explicitly in the relevant entity spec". No such clause exists for §1. Read on its own, SystemWide still says every string input is trimmed at the boundary. A developer who builds a boundary-level trim, as REQ-SYS-1.1 describes, would silently break REQ-CR-1.18.

The code follows REQ-CR-1.18 (StringSearchPattern.create in ClassificationComponent.fs does not trim). It also shows the friction: the REQ-CR-5.3 source-pattern filter text goes through JournalRefFinancialInstitution.create, which trims and caps at 100 characters, while the patterns it searches are untrimmed and may run to 500 characters. That is consistent with SYS-1.1 but not with the reasoning behind CR-1.18. The spec does not say which rule governs the filter's text. Dan's decision is clearly stated in CR-1.18, so this is about the higher-level spec not acknowledging it, not about which rule wins.

**Action:** Add a clause to SystemWide §1, parallel to REQ-SYS-6.1.1, that allows an entity spec to exempt a named field from REQ-SYS-1.1/1.2 when it states the exemption explicitly, citing REQ-CR-1.18 as the instance. Optionally, have REQ-CR-5.3 say whether the source-pattern filter's text is trimmed.

**Why:** Cross-cutting policies in SystemWide are meant to be complete statements that entity specs can rely on. A universal "All" that an entity spec quietly overrides makes SystemWide wrong for anyone reading it alone, and leaves boundary-level code at risk of breaking the exemption.

---

## AMB-CR-5.4 — insufficient-elaboration
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-5.4 (and REQ-CR-5.3 filtered retrieval)
- **Summary:** REQ-CR-5.4 sets a tie-break by rule name for payment-agreement rules under an account-code sort, but says nothing about ties between rules sharing one account code, ties at the same priority, or the order when no sort is given.
- **Resolution:** dan-decides

REQ-CR-5.4: "Under an account-code sort, rules with no account code (payment-agreement claimants) come after every account-claimant rule in both directions, ordered among themselves by rule name." Several account-claimant rules commonly claim the same account, for example many merchant rules for one expense account. Their relative order is unstated, as is the order of rules sharing a priority value, and REQ-CR-5.4 does not say what order applies when no sort is requested. The implementation (ClassificationOrchestration.fetchRulesFiltered) orders only by `a.code` or by `cr.priority`, so ties come back in database order. The existing test at Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:536 asserts only that tied rules are "adjacent", which confirms the tie order is undetermined. Other specs state this choice explicitly. REQ-AC-3.12.4 says "With no sort specified, order is unspecified." This spec's own REQ-CR-8.5 says "the order between lines is unspecified" and tie-breaks the rest by rule name. Two reasonable implementers would diverge: one returns ties in database order, another breaks them by rule name, which is unique under REQ-CR-1.22. The CLI user would see different output.

**Action:** Amend REQ-CR-5.4 to state the tie-break for rules sharing an account code or a priority (e.g. rule name, as for payment-agreement rules and REQ-CR-8.5), or to declare tie order unspecified, and to state the order when no sort is given.

**Why:** The spec already decided one tie-break in this same sentence. Leaving the analogous and more common ties open means the observable output depends on how the database happens to order rows, and a test cannot assert a full ordering.

---

