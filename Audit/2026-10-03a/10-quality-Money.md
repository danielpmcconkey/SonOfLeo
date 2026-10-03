# money-spec-auditor

## MON-AMB-1 — ambiguity
- **Location:** Specs/Behavioral/Money.md REQ-MON-2.1 / 2.1.1 / 2.7.1 / 2.8
- **Summary:** REQ-MON-2.1 does not say whether comparing Money values (equality, ordering, sign tests) counts as 'operating on' Money, and in practice agent developers have split: some unwrap to decimal to compare, some compare Money directly.
- **Resolution:** dan-decides

REQ-MON-2.1 says functions that 'operate on or with' Money-definition values 'must only take explicit Money type arguments and must only return explicit Money type values'. The only stated exceptions are boundary functions (2.1.1) and the multiply/divide path (2.7.1: unwrap to decimal, do the math, convert back). Money.md specifies no comparison or sign operation, and Src/Business.FinancialServices/Money.fs has none (only fromDecimal, fromDecimalList, splitByN, add, subtractVal1FromVal2, sumList, amount and the string formatters). Non-boundary business code has gone two ways. (a) It unwraps with Money.amount and compares decimals: StageEntryOrchestration.fs:75-76 (`amountDec <= 0M`), JournalEntryLineOrchestration.fs:16 (`Money.amount <= 0M`), AgreementOrchestration.fs:126 (`Money.amount > 0M`), InstanceOrchestration.fs:134-135 (`invoiceAmountDecimal > 0M`), InstanceOrchestration.fs:529-533 (derivePaymentState: `paidDecimal = invoiceDecimal`), AccountDeactivation.fs:76, BalanceSheetIntegrity.fs:53, Classification/FieldMatch.fs:28-34 (REQ-CR-1.16 operators run on decimals). (b) It compares Money directly: InstanceOrchestration.fs:145 (confirmFullyPaidAmountMatches: `paidTotal = invoiceAmount.money`) and StageEntryOrchestration.fs:57 (`totalCredits = totalDebits`). The paid-vs-invoice equality in InstanceOrchestration is written both ways in the same file. Under a strict reading of 2.1, every case in (a) breaks the requirement: these are not boundary functions, and 2.7.1's unwrap permission covers only multiplication and division. Under a loose reading, 2.8 (convert Money to decimal) permits any unwrap. Two competent developers have in fact built it both ways.

**Action:** Dan decides one of two options. (1) Amend REQ-MON-2.1 or 2.7.1 to say that unwrapping via REQ-MON-2.8 is permitted for comparison and sign predicates. (2) Add Money comparison/sign requirements (e.g. compare, isPositive) and treat decimal-side comparisons in non-boundary code as violations of 2.1.

**Why:** REQ-MON-2.1 is waived from testing and depends wholly on code review to enforce it. Dan has said this audit is the main back-stop for agent-written code. If the requirement's scope can be read two ways, no reviewer can enforce it consistently, and the code already shows the drift.

---

## MON-CON-1 — contradiction
- **Location:** Specs/Behavioral/Money.md REQ-MON-2.2.1 vs REQ-MON-2.3.1, 2.5.1, 2.6.1, 2.9.1; Unenforceable table (REQ-MON-1.1)
- **Summary:** REQ-MON-2.2.1 excludes the unenforceable REQ-MON-1.1 from 'validate all of section 1', but the parallel requirements 2.3.1, 2.5.1, 2.6.1 and 2.9.1 do not, so read literally they require validating something the spec itself says cannot be enforced.
- **Resolution:** fix-spec

REQ-MON-2.2.1 (line 17) reads 'validate that all requirements from section 1 are met when doing so. (Except 1.1, which is unenforceable)'. REQ-MON-2.3.1 (line 19), 2.5.1 (line 29), 2.6.1 (line 31) and 2.9.1 (line 36) say 'validate ... all requirements from section 1' or 'all rules stated in section 1' with no such exception. The Unenforceable table (line 56) says of REQ-MON-1.1 that 'Nothing in the system tracks currency.' So these four requirements, read literally, require validating 1.1, which the same document says nothing can do. The tests have taken the narrower reading: the 2.3.1/2.5.1/2.6.1/2.9.1 tests in Tests/Tests.Isolated/Model/Money.fs cover only precision, max and min. The code behaves correctly. The defect is internal inconsistency in the spec text.

