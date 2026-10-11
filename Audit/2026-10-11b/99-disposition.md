# Disposition Record

| # | Auditor | ID | Summary | Owner | Status | Ruling | Date |
|---|---------|----|---------|----- -|--------|--------|------|
| 001 | architecture-auditor | ARCH-DRIFT-1 | The CashFlow CancelInstance and CancelInvoice routes offer a use case that no ApplicationFunction in the model describes. | dan-decides | pending |  |  |
| 002 | architecture-auditor | ARCH-ERR-1 | PositionsError declares the domain type ActivityShapeProblem, and the error case PositionsActivityShapeInvalid carries it. The todo comment admits this was done for compile order. | dan-decides | pending |  |  |
| 003 | architecture-auditor | ARCH-ERR-2 | CashFlowError declares the union OrphanedLineReason, and the error case CashflowPaymentAgreementLinksOrphaned carries it. | dan-decides | pending |  |  |
| 004 | architecture-auditor | ARCH-ERR-3 | ClassificationError declares the union StageLineProtection, and two error cases carry it. | dan-decides | pending |  |  |
| 005 | architecture-auditor | ARCH-ERR-4 | DataIngestionError declares the record IngestionRejectedRecord, whose field `error` is an IAppError, and IngestionFileRejected carries a list of it. | dan-decides | pending |  |  |
| 006 | architecture-auditor | ARCH-ERR-5 | Nine DalError cases carry `exn`, which is neither a primitive nor a NodaTime value. | dan-decides | pending |  |  |
| 007 | architecture-auditor | ARCH-ERR-6 | The UtilityError cases ConfigReadError (string * exn) and FileIoError (exn) carry exception objects. | dan-decides | pending |  |  |
| 008 | architecture-auditor | ARCH-COMP-1 | AccountActivityDetail is a journal-entry-only record defined in orchestration. | dan-decides | pending |  |  |
| 009 | architecture-auditor | ARCH-COMP-2 | The AccountBalance record (accountId, totalCredits, totalDebits, netBalance) holds only account-domain fields and Money, yet it is defined in orchestration. | dan-decides | pending |  |  |
| 010 | architecture-auditor | ARCH-COMP-3 | SnapshotLineView, SnapshotView and RecordedSnapshot hold only positions-domain types, yet they are defined in orchestration. | dan-decides | pending |  |  |
| 011 | architecture-auditor | ARCH-COMP-4 | Agreement and AgreementListing are cash-flow-only records defined in orchestration. | dan-decides | pending |  |  |
| 012 | architecture-auditor | ARCH-COMP-5 | AgreementFilter (cash flow only) and FetchStageEntrySort (data ingestion only) are single-domain types in orchestration. | dan-decides | pending |  |  |
| 013 | architecture-auditor | ARCH-COMP-6 | HoldingView is a positions-only record defined in orchestration. | dan-decides | pending |  |  |
| 014 | architecture-auditor | ARCH-COMP-7 | HoldingsAsOfLine is a positions-only record defined in orchestration. | dan-decides | pending |  |  |
| 015 | architecture-auditor | ARCH-COMP-8 | InvoiceComposite, InstanceComposite, InvoiceCompositeUpdate and InstanceCompositeUpdate are cash-flow-only records defined in orchestration. | dan-decides | pending |  |  |
| 016 | architecture-auditor | ARCH-COMP-9 | ActivityView and RecordedRange are positions-only records defined in orchestration. | dan-decides | pending |  |  |
| 017 | architecture-auditor | ARCH-COMP-10 | WealthPoint (monthEnd, totals by WealthGroup, total) is a positions-only record defined in orchestration. | dan-decides | pending |  |  |
| 018 | architecture-auditor | ARCH-COMP-11 | The JournalEntry 'composite' holds only journal entry types, so the new composite constraint rejects it, while another principle in the model uses it as its example of a composite. | dan-decides | pending |  |  |
| 019 | architecture-auditor | ARCH-COMP-12 | LedgerAccountBalance (code, name, balance, balanceDate) is an account-only record defined in orchestration. | dan-decides | pending |  |  |
| 020 | architecture-auditor | ARCH-COMP-13 | PeriodActivityLine is a journal-entry-only record defined in orchestration. | dan-decides | pending |  |  |
| 021 | architecture-auditor | ARCH-COMP-14 | PrePostingPayment and PrePostingAgreement are cash-flow-only records defined in orchestration. | dan-decides | pending |  |  |
| 022 | architecture-auditor | ARCH-COMP-15 | ReconciliationRow is an account-only record defined in orchestration. | dan-decides | pending |  |  |
| 023 | architecture-auditor | ARCH-COMP-16 | SecurityView (security plus dimension value names) is a positions-only record defined in orchestration. | dan-decides | pending |  |  |
| 024 | architecture-auditor | ARCH-COMP-17 | StageEntry and DeduplicationResult are data-ingestion-only records defined in orchestration. | dan-decides | pending |  |  |
| 025 | architecture-auditor | ARCH-COMP-18 | TrialBalanceRowNested and TrialBalanceRowFlattened are account-only records defined in orchestration. | dan-decides | pending |  |  |
| 026 | architecture-auditor | ARCH-COMP-19 | RollForwardRow and RollForward are positions-only records defined in orchestration. | dan-decides | pending |  |  |
| 027 | architecture-auditor | ARCH-PRIM-1 | The HoldingsAsOf report types hold persisted names, ticker, institution and account group as bare strings, although domain types exist for each. | dan-decides | pending |  |  |
| 028 | architecture-auditor | ARCH-PRIM-2 | The NetWorth rows hold ledger code and name, investment account name, owner names, account group and property name as strings. | dan-decides | pending |  |  |
| 029 | architecture-auditor | ARCH-PRIM-3 | SnapshotLineView.securityName and SnapshotView.investmentAccountName are bare strings, and viewSnapshot takes the account name as a string. | dan-decides | pending |  |  |
| 030 | architecture-auditor | ARCH-PRIM-4 | HoldingView.investmentAccountName and securityName are bare strings. | dan-decides | pending |  |  |
| 031 | architecture-auditor | ARCH-PRIM-5 | ActivityView and RecordedRange hold the investment account name and security name as strings. | dan-decides | pending |  |  |
| 032 | architecture-auditor | ARCH-PRIM-6 | InvestmentAccountView holds owner names and the linked ledger account's code and name as strings. | dan-decides | pending |  |  |
| 033 | architecture-auditor | ARCH-PRIM-7 | PropertyView holds owner names and the asset and mortgage account codes and names as strings. | dan-decides | pending |  |  |
| 034 | architecture-auditor | ARCH-PRIM-8 | PreLedgerBalanceView holds the ledger account code and name as strings. | dan-decides | pending |  |  |
| 035 | architecture-auditor | ARCH-PRIM-9 | SecurityView.dimensionValueNames is a Map<Dimension, string> holding persisted dimension value names. | dan-decides | pending |  |  |
| 036 | architecture-auditor | ARCH-PRIM-10 | RollForwardRow holds the persisted snapshot quantities as decimal and the security and account names as strings. | dan-decides | pending |  |  |
| 037 | architecture-auditor | ARCH-PRIM-11 | PrePostingPayment holds invoice and payment values as the types they unwrap to (Money, LocalDate) instead of PaymentAmount, InvoiceDate, DueDate and InvoiceAmount. | dan-decides | pending |  |  |
| 038 | architecture-auditor | ARCH-PRIM-12 | The WealthGroup union carries persisted names as `GroupName of string` and `GroupOwners of string list`. | dan-decides | pending |  |  |
| 039 | architecture-auditor | ARCH-PRIM-13 | Account.updateAccountNameById and updateExternalReferenceById take the new name and reference as string and string option, so the UI hands the Business tier primitives and the domain types are built below the bridge. | dan-decides | pending |  |  |
| 040 | architecture-auditor | ARCH-PRIM-14 | FiscalPeriod.fetchIdByKey takes the persisted period key as a bare string although FiscalPeriodKey exists. | dan-decides | pending |  |  |
| 041 | architecture-auditor | ARCH-PRIM-15 | The ClassificationRule entity holds the persisted rule priority as a naked int. | dan-decides | pending |  |  |
| 042 | architecture-auditor | ARCH-PRIM-16 | The PrioritizedMatch component holds the rule priority as a naked int. | dan-decides | pending |  |  |
| 043 | architecture-auditor | ARCH-RR-1 | The HoldingsAsOf report read keeps eleven columns as bare strings, decimals and Guid after reconstitute, and passes the raw row itself on to buildAccount. | dan-decides | pending |  |  |
| 044 | architecture-auditor | ARCH-ID-1 | HoldingsAsOf rows carry security, dimension value, owner and ledger-account names and codes, and no IDs, and the query selects names to stand in for IDs. | dan-decides | pending |  |  |
| 045 | architecture-auditor | ARCH-ID-2 | The NetWorth rows identify ledger accounts by code and name, investment accounts and properties by name, and owners by name. | dan-decides | pending |  |  |
| 046 | architecture-auditor | ARCH-ID-3 | The snapshot views add a security name and an investment account name that the bridge should resolve from the IDs already present. | dan-decides | pending |  |  |
| 047 | architecture-auditor | ARCH-ID-4 | HoldingView carries the investment account name and security name, though Holding already holds their IDs. | dan-decides | pending |  |  |
| 048 | architecture-auditor | ARCH-ID-5 | ActivityView and RecordedRange carry account and security names, and securityNamesById builds a name map in the Business tier. | dan-decides | pending |  |  |
| 049 | architecture-auditor | ARCH-ID-6 | InvestmentAccountView carries owner names and the ledger account's code and name. | dan-decides | pending |  |  |
| 050 | architecture-auditor | ARCH-ID-7 | PropertyView carries owner names and the asset and mortgage accounts' codes and names. | dan-decides | pending |  |  |
| 051 | architecture-auditor | ARCH-ID-8 | PreLedgerBalanceView carries the ledger account's code and name beside the entity, which already holds ledgerAccountId. | dan-decides | pending |  |  |
| 052 | architecture-auditor | ARCH-ID-9 | SecurityView maps each dimension to its value's name, though Security holds the DimensionValueIds. | dan-decides | pending |  |  |
| 053 | architecture-auditor | ARCH-ID-10 | Roll-forward rows identify the security by name and the account by name, with no HoldingId, SecurityId or InvestmentAccountId. | dan-decides | pending |  |  |
| 054 | architecture-auditor | ARCH-ID-11 | Pre-posting review rows identify accounts by code and name, rules by name, agreements by name and the ingestion source by name, with no IDs. | dan-decides | pending |  |  |
| 055 | architecture-auditor | ARCH-ID-12 | Trial balance rows carry account code and name and no AccountId. | dan-decides | pending |  |  |
| 056 | architecture-auditor | ARCH-ID-13 | ReconciliationRow identifies the account by code and name with no AccountId. | dan-decides | pending |  |  |
| 057 | architecture-auditor | ARCH-ID-14 | PeriodActivityAccount identifies the account by code and name with no AccountId. | dan-decides | pending |  |  |
| 058 | architecture-auditor | ARCH-ID-15 | DeactivatedAccountWithBalance identifies the account by code and name with no AccountId. | dan-decides | pending |  |  |
| 059 | architecture-auditor | ARCH-ID-16 | personNamesOf turns PersonIds into sorted name strings in the Business tier, feeding the NetWorth, InvestmentAccount and Property views. | dan-decides | pending |  |  |
| 060 | architecture-auditor | ARCH-ID-17 | ledgerAccountCodeAndName resolves an AccountId to its code and name for display in Business-tier views. | dan-decides | pending |  |  |
| 061 | architecture-auditor | ARCH-ID-18 | The Instance entity carries its master agreement's name, joined in by the query 'for read convenience'. | dan-decides | pending |  |  |
| 062 | architecture-auditor | ARCH-ID-19 | The cash flow component rows carry agreement names, payment agreement names and an account code and name in place of IDs. | dan-decides | pending |  |  |
| 063 | architecture-auditor | ARCH-ID-20 | WealthGroup identifies groups by investment account name, dimension value name and owner names instead of IDs. | dan-decides | pending |  |  |
| 064 | architecture-auditor | ARCH-PRIV-1 | DeduplicationResult and AccountClassificationResult are public records returned by write use cases. They are not read-only report outputs. | dan-decides | pending |  |  |
| 065 | architecture-auditor | ARCH-PRIV-2 | PaymentAgreementClassificationResult, InvoiceCompositeUpdate and InstanceCompositeUpdate are public composite records. | dan-decides | pending |  |  |
| 066 | architecture-auditor | ARCH-PRIV-3 | RecordedSnapshot, RecordedRange and RecordedPreLedgerBalance are public records returned by record (write) operations. | dan-decides | pending |  |  |
| 067 | architecture-auditor | ARCH-PRIV-4 | Seven classification component records are public and read by field. | dan-decides | pending |  |  |
| 068 | architecture-auditor | ARCH-PRIV-5 | The InvoiceDecision and PaymentPostingTransition component records are public, and both come from write operations. | dan-decides | pending |  |  |
| 069 | architecture-auditor | ARCH-CMOD-1 | The component types BaseStageEntryGroupId and BaseStageRawRow are defined in BaseStageEntry.fs, not in the DataIngestion components module, StageEntryComponent. | dan-decides | pending |  |  |
| 070 | architecture-auditor | ARCH-CMOD-2 | The public records StoredFieldMatch and StoredRuleGroup are defined in the ClassificationRule entity module, and their fields are bare strings and decimal. | dan-decides | pending |  |  |
| 071 | fsharp-ddd-panel | FSDDD-ERR-1 | Four error modules declare domain types and carry them in error cases, which the new 'Error cases carry primitives' constraint forbids. | fix-code | pending |  |  |
| 072 | fsharp-ddd-panel | FSDDD-RAW-1 | HoldingsAsOf's report read reads names, institution, account group, ticker, owner names and ledger code/name into bare strings that never pass through their domain constructors, and groups accounts by name. | fix-code | pending |  |  |
| 073 | fsharp-ddd-panel | FSDDD-ID-1 | Positions report records carry names and codes as bare strings in place of IDs, and in several places unwrap domain types (Quantity, ContributionBasis, TaxTreatment) to primitives. | fix-code | pending |  |  |
| 074 | fsharp-ddd-panel | FSDDD-ID-2 | Positions operation views, and the Business-tier helpers that build them, turn IDs into names and codes as strings below the UI bridge, which is the LookupCache's job in the UI tier. | fix-code | pending |  |  |
| 075 | fsharp-ddd-panel | FSDDD-ID-3 | Ledger and ingestion report rows identify accounts, rules, agreements and sources by code and name rather than by ID. | fix-code | pending |  |  |
| 076 | fsharp-ddd-panel | FSDDD-PRIM-1 | PrePostingPayment unwraps CashFlow domain types (PaymentAmount, InvoiceDate, DueDate, InvoiceAmount) to Money and LocalDate in the business tier. | fix-code | pending |  |  |
| 077 | fsharp-ddd-panel | FSDDD-PRIM-2 | Ledger entity-module functions take raw strings where a domain type exists: Account.updateAccountNameById (string), updateExternalReferenceById (string option), and FiscalPeriod.fetchIdByKey (string, though FiscalPeriodKey exists). | fix-code | pending |  |  |
| 078 | fsharp-ddd-panel | FSDDD-COMP-1 | The new composite definition (it must span more than one domain, and a one-domain record belongs in that domain's components module) contradicts the type-taxonomy article, which names the single-domain JournalEntry as the canonical composite. Compile order also makes the prescribed placement impossible for records that hold entities. | dan-decides | pending |  |  |
| 079 | fsharp-ddd-panel | FSDDD-PLACE-1 | Many records in orchestration draw every field from one domain (plus the shared Money, Quantity and Price), so under the 905c0b9 constraint they are that domain's component types, defined in the wrong tier. | fix-code | pending |  |  |
| 080 | fsharp-ddd-panel | FSDDD-CTOR-1 | EntryDate.create is a component smart constructor that takes a Context and reads the database, which the validation-layers article forbids, and it needs an `internal` escape hatch to avoid that I/O on reads. | dan-decides | pending |  |  |
| 081 | fsharp-ddd-panel | FSDDD-CACHE-1 | The LookupCache bindings exported by Business entity modules are typed Cache<string, Guid> and Cache<Guid, string>, so codes and IDs leave the business tier as bare primitives, and the UI re-wraps and re-validates them. | fix-code | pending |  |  |

## Statuses
- **pending** — not yet reviewed
- **accepted** — finding valid, action assigned
- **overruled** — finding rejected with reason
- **deferred** — acknowledged, not acting now (add revisit trigger)
