# code-truthfulness-ui-interface-bridge

_No findings._

## Reasoning

Scope: Ui.InterfaceBridge (contracts, boundary converters, routes, report writers, CommandRoute, Startup), Ui.OperatorCli and Ui.ReportCli. I read the audit-conduct catalog and its articles, the precedent ledger (relevant entries: NGUI-AQ-1, WAIVE-1, ROUTE-ORACLE, REPORT-RECORDS, REPORT-SQL), the coding and architecture catalogs, Positions §6–§15 and Reporting §2, §3 and §8–§10. I then diffed every UI file changed since the last audit's disposition commit (22af7f4..HEAD, 16 files) and read each slice-2 change line by line.

Checked and found consistent with the specs:

(1) Lots. AccountSnapshotLotInput/Return and AccountSnapshotLineInput/Return carry acquired date, quantity and optional cost basis. The converter keeps the supplied order (List.map), as REQ-POS-6.13 requires. Snapshot Fetch and Holding FetchAsOf return each line's lots (REQ-POS-7.5, 8.3). Record runs in one transaction (REQ-POS-7.1).

(2) Activity. The contract carries every field REQ-POS-12.1 lists, with optional source, Security, quantity and price. Kind is parsed with ActivityKind.fromString, and the route description lists all 15 kinds in REQ-POS-12.2. Record is one atomic route and returns removed and recorded counts per account and range (REQ-POS-13.1, 13.4). List takes begin and end dates (REQ-POS-13.5).

(3) Roll-forward. The route takes an account and two dates. The return rows carry start quantity, units in, units out, expected, end quantity and difference as signed decimals, matching REQ-POS-14.2 and 14.3. The route is read-only.

(4) Pre-ledger balances. Record, Delete and List routes exist. Accounts are given by code through fallibleConverterAccountCodeToAccountId (REQ-NGUI-1.5). Returns carry LedgerAccountReturn with both code and name (REQ-NGUI-1.6). The List account filter is optional (REQ-POS-15.6). Recorded returns carry replacedExisting (REQ-POS-15.4).

(5) Reports. The NetWorth data-only contract now carries isPreLedger, a balanceDate on each ledger row, totalOwnedPropertyMortgages and absentComponents (REQ-RPT-8.5, 8.8). NetWorthWriter shows the balance-date column only on pre-ledger dates, including on mortgage rows, and adds a note naming the absent components. The NetWorthHistory route, contract and writer match REQ-RPT-10.2 and 10.4: one column per total, a Source column plus an italic class marking pre-ledger points, an Absent column that is empty when nothing is absent, and a header showing begin and end dates with no as-of date. File names get -begin_end interpolation, and all text is HTML-encoded. Footers use the initiation instant (REQ-RPT-3.2 / SYS-3.4). All report routes use NoTransaction FetchOnly (REQ-RPT-2.6).

(6) Wiring. positionsDomainCommandRoutes is registered in OperatorCli and reportingRoutes in ReportCli. No route exposes a hard delete for Dimension Value, Security or Investment Account (REQ-POS-11.12–11.14). Route descriptions for Holding Delete and Investment Account Create agree with REQ-POS-11.10 and 4.8.

(7) Other diffs. The ingestion-source filter now resolves the name and fails with a typed error when it matches no source (consistent with REQ-STG-10.7). The Person update route now addresses the person by name and resolves it to an ID.

Considered and rejected:
- InvestmentWealthHistoryWriter now uses List.find. It relies on the orchestrator's invariant that every point carries every group (REQ-RPT-9.2), which the computation's own tests cover. Under the schema-guaranteed-values conduct rule, a value guaranteed by construction is not unsafe partiality.
- The NetWorth route description says "latest snapshot on or before the date" without repeating "active on the date". This is help text that paraphrases rather than contradicts, so it is too minor to report.
- The AccountSnapshot Delete description says "with its lines" without mentioning lots. Also immaterial.
- Error message contents (account, date and range naming) are produced in orchestration, outside this scope.

Statement check: Dan said "All UI routes and reports are wired in". The repo agrees: every slice-2 operation has a route, NetWorthHistory is registered in ReportCli, and the PositionsSliceTwoRoutes and PositionsReportRoutes tests exercise them. Nothing rose to finding level.