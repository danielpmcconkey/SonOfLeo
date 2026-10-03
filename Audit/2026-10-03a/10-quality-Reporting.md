# Reporting.md requirements-quality auditor

## RPT-AMB-7.3 — insufficient-elaboration
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-7.3; Src/Business.CrossDomainOrchestration/PrePostingReview.fs ruleNameForLine (doc comment above it)
- **Summary:** REQ-RPT-7.3 does not settle which rule to name. It never says which runs count, how 'evaluated' can be seen, or what happens when several matched rules carry the line's account; the implementer had to invent three rules of their own.
- **Resolution:** fix-spec

REQ-RPT-7.3 says: 'the rule recorded against the line in the most recent classification run that evaluated it (DataIngestion REQ-STG-5.10) whose account is the line's current account.' Three gaps follow from the specs as they now stand.
(1) Which runs count. Payment-agreement linkage is also a classification run recorded under a run ID (REQ-CF-12.3, REQ-CF-12.8), and it evaluates lines that already have an account ('A line's account assignment does not exclude it'). The Saturday order runs linkage after account classification (CashFlow §12 intro). Read literally, 'the most recent run that evaluated it' is therefore the linkage run, which records no account rules, so the field would be empty on almost every line.
(2) 'Evaluated' cannot be seen. REQ-CR-8.2 says 'A line with no match produces no row', so the only runs on record are runs that matched something.
(3) Several candidates. REQ-CR-8.2 records every matching rule, losing and tied rules included. Two rules in the same run can claim the same account at different priorities, yet the spec says 'the rule', singular.
The code's doc comment states the choices the developer made: 'Payment agreement runs are not account classification and are ignored'; it uses 'the most recent run that recorded an account rule against it'; and 'if more than one does, the highest priority wins, then the rule name.' These are reasonable, but they live only in Src. A second developer could easily pick the literal linkage-run reading and leave the field empty, or name a losing rule.

**Action:** Amend REQ-RPT-7.3: consider only runs over account-claimant rules; 'most recent' means the most recent run that recorded an account-claimant match for the line; when more than one matched rule in that run claims the current account, name the one with the highest priority, then the first by rule name.

**Why:** Without this, nothing in the spec stops the rule-name column going silently blank or naming the wrong rule. That column exists so a wrong rule is fixed once (the requirement's own Why), and a test written from the spec could not tell the readings apart.

---

## RPT-AMB-7.4 — ambiguity
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-7.4; Specs/Behavioral/CashFlow.md REQ-CF-14.5, REQ-CF-14.6; DbMigration/Scripts/202609071125-CreateCashFlowTables.sql (payment.stage_entry_line_id has no UNIQUE constraint)
- **Summary:** REQ-RPT-7.4 is written as if at most one Payment references a line, but CashFlow explicitly allows several, so the spec does not say what the review shows in that case.
- **Resolution:** fix-spec

REQ-RPT-7.4: 'When a Payment references the line, the line also carries the Payment's amount, and its Invoice's ... and its Instance's date.' Every noun is singular. CashFlow allows more than one Payment on a line. REQ-CF-14.5 lets the operator 'add a Payment to an existing Invoice', and REQ-CF-14.6 says deleting a Payment deletes its link 'unless another Payment still references the same line'. The schema has no unique constraint on cashflow.payment.stage_entry_line_id, unlike payment_agreement_link, which has payment_agreement_link_line_unique. The code (PrePostingReview.fs, paymentsByLine) returns a list of every Staged-pointer Payment on the line, sorted by instance date, then due date. A developer reading the spec could just as reasonably show only the first Payment, or fail. The sort order is also unspecified.

**Action:** Amend REQ-RPT-7.4 to say the line carries every Payment that references it, each with its Invoice and Instance fields, and state the order (the code uses Instance date, then due date).

**Why:** In the case the report exists to catch, one cash line wrongly applied to two invoices, the singular reading hides the second Payment.

---

## RPT-AMB-6.1 — ambiguity
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-6.1, REQ-RPT-6.3 (vs REQ-RPT-1.5); Src/Business.CrossDomainOrchestration/PeriodActivity.fs fetchPeriodActivity
- **Summary:** Period activity orders accounts like the trial balance (hierarchical) but never says whether parent accounts roll up their descendants, as REQ-RPT-1.5 requires for the trial balance.
- **Resolution:** dan-decides

REQ-RPT-6.3 orders accounts 'as in the trial balance (REQ-RPT-1.6)', which is a depth-first parent-then-children outline. The trial balance rolls parents up recursively (REQ-RPT-1.5). REQ-RPT-6.1 asks only for 'net total for the range' and 'each contributing journal entry line' for every account 'with activity in the range', and says nothing about descendants. Two readings are both reasonable: (a) each account shows only its own lines, and a parent with no lines of its own is absent; (b) a parent's net total includes its descendants (the outline ordering suggests this), so the parent appears whenever any child has activity. The code does (a): it groups lines by the account they sit on and drops accounts with no lines of their own. On a spending view where expenses nest (for example 5300 Utilities > 5311 Water), the two readings give different totals and a different list of accounts.

**Action:** Add one sentence to REQ-RPT-6.1 saying whether the net total is the account's own lines only or rolled up as in REQ-RPT-1.5, and whether a parent with no lines of its own appears.

**Why:** The two readings give different visible output (totals and which rows appear), so the spec describes the WHAT incompletely, not merely the HOW.

---

## RPT-AMB-3-SCOPE — ambiguity
- **Location:** Specs/Behavioral/Reporting.md §3 (REQ-RPT-3.2 to 3.6), REQ-RPT-6.4, REQ-RPT-7.7
- **Summary:** §3's rendering requirements are titled and worded for the trial balance only. REQ-RPT-6.4 and 7.7 extend just REQ-RPT-3.1 (header) to the other rendered reports, leaving footer, print CSS and sign/depth classes unstated for integrity, period activity and pre-posting review.
- **Resolution:** fix-spec

§3 is headed 'Trial balance HTML rendering'. REQ-RPT-6.4 says integrity and period activity 'support the output modes of §2' and that 'the rendered header (REQ-RPT-3.1) shows the range'. REQ-RPT-7.7 says the same for the pre-posting review and its header. Neither says whether REQ-RPT-3.2 (generated-instant footer), 3.5 (print-optimized CSS), 3.4 (sign CSS classes on money) or 3.3 (depth class on account rows; period activity also uses the trial-balance outline) apply. Citing only 3.1 invites the reading that the rest do not. The current writers (BalanceSheetIntegrityWriter, PeriodActivityWriter, PrePostingReviewWriter) do all include createReportFooter() and the base CSS, so today's code and the likely intent agree. A new report writer could still leave them out and remain compliant with the spec as written. The waivers for 3.2 to 3.6 (visual inspection, 2026-08-07) predate these reports, so it is also unclear whether they cover them.

**Action:** Retitle §3 as general HTML rendering and state which of 3.2 to 3.6 apply to every rendered report, or add one line to 6.4 and 7.7 citing the ones that apply.

**Why:** An extension that cites only 3.1 leaves the rest of the rendering contract undefined for three of the four rendered reports.

---

## RPT-SYS34-CLOCK — enforcement-gap
- **Location:** Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs prePostingReview (Calendar.today()); Src/Ui.InterfaceBridge/ReportVisualizationAssets/ReportFooter.fs createReportFooter (Clock.now()); Specs/Behavioral/SystemWide.md REQ-SYS-3.4; Reporting.md REQ-RPT-7.7, REQ-RPT-3.2
- **Summary:** The pre-posting review's run date (REQ-RPT-7.7) and every report's generated instant (REQ-RPT-3.2) read the system clock separately, not from the operation's single initiation instant as REQ-SYS-3.4 requires.
- **Resolution:** fix-code

REQ-SYS-3.4: 'Every operation carries ... a single initiation instant read from the system clock when the operation begins. Every timestamp the operation writes, and every "current date" it derives ..., uses that instant.' The run date in REQ-RPT-7.7 (used for date interpolation and the header) is a derived current date. The footer instant in REQ-RPT-3.2 is a timestamp the operation writes to its output file. The route builds a Context, whose initiation instant is available through Context.getInitiationInstant. It then passes Calendar.today() to PrePostingReviewWriter.write, and Calendar.today() calls Clock.now() again. createReportFooter() also calls Clock.now() on its own. One report can therefore carry a header and file-name date that disagree with its footer instant, and a run near midnight can straddle two dates. That is exactly what REQ-SYS-3.4's Why rules out ('date-dependent logic cannot straddle midnight partway through a run'). Checks/check-clock passes, so this is not detected mechanically.

**Action:** Derive the pre-posting review date and the footer instant from the context's initiation instant (Context.getInitiationInstant, then Calendar.dateFromInstant). Have createReportFooter take the instant as a parameter.

**Why:** The spec (higher authority) says one operation has one moment, and the code here takes three clock reads in one report. It is minor in practice, but it is a real deviation the gate does not catch.

---


