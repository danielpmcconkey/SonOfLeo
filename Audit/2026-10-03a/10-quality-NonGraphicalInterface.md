# ngui-spec-auditor

## NGUI-1.6-CASHFLOW — enforcement-gap
- **Location:** Src/Ui.InterfaceBridge/InterfaceContracts/CashFlowContracts.fs:107-118 (PaymentAgreementReturn); Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:208; REQ-NGUI-1.6
- **Summary:** The cash-flow PaymentAgreementReturn contract names the debit and credit accounts by code only, with no account names, which breaks REQ-NGUI-1.6.
- **Resolution:** fix-code

REQ-NGUI-1.6: "All interface return payloads that identify an account must include the account name alongside the account code." PaymentAgreementReturn has `debitAccountCode: string` and `creditAccountCode: string` (CashFlowContracts.fs:111-112) and no name fields. It is nested in AgreementReturn.paymentAgreements (line 122). AgreementReturn is the outputContract of the CashFlow agreement routes (CashFlowRoutes.fs:182; the converter is called at CashFlowRoutes.fs:144). Other cash-flow returns follow the rule, for example ProjectedAccountReturn (accountCode plus accountName, lines 145-146), and so do the Classification and Ingestion returns (ClassificationContracts.fs:56-57, IngestionContracts.fs:30-31). This is the second time a new slice has missed this rule: the 2026-08-21a audit raised NGUI-1.6-INGESTION for three ingestion return types, and it was accepted as fix-code.

**Action:** Add debitAccountName and creditAccountName to PaymentAgreementReturn. Fill them in ``convert [PaymentAgreement] to [PaymentAgreementReturn]`` using the same lookup the other converters use.

**Why:** REQ-NGUI-1.6 is waived from testing on the grounds that code review and periodic audit enforce it. This audit is that enforcement, and the violation reaches every agreement create, update and summary payload the operator reads during the Saturday routine.

---

## NGUI-1.6-WAIVER — other
- **Location:** Specs/Behavioral/NonGraphicalInterface.md:69 (Waived table, REQ-NGUI-1.6)
- **Summary:** The REQ-NGUI-1.6 waiver says the rule cannot be proven and is enforced by code review, but new slices keep breaking it and a mechanical check is feasible.
- **Resolution:** dan-decides

Waiver reason: "Negative existence claim — cannot prove every payload includes account name; enforced by code review and periodic audit" (Dan, 2026-08-07). Two facts undercut this. (1) Every return payload is an F# record type in one place, Src/Ui.InterfaceBridge/InterfaceContracts/*.fs. A reflection test can check that every contract type with an `*AccountCode` or `code` field identifying an account also has a matching name field, so the rule is provable over a closed, enumerable set of types. (2) Code review has missed it twice since the waiver was granted: the ingestion slice (NGUI-1.6-INGESTION, 2026-08-21a) and now the cash-flow slice (PaymentAgreementReturn, see NGUI-1.6-CASHFLOW). Dan's statement says development is now fully agentic and he does not review code, so this audit is the only enforcement left. The resolved-findings precedent WAIVE-1 covers only the "too broadly scoped" wording on REQ-NGUI-3.1-3.5, so it does not apply here.

**Action:** Dan decides one of two things. Keep the waiver as it is. Or revoke it and require a reflection-based test over the InterfaceContracts return types (the same approach could cover REQ-NGUI-1.4's code half).

**Why:** A waiver is sound only when its stated enforcement actually works. Two misses in two slices, with human code review gone from the loop, suggest that the enforcement this waiver names no longer catches the violations it is meant to prevent.

---

## NGUI-1.6-PARENT — ambiguity
- **Location:** Specs/Behavioral/NonGraphicalInterface.md:14 (REQ-NGUI-1.6); Src/Ui.InterfaceBridge/InterfaceContracts/AccountContracts.fs:25, :48
- **Summary:** REQ-NGUI-1.6 does not say whether a reference to a related account, such as a parent account, counts as a payload that "identifies an account". The Account contracts give a parent code with no name.
- **Resolution:** dan-decides

AccountReturn.parentCode (AccountContracts.fs:25) and AccountActivityReturn.accountParentCode (AccountContracts.fs:48) identify the parent account by code only. Read literally, REQ-NGUI-1.6 ("All interface return payloads that identify an account must include the account name alongside the account code") covers these fields too. Read more narrowly, it covers only the payload's main account. The two readings lead to different contracts: one developer adds parentName, another does not. The cash-flow slice's debit and credit codes show the same pattern, and Dan has not ruled on secondary account references. No resolved-findings entry covers this.

**Action:** Dan rules whether REQ-NGUI-1.6 applies to every account reference in a return payload (parent, debit or credit, and so on) or only to the main account. Amend the REQ text to say so. If it applies to all of them, add parent-name fields to AccountReturn and AccountActivityReturn.

**Why:** A waived rule enforced only by review and audit needs an unambiguous scope. Otherwise each agentic developer and each auditor applies it differently, and the same question comes back every audit.

---


