# statement-delta

## SD-1 — statement-delta
- **Location:** Specs/Behavioral/Positions.md REQ-POS-7.4, 11.1-11.8; Specs/Behavioral/Person.md (Deletion note, REQ-PER-2.1-2.3); Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs:305-455; Src/Ui.InterfaceBridge/Routes/PersonRoutes.fs:44-57
- **Summary:** Dan says the slice implements 'all the CRUD operations' for Positions data and 'crud functions' for Person, but six of the eight maintained record types (Person, Dimension Value, Security, Investment Account, Holding, Property) have no delete operation. This is by design in the spec.
- **Resolution:** dan-decides

The operator routes registered for the slice are: Person Create/Update/List; DimensionValue Create/Rename/List; Security Create/Update/List; InvestmentAccount Create/Update/List; Holding Create/UpdateBasisMethod/List/FetchAsOf; AccountSnapshot Record/Delete/Fetch/ListDates; Property Create/Update/List; Valuation Record/Delete/List. Only Account Snapshots (REQ-POS-7.4) and Valuations (REQ-POS-11.8) can be deleted. The spec intends this. Person.md says plainly: 'There is no means to delete a Person.' The REQ-POS-11.x maintenance requirements for Dimension Values, Securities, Investment Accounts, Holdings and Properties provide create, update and list, with no delete. Taken literally, 'all the CRUD operations' (create, read, update, delete) does not match the repo: a Security, Investment Account, Property or Person created by mistake cannot be removed through any route.

**Action:** Confirm that 'no delete for maintained Positions/Person records' is the intended model (it matches the spec) and update the mental model to: create, read and update for maintenance records; delete only for Account Snapshots and Valuations.

**Why:** If Dan expects to be able to undo a mistyped Security or Person from the CLI, he will find no route for it. The only remedies are renaming the record or editing the database directly.

---

## SD-0-CONFIRMATIONS — other
- **Location:** Repo at da96f73 (audit-2026-10-04a == main)
- **Summary:** Apart from SD-1, every claim in Dan's statement checked out against the repo.
- **Resolution:** dan-decides

Verified: (1) Last audit: Audit/2026-10-03a/03-dan-statement.md covers the cash flow slice on top of ingestion and the ledger. Of the 99-disposition.md rows, 297 are accepted, 12 overruled and 2 deferred (EFF-MON-7, MON-DEF-1). Spot checks show accepted items applied: LookupCache no longer names ledger/cashflow tables; updateInitiationInstant is gone from Src; BridgeError.fs is deleted; ListAgreements/FetchOpenInstances exist in CashFlowRoutes; classifyAccounts and classifyPaymentAgreements moved to ClassificationOrchestration. The one open remediation item, REQ-CF-7.15, was ruled by Dan and closed in 342ff0a. (2) Positions tracks securities (Security, InvestmentAccount, Holding, AccountSnapshot) and real estate (Property, Valuation), with NetWorth and InvestmentWealthHistory report routes (ReportRoutes.fs:149,154) and writers. (3) Person sits in Src/Business.General/Person.fs under the 'general' schema (202610041000-CreateSchemaGeneralAndPerson.sql). Definitions.md:54-58 separates Person from User, and the archimate 'Domains Build Upward' principle repeats the distinction. The ledger account table has no owner column. (4) Every spec'd Positions, Person and report operation has a route: OperatorCli/Program.fs:15-17 registers personDomainCommandRoutes and positionsDomainCommandRoutes, and ReportCli registers NetWorth and InvestmentWealthHistory. No institution parser or historical-import code exists in the repo. Imported history arrives through AccountSnapshot Record with provenance 'Imported' (REQ-POS-6.2). (5) Snapshots are whole-account and verbatim (Positions.md design notes; REQ-POS-6.7/6.8 validate against the reported figures and derive nothing). There are no purchase, sale, tax-lot or realised-gain structures. basis_method is on positions.holding. (6) Business.FinancialServices.Positions has its own fsproj (references General, FinServ and Ledger only) and its own 'positions' schema (202610041010/1020). The archimate concept order is 'account < fiscal period < journal entry < data ingestion < cash flow < positions < classification'. CrossDomainOrchestration references Positions between CashFlow and Classification. (7) Investment and real-estate entity modules inside the Positions project do not open or reference each other. Combining logic lives in CrossDomainOrchestration (NetWorth; PositionsLedgerLinks, the shared ledger-link check). This matches the archimate rule that 'anything that combines them is orchestration'. (8) Quantity.fs and Price.fs sit beside Money.fs in Business.FinancialServices as private decimal records that reject anything beyond 6 decimal places (no rounding), with max 9999999999.999999. Price.multiplyQuantity returns decimal, not Money (REQ-QP-3.5). (9) App.Utility/Json.fs fromJson now uses missingFieldMessage to name the field the payload actually left out (commit 2387cde). Every route module uses fromJson. The only other non-slice Src edits (HtmlComponents H2/H3/table tags, report contracts and converters) exist to support the new reports.

**Action:** None. This entry is informational.

**Why:** It shows Dan which parts of his mental model were checked against the repo and held up.

---