**Action:** Make the five 'validate section 1' requirements consistent. Either add '(except 1.1)' to 2.3.1, 2.5.1, 2.6.1 and 2.9.1, or remove the parenthetical from 2.2.1 and add one sentence after section 1 saying that validation requirements refer to REQ-MON-1.2 through 1.4.

**Why:** A test writer working from the spec text could reasonably try to add a currency check to these four requirements, or decide that 2.2.1 alone carries the exception. The spec should say the same thing in every place it says it.

---

## MON-WAV-1 — other
- **Location:** Specs/Behavioral/Money.md Waived table, REQ-MON-2.1.1 and REQ-MON-2.7.1
- **Summary:** The waiver reason 'You cannot test for the total absence of something' does not describe REQ-MON-2.1.1 (an exception clause) or REQ-MON-2.7.1 (a required conversion route); it fits only their parent requirements.
- **Resolution:** dan-decides

The Waived section says waived requirements are 'enforced (by type system, code review, schema, or construction pattern) but deliberately not verified by tests'. All four entries share one reason. That reason fits REQ-MON-2.1 and REQ-MON-2.7, which are prohibitions. It does not fit the other two. REQ-MON-2.1.1 prohibits nothing: it grants an exception for boundary functions. It is a design permission that binds humans, which matches this file's own definition of Unenforceable ('Active requirements that bind humans, not code'). REQ-MON-2.7.1 is a positive statement of how multiply/divide needs must be met (unwrap to decimal, compute, call fromDecimal). It is enforced by construction: Money.fs:8 declares `type Money = private { amount: decimal }`, and the module defines no * or / operations, so 2.7.1's route is the only one available. The waived classification of 2.7.1 is sound, but its stated reason is not the actual enforcement mechanism. AMB-13 in resolved-findings.md covers the operator-versus-behavioral wording of 2.7, not the soundness of these waiver reasons, so it does not cover this finding.

**Action:** Restate the REQ-MON-2.7.1 waiver reason as enforced by construction (private record, no arithmetic operators beyond add/subtract/sum/split). Either move REQ-MON-2.1.1 to the Unenforceable table as a design-time permission, or give it a reason that matches what it says.

**Why:** Check 7 asks whether waiver reasons are sound. A reason that does not describe the requirement leaves the next auditor, or an agent developer, unable to tell how the requirement is actually enforced. Under fully agentic development, that is what decides whether the waiver still holds.

---

## MON-DEF-1 — contradiction
- **Location:** Specs/Behavioral/Money.md REQ-MON-2.1 vs Specs/Definitions.md 'Money (as a variety of number)' note
- **Summary:** REQ-MON-2.1 binds every value that meets the Definitions.md Money definition to the system's Money type, while Definitions.md says that definition also covers future Money types that the system Money type's rules do not all govern.
- **Resolution:** dan-decides

REQ-MON-2.1: 'Functions that are intended to operate on or with values that meet the Definitions.md definition for "Money (as a variety of number)" must only take explicit Money type arguments and must only return explicit Money type values.' Definitions.md (Money note): 'in future, we will be building out a Monte Carlo simulation that will have its own type that fits within the Money definition ... rules governing the system's Money type do not all apply to all types defined under the "Money" definition.' REQ-MON-2.1 is scoped by the definition, not by the type. Read literally, any future Money-definition type (the Monte Carlo type Definitions.md anticipates) would have to be handled through the system Money type, which is the outcome Definitions.md rules out. Definitions.md ranks above Specs/Behavioral in the stated authority hierarchy. Nothing breaks today: Definitions.md itself says the system Money type is currently the only one. The conflict is in the text as written, not hypothetical.

**Action:** Dan decides whether REQ-MON-2.1 should be scoped to values the system currently represents with its Money type (e.g. 'values that meet the Money definition and are represented by the system's Money type'), or whether the Definitions.md note should state that REQ-MON-2.1 does apply to future Money-definition types.

**Why:** Higher-authority text contradicting lower-authority text is a finding under the hierarchy. Fixing it now costs one clause. Fixing it later means an agent developer runs into 2.1 while building the retirement-planning engine Dan has described.

---

