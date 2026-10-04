# code-truthfulness-ui-interfacebridge-cli

## STMT-JSON-1 — statement-delta
- **Location:** Src/App.Utility/Json.fs (missingFieldMessage, fromJson); exercised by every route in Src/Ui.InterfaceBridge/Routes/*.fs, e.g. PositionsRoutes.recordAccountSnapshots and createProperty
- **Summary:** Dan says Json.fromJson 'now names the field a payload actually left out', but in two common cases it still names a different field: a missing field inside a nested record, and a payload that legally leaves out an option field declared before the missing field.
- **Resolution:** dan-decides

missingFieldMessage only rewrites the library message when its prefix names the top-level record type 'T (`Missing field for record type {typeof<'T>.FullName}: `). Otherwise it returns the library message unchanged, and the code comment says so. It then picks `FSharpType.GetRecordFields recordType |> Array.tryFind (fun f -> not (sent.Contains f.Name))`, which is the first field absent from the JSON, whatever its type. The same comment says FSharp.SystemTextJson treats an unset option field as allowed and names the first unset field, option fields included. Two cases follow. (1) Nested records. AccountSnapshot Record reads AccountSnapshotRecordInput { snapshots: AccountSnapshotInput list }, and AccountSnapshotInput declares `contributionBasis: decimal option` before `lines`. Take a snapshot for a non-Roth account that sends contributionBasis as null (the normal case under REQ-POS-6.4) and forgets `lines`. The library names AccountSnapshotInput's contributionBasis. That is not 'T, so the message passes through unchanged and names contributionBasis, not lines. (2) Option fields left out of a top-level record. PropertyCreateInput declares `disposalDate: LocalDate option` before `purchaseBasis`. Take a payload for an undisposed property that leaves out disposalDate entirely (legal, since it is an option) and also leaves out purchaseBasis. The fix finds disposalDate first, because it is not in `sent`, so the error names disposalDate. The REQ-POS-9.4/9.5 route tests in Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs (lines ~426, ~815, ~830) remove one required field from an otherwise complete payload, where any option fields are presumably sent explicitly. So they do not cover either case. Caveat: dotnet was not available in the audit container, so case (2) rests on the library behaviour the code's own comment describes and was not executed.

**Action:** Either have missingFieldMessage skip option-typed fields (and voption/Skippable) when it picks the first field not sent, and apply the same correction to nested record types, or narrow the statement and the code comment to 'names the left-out field when the top-level record's only unset fields are required ones'. Add a route test for each case.

**Why:** Dan is relying on this change as a fix that every route shares. The operator acts on the error message: told 'contributionBasis' is missing for a non-Roth snapshot, they would add a contribution basis, which REQ-POS-6.4 then rejects, instead of adding the lines they left out. A message naming the wrong field is the failure the change was meant to remove.

---

## SYS-1.1-ENUM-TRIM — contradiction
- **Location:** REQ-SYS-1.1; Src/Business.FinancialServices.Positions/PositionsComponent.fs (Dimension.fromString l.88, TaxTreatment.fromString l.169, BasisMethod.fromString l.184, Provenance.fromString l.206, PropertyUse.fromString l.230); Src/Business.CrossDomainOrchestration/InvestmentWealthHistory.fs WealthGrouping.fromString l.29; called untrimmed from Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs (l.29, 39, 50), BoundaryConverters/PositionsFieldConverters.fs (l.52, 64, 94, 120, 173, 245, 275), Routes/ReportRoutes.fs l.109
- **Summary:** The Positions slice (and the report grouping) matches enum-valued string inputs without trimming them, so a padded value such as " Roth" is rejected. REQ-SYS-1.1 requires every raw string input to be trimmed at the boundary before validation.
- **Resolution:** fix-code

REQ-SYS-1.1: 'All raw string inputs must be trimmed of leading and trailing white space at the system boundary, before validation, before persistence, and before being returned to the caller.' The Ledger's enum parsers trim: AccountType.fromString (`match accountType.Trim() with`), AccountSubtype.fromString and JournalEntryLineType.fromString all trim before matching. They are verified by 'REQ-SYS-1.1 AccountType fromString trims input before matching' and 'REQ-SYS-1.1 AccountSubtype fromString trims input before matching' (Tests.Isolated/Model/Ledger/AccountComponent.fs l.115, l.152). None of the new Positions parsers trims, and neither the routes nor the converters trim before calling them (Src/Ui.InterfaceBridge has no `.Trim()` call). Examples: an InvestmentAccount Create with taxTreatment " Roth" fails PositionsInvalidTaxTreatment; a DimensionValue Create with dimension "Sector " fails PositionsInvalidDimension; a wealth-history report with grouping "Owners " fails PositionsInvalidWealthGrouping. Every Positions name type trims (PositionsComponent tests l.62, 78, 106, 189), so the gap is only in the enum tokens. The same pattern predates this slice in CashFlow (FlowDirection.fromString l.64 and the other CashFlowComponent fromString functions), Classification (ClassificationComponent l.50, 111, 197) and DataIngestion (StageEntryComponent l.25, 54). Nothing in resolved-findings.md addresses enum trimming.

**Action:** Trim the raw string in each enum fromString before matching, as AccountType.fromString does, starting with the Positions parsers and WealthGrouping. Add REQ-SYS-1.1 isolated tests like the Ledger ones. Alternatively, if Dan holds that enum tokens are not 'raw string inputs', amend REQ-SYS-1.1 to say so; the Ledger's behaviour then becomes an exception rather than the rule.

**Why:** REQ-SYS-1.1 is the system-wide boundary rule, and the Ledger shows that it covers enum tokens. As things stand, the same padding is accepted for an account type but rejected for a tax treatment, so how the boundary treats input depends on which domain receives it.

---

## RPT-3.1-WEALTH-HEADER — contradiction
- **Location:** REQ-RPT-3.1, Reporting.md §3 scope note (2026-10-04), REQ-RPT-9.5; Src/Ui.InterfaceBridge/ReportWriters/InvestmentWealthHistoryWriter.fs write (subtitle From/to)
- **Summary:** The spec says REQ-RPT-3.1 applies to the investment wealth history report, and REQ-RPT-3.1 requires the header to show 'the as-of Calendar Date'. That report has no as-of date: the writer correctly shows the begin and end dates, and the test citing REQ-RPT-3.1 asserts the range.
- **Resolution:** fix-spec

Reporting §3's scope note: 'For net worth (§8) and investment wealth history (§9), REQ-RPT-3.1, 3.2 and 3.5 apply'. REQ-RPT-3.1: 'The rendered HTML report must contain a header section displaying the report title and the as-of Calendar Date.' Investment wealth history takes a begin and an end date (REQ-RPT-9.1), and its header renders 'From <begin> to <end>, by <grouping>' (InvestmentWealthHistoryWriter.fs, subtitle element). The test 'REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history shows the report title and the begin and end dates in its header…' (Tests.Integrated/InterfaceBridge/PositionsReportRoutes.fs l.238) asserts the range under REQ-RPT-3.1. Period activity has the same range shape, and REQ-RPT-6.4 states the override explicitly ('the rendered header (REQ-RPT-3.1) shows the range'). REQ-RPT-9.5 has no such clause. So code and test agree with each other, but the spec text they cite asks for something else.

**Action:** Amend REQ-RPT-9.5 to say the rendered header (REQ-RPT-3.1) shows the begin and end dates, as REQ-RPT-6.4 does for period activity.

**Why:** REQ-IDs are the only link between specs and tests. A test that cites a requirement for behaviour the requirement does not state leaves traceability looking clean while the spec and the code disagree.

---

