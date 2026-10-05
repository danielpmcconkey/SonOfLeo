module Tests.Integrated.InterfaceBridge.PositionsReportRoutes

open System.IO
open System.Text.RegularExpressions
open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration.NetWorth
open Business.CrossDomainOrchestration.InvestmentWealthHistory
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.TestError
open Xunit

module PF = PositionsFixture

(* Both report routes are read-only, so these tests read the committed fixture. Its net worth and wealth history
   figures are derived by hand in the CrossDomainOrchestration NetWorth and InvestmentWealthHistory tests; here the
   route is checked against the computation it wraps, and the rendered file against its name, title and dates. Each
   test deletes any file it wrote, and deletes the file it expects first, so a file left by an earlier run can't
   stand in for a new one. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private outputDir =
    let dir = "/tmp/son-of-leo-test-output"
    Directory.CreateDirectory dir |> ignore
    dir

let private dateText (d: LocalDate) = d |> Calendar.localDateToString "yyyy-MM-dd"

/// The text of a rendered report's <header> element.
let private headerOf (html: string) =
    let headerStart = html.IndexOf("<header")
    let headerEnd = html.IndexOf("</header>")
    Assert.True(headerStart >= 0 && headerEnd > headerStart, "the rendered report has no <header> element")
    html.Substring(headerStart, headerEnd - headerStart)

/// The text of the header's <h1>, the report's title.
let private titleIn (header: string) =
    let m = Regex.Match(header, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline)
    Assert.True(m.Success, "the report header has no <h1> title")
    m.Groups.[1].Value.Trim()

/// The text of every match of an element's cells, in document order.
let private cellsIn (tag: string) (html: string) =
    Regex.Matches(html, $"<{tag}[^>]*>(.*?)</{tag}>", RegexOptions.Singleline)
    |> Seq.map (fun m -> m.Groups.[1].Value.Trim())
    |> List.ofSeq

// ---- Net worth ----

let private runNetWorth (asOf: LocalDate) (reportOutput: OutputSpecifier) =
    ({ asOf = { asOf = asOf }; reportOutput = reportOutput } : NetWorthInput)
    |> Json.toJson<NetWorthInput>
    |> Result.bind (routeReportingCommandForTesting "NetWorth" [])
    |> Result.bind Json.fromJson<NetWorthReturn>

let private netWorthReportPath asOf interpolate fileName =
    runNetWorth asOf (OutputSpecifier.Report { baseDir = outputDir; interpolateAsOf = interpolate; fileName = fileName })
    |> Result.bind (function
        | NetWorthReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
        | NetWorthReturn.DataOnly _ -> Error(TestingError "Expected Report but got DataOnly"))

// ---- Investment wealth history ----

let private runWealthHistory beginDate endDate grouping (reportOutput: OutputSpecifier) =
    ({ beginDate = beginDate; endDate = endDate; grouping = grouping; reportOutput = reportOutput } : InvestmentWealthHistoryInput)
    |> Json.toJson<InvestmentWealthHistoryInput>
    |> Result.bind (routeReportingCommandForTesting "InvestmentWealthHistory" [])
    |> Result.bind Json.fromJson<InvestmentWealthHistoryReturn>

let private wealthReportPath beginDate endDate interpolate fileName =
    runWealthHistory beginDate endDate "Region"
        (OutputSpecifier.Report { baseDir = outputDir; interpolateAsOf = interpolate; fileName = fileName })
    |> Result.bind (function
        | InvestmentWealthHistoryReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
        | InvestmentWealthHistoryReturn.DataOnly _ -> Error(TestingError "Expected Report but got DataOnly"))

/// Writes the report, checks the path is the one expected and that the file is new, and hands back its HTML.
let private writtenFresh (expectedPath: string) (write: unit -> Result<string, IAppError>) =
    File.Delete expectedPath
    Assert.False(File.Exists expectedPath)
    write ()
    |> Result.map (fun path ->
        try
            Assert.Equal(expectedPath, path)
            Assert.True(Path.IsPathFullyQualified path, $"{path} is not fully qualified")
            Assert.True(File.Exists path, $"no file at {path}")
            File.ReadAllText path
        finally
            File.Delete path)

[<Collection("SharedTestData")>]
type PositionsReportRoutesTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-RPT-8.6 the NetWorth route in data-only mode returns the same rows and totals as the net worth computation for the same date, each ledger account's name beside its code`` () =
        result {
            let! returned = runNetWorth p.monthEnd3 OutputSpecifier.DataOnly
            let! row =
                match returned with
                | NetWorthReturn.DataOnly row -> Ok row
                | NetWorthReturn.Report _ -> Error(TestingError "Expected DataOnly but got Report")
            let! computed = computeNetWorth (fresh ()) p.monthEnd3
            let ledgerRows (rows: LedgerAccountBalance list) = rows |> List.map (fun r -> r.code, r.name, r.balance |> Money.amount)
            let returnedRows (rows: NetWorthLedgerAccountReturnRow list) = rows |> List.map (fun r -> r.code, r.name, r.balance)
            Assert.Equal(computed.asOf, row.asOf)
            Assert.NotEmpty(row.assetAccounts)
            Assert.Equal<(string * string * decimal) list>(computed.assetAccounts |> ledgerRows, row.assetAccounts |> returnedRows)
            Assert.Equal<(string * string * decimal) list>(computed.liabilityAccounts |> ledgerRows, row.liabilityAccounts |> returnedRows)
            Assert.Equal<(string * string list * string * string * LocalDate * string * decimal * decimal option) list>(
                computed.investmentAccounts
                |> List.map (fun a ->
                    a.investmentAccountName, a.ownerNames, a.accountGroup, a.taxTreatment |> TaxTreatment.toString,
                    a.snapshotDate, a.provenance |> Provenance.toString, a.marketValue |> Money.amount,
                    a.contributionBasis |> Option.map Money.amount),
                row.investmentAccounts
                |> List.map (fun a ->
                    a.accountName, a.owners, a.accountGroup, a.taxTreatment, a.snapshotDate, a.provenance, a.marketValue,
                    a.contributionBasis))
            Assert.Equal<(string * string * string list * decimal * LocalDate option * bool * (string * string * decimal) list * decimal) list>(
                computed.properties
                |> List.map (fun x ->
                    x.propertyName, x.propertyUse |> PropertyUse.toString, x.ownerNames, x.value |> Money.amount,
                    (match x.valueSource with
                     | ValuationDated d -> Some d
                     | PurchaseBasisValue -> None),
                    (x.valueSource = PurchaseBasisValue), x.mortgageAccounts |> ledgerRows, x.equity |> Money.amount),
                row.properties
                |> List.map (fun x ->
                    x.propertyName, x.propertyUse, x.owners, x.value, x.valuationDate, x.valueIsPurchaseBasis,
                    x.mortgageAccounts |> returnedRows, x.equity))
            Assert.Equal<decimal list>(
                [ computed.totalLedgerAssets; computed.totalInvestments; computed.totalPropertyValues
                  computed.totalLiabilities; computed.netWorth; computed.investableWealth ]
                |> List.map Money.amount,
                [ row.totalLedgerAssets; row.totalInvestments; row.totalPropertyValues; row.totalLiabilities; row.netWorth
                  row.investableWealth ])
            Assert.Equal<(string * decimal) list>(
                computed.investmentsByTaxTreatment |> List.map (fun (t, m) -> t |> TaxTreatment.toString, m |> Money.amount),
                row.investmentsByTaxTreatment |> List.map (fun g -> g.group, g.marketValue))
            Assert.Equal<(string * decimal) list>(
                computed.investmentsByAccountGroup |> List.map (fun (g, m) -> g, m |> Money.amount),
                row.investmentsByAccountGroup |> List.map (fun g -> g.group, g.marketValue))
            Assert.Contains(("F-1275", "Fixture Positions Cash", 5000.00M), row.assetAccounts |> returnedRows)
            let residence = row.properties |> List.find (fun x -> x.propertyName = PF.residence)
            Assert.Equal<(string * string * decimal) list>(
                [ "F-2310", "Fixture Residence Mortgage", 300000.00M ],
                residence.mortgageAccounts |> returnedRows)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-2.3 the NetWorth route in report mode writes a new HTML file and returns its fully qualified path`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-8-6-net-worth.html"))
            (fun () -> netWorthReportPath p.monthEnd3 false "rpt-8-6-net-worth")
        |> Result.map (fun html -> Assert.Contains(PF.residence, html))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-2.4 the NetWorth report file with date interpolation is the base directory and file name followed by a hyphen, the as-of date as yyyy-MM-dd, and .html`` () =
        writtenFresh
            (Path.Combine(outputDir, $"rpt-8-6-net-worth-interpolated-{dateText p.monthEnd3}.html"))
            (fun () -> netWorthReportPath p.monthEnd3 true "rpt-8-6-net-worth-interpolated")
        |> Result.map ignore
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-3.1 the rendered net worth header shows the report title and the as-of date`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-8-6-net-worth-header.html"))
            (fun () -> netWorthReportPath p.monthEnd3 false "rpt-8-6-net-worth-header")
        |> Result.map (fun html ->
            let header = headerOf html
            Assert.Equal("Net Worth", titleIn header)
            Assert.Contains(dateText p.monthEnd3, header))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.5 the InvestmentWealthHistory route in data-only mode returns the same points and group totals as the wealth history computation for the same range and grouping`` () =
        result {
            let! returned = runWealthHistory p.monthEnd4 p.monthEnd1 "Owners" OutputSpecifier.DataOnly
            let! points =
                match returned with
                | InvestmentWealthHistoryReturn.DataOnly points -> Ok points
                | InvestmentWealthHistoryReturn.Report _ -> Error(TestingError "Expected DataOnly but got Report")
            let! computed = computeInvestmentWealthHistory (fresh ()) p.monthEnd4 p.monthEnd1 ByOwners
            let groupOf =
                function
                | GroupName n -> Named n
                | GroupOwners owners -> WealthGroupContract.Owners owners
                | WealthGroup.Unassigned -> WealthGroupContract.Unassigned
            Assert.Equal(4, computed |> List.length)
            Assert.Equal<(LocalDate * (WealthGroupContract * decimal) list * decimal) list>(
                computed
                |> List.map (fun pt ->
                    pt.monthEnd, pt.totals |> List.map (fun (g, m) -> groupOf g, m |> Money.amount), pt.total |> Money.amount),
                points |> List.map (fun pt -> pt.monthEnd, pt.totals |> List.map (fun t -> t.group, t.marketValue), pt.total))
            Assert.Contains(
                (WealthGroupContract.Owners [ PF.alex; PF.sam ], 5500.00M),
                points |> List.find (fun pt -> pt.monthEnd = p.monthEnd3) |> _.totals |> List.map (fun t -> t.group, t.marketValue))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-2.3 the InvestmentWealthHistory route in report mode writes a new HTML file and returns its fully qualified path`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-9-5-wealth-history.html"))
            (fun () -> wealthReportPath p.monthEnd4 p.monthEnd1 false "rpt-9-5-wealth-history")
        |> Result.map (fun html -> Assert.Contains("International", html))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-2.4 the InvestmentWealthHistory report file with date interpolation is the base directory and file name followed by a hyphen, the begin and end dates as yyyy-MM-dd_yyyy-MM-dd, and .html`` () =
        writtenFresh
            (Path.Combine(outputDir, $"rpt-9-5-wealth-history-interpolated-{dateText p.monthEnd4}_{dateText p.monthEnd1}.html"))
            (fun () -> wealthReportPath p.monthEnd4 p.monthEnd1 true "rpt-9-5-wealth-history-interpolated")
        |> Result.map ignore
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history shows the report title and the begin and end dates in its header, and a table with one row per month-end and one column per group value`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-9-5-wealth-history-table.html"))
            (fun () -> wealthReportPath p.monthEnd4 p.monthEnd1 false "rpt-9-5-wealth-history-table")
        |> Result.map (fun html ->
            let header = headerOf html
            Assert.Equal("Investment Wealth History", titleIn header)
            Assert.Contains(dateText p.monthEnd4, header)
            Assert.Contains(dateText p.monthEnd1, header)
            (* By Region the fixture's lines fall in Domestic (the total market fund), International (the international
               fund) and Unassigned (the bond and stable value funds, which have no region). *)
            Assert.Equal<string list>([ "Month end"; "Domestic"; "International"; "Unassigned"; "Total" ], html |> cellsIn "th")
            let rows = Regex.Matches(html, "<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline) |> Seq.map _.Groups.[1].Value |> List.ofSeq
            let dataRows = rows |> List.filter (fun r -> r.Contains "<td")
            Assert.Equal<string list>(
                [ p.monthEnd4; p.monthEnd3; p.monthEnd2; p.monthEnd1 ] |> List.map dateText,
                dataRows |> List.map (fun r -> r |> cellsIn "td" |> List.head))
            dataRows |> List.iter (fun r -> Assert.Equal(5, r |> cellsIn "td" |> List.length)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.1 an InvestmentWealthHistory payload naming a grouping outside account, account group, tax treatment, owners and the seven dimensions is rejected with a typed error naming the text given`` () =
        runWealthHistory p.monthEnd4 p.monthEnd1 "Colour" OutputSpecifier.DataOnly
        |> expectError
            (function AsError (PositionsInvalidWealthGrouping raw) -> Some raw | _ -> None)
            (fun raw -> Assert.Equal("Colour", raw))

    [<Fact>]
    member _.``REQ-RPT-8.5 REQ-RPT-8.6 the rendered net worth Totals block shows the owned-property mortgages total, and the counted ledger assets, investments and property values it shows less its liabilities and owned-property mortgages equal the net worth it shows, with a nonzero owned-property mortgages total`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-8-5-net-worth-totals.html"))
            (fun () -> netWorthReportPath p.monthEnd3 false "rpt-8-5-net-worth-totals")
        |> Result.map (fun html ->
            // the Totals block runs from its heading to the end of its table; each row is a label and an amount
            let heading = Regex.Match(html, "<h2[^>]*>Totals</h2>")
            let start = if heading.Success then heading.Index else -1
            Assert.True(start >= 0, "the rendered net worth has no Totals block")
            let block = html.Substring(start, html.IndexOf("</table>", start) - start)
            let totals =
                Regex.Matches(block, "<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline)
                |> Seq.map (fun m -> m.Groups.[1].Value |> cellsIn "td")
                |> Seq.choose (function
                    | [ label; shown ] ->
                        Some(label, System.Decimal.Parse(shown, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture))
                    | _ -> None)
                |> Map.ofSeq
            let shown label =
                match totals |> Map.tryFind label with
                | Some value -> value
                | None -> failwith $"the Totals block shows no '{label}' among {totals |> Map.keys |> List.ofSeq}"
            // the mortgages of 12 Example Street (300,000.00) and 34 Example Avenue (180,000.00) as of the end of month -3
            Assert.Equal(480000.00M, shown "Mortgages of owned properties")
            Assert.Equal(
                shown "Net worth",
                shown "Counted ledger assets" + shown "Investments" + shown "Property values" - shown "Liabilities"
                - shown "Mortgages of owned properties"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.5 the rendered wealth history shows 0.00, not a blank cell, for a group at a month-end where it has no holdings`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-9-5-wealth-history-zero.html"))
            (fun () -> wealthReportPath p.monthEnd4 p.monthEnd1 false "rpt-9-5-wealth-history-zero")
        |> Result.map (fun html ->
            (* By Region at the end of month -1 nothing International is held: Alex Brokerage's d4 snapshot dropped the
               international fund and Old Brokerage has ended. Domestic: 1,380.00 + 500.00 + 5,750.00 + 2,152.50;
               Unassigned: 1,000.00 + 2,075.29. *)
            Assert.Equal<string list>([ "Month end"; "Domestic"; "International"; "Unassigned"; "Total" ], html |> cellsIn "th")
            let lastRow =
                Regex.Matches(html, "<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline)
                |> Seq.map (fun m -> m.Groups.[1].Value |> cellsIn "td")
                |> Seq.filter (fun cells -> cells |> List.tryHead = Some(dateText p.monthEnd1))
                |> Seq.exactlyOne
            Assert.Equal<string list>([ dateText p.monthEnd1; "9,782.50"; "0.00"; "3,075.29"; "12,857.79" ], lastRow))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history header shows the begin date, end date and grouping, and no as-of date`` () =
        writtenFresh
            (Path.Combine(outputDir, "rpt-9-5-wealth-history-range-header.html"))
            (fun () ->
                runWealthHistory p.monthEnd4 p.monthEnd2 "TaxTreatment"
                    (OutputSpecifier.Report { baseDir = outputDir; interpolateAsOf = false; fileName = "rpt-9-5-wealth-history-range-header" })
                |> Result.bind (function
                    | InvestmentWealthHistoryReturn.Report pathReturn -> Ok pathReturn.fullyQualifiedPath
                    | InvestmentWealthHistoryReturn.DataOnly _ -> Error(TestingError "Expected Report but got DataOnly")))
        |> Result.map (fun html ->
            let header = headerOf html
            let text = Regex.Replace(header.Substring(header.IndexOf("</h1>")), "<[^>]+>", "").Trim()
            Assert.Equal("Investment Wealth History", titleIn header)
            // the begin and end dates are the only dates shown, so no as-of date stands beside them
            Assert.Equal<string list>(
                [ dateText p.monthEnd4; dateText p.monthEnd2 ],
                Regex.Matches(header, @"\d{4}-\d{2}-\d{2}") |> Seq.map _.Value |> List.ofSeq)
            Assert.Contains("TaxTreatment", text)
            Assert.DoesNotContain("As of", text))
        |> railroadWrapper
