module Tests.Integrated.InterfaceBridge.PositionsRoutes

open System
open System.Text.Json.Nodes
open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open App.Utility.UtilityError
open Business.FinancialServices
open Business.FinancialServices.BizFinServError
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration.HoldingsAsOf
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.EntityFunctions
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module C = Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
module PF = PositionsFixture

(* Every Positions route that writes commits. So a test that writes makes its own entities, named uniquely, through
   the routes; reads back after the route returns, through the routes or a fresh context; and deletes what it made by
   name in a finally. Tests that only read use the fixture, whose contents are in Tests.Helpers/PositionsFixture.fs. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private unique (prefix: string) = $"{prefix} {Guid.NewGuid():N}"

let private send domain verb (payload: string) = routeUiCommandForTesting domain verb [] payload

let private call<'i, 'r> domain verb (input: 'i) : Result<'r, IAppError> =
    input |> Json.toJson<'i> |> Result.bind (send domain verb) |> Result.bind Json.fromJson<'r>

/// The input as JSON with one field taken out, for payloads that leave a required field off.
let private without (field: string) (input: 'i) : Result<string, IAppError> =
    input
    |> Json.toJson<'i>
    |> Result.map (fun json ->
        let node = JsonNode.Parse(json).AsObject()
        Assert.True(node.Remove field, $"the payload has no field {field} to take out")
        node.ToJsonString())

let private missingField = function AsError (JsonDeserializationFailed (_, message, _)) -> Some message | _ -> None
let private codeNotFound = function AsError (AccountCodeDoesntMatchAccountId code) -> Some code | _ -> None

let private cleanUp (cleanUps: (unit -> Result<unit, IAppError>) list) = cleanUps |> cleanUpAll |> railroadWrapper

let private ledger code name : C.LedgerAccountReturn = { code = code; name = name }

// ---- Dimension Values ----

let private dimensionValue dimension name : C.DimensionValueReturn = { dimension = dimension; valueName = name }

let private listDimension dimension =
    call<C.DimensionValueListInput, C.DimensionValueReturn list> "DimensionValue" "List" { dimension = dimension }

// ---- Securities ----

let private uniqueTicker () = $"RT{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant()

let private createSecurity name ticker values =
    call<C.SecurityCreateInput, C.SecurityReturn> "Security" "Create" { securityName = name; ticker = ticker; dimensionValues = values }

let private updateSecurity (input: C.SecurityUpdateInput) = call<C.SecurityUpdateInput, C.SecurityReturn> "Security" "Update" input

let private securitySummary (s: C.SecurityReturn) =
    s.securityName, s.ticker, s.dimensionValues |> List.map (fun v -> v.dimension, v.valueName)

let private listSecurities () =
    send "Security" "List" "" |> Result.bind Json.fromJson<C.SecurityReturn list>

let private storedSecurity name =
    listSecurities () |> Result.map (List.filter (fun s -> s.securityName = name) >> List.map securitySummary)

// ---- Investment Accounts ----

type private AccountSummary =
    string * string * string * string * string list * LocalDate * LocalDate option * C.LedgerAccountReturn option

let private accountSummary (a: C.InvestmentAccountReturn) : AccountSummary =
    a.accountName, a.institution, a.accountGroup, a.taxTreatment, a.owners, a.activeBegin, a.activeEnd, a.ledgerAccount

let private listAccounts () =
    send "InvestmentAccount" "List" "" |> Result.bind Json.fromJson<C.InvestmentAccountReturn list>

let private storedAccount name =
    listAccounts () |> Result.map (List.filter (fun a -> a.accountName = name) >> List.map accountSummary)

let private createAccount (input: C.InvestmentAccountCreateInput) =
    call<C.InvestmentAccountCreateInput, C.InvestmentAccountReturn> "InvestmentAccount" "Create" input

let private updateAccount (input: C.InvestmentAccountUpdateInput) =
    call<C.InvestmentAccountUpdateInput, C.InvestmentAccountReturn> "InvestmentAccount" "Update" input

let private noAccountChange name : C.InvestmentAccountUpdateInput =
    { accountName = name
      accountNameUpdate = NoChange
      institutionUpdate = NoChange
      accountGroupUpdate = NoChange
      taxTreatmentUpdate = NoChange
      ownersUpdate = NoChange
      activeBeginUpdate = NoChange
      activeEndUpdate = NoChange
      ledgerAccountCodeUpdate = NoChange }

// ---- Holdings ----

let private createHolding account security basisMethod =
    call<C.HoldingCreateInput, C.HoldingReturn> "Holding" "Create"
        { accountName = account; securityName = security; basisMethod = basisMethod }

let private holdingSummary (h: C.HoldingReturn) = h.accountName, h.securityName, h.basisMethod

let private listHoldings (account: string option) =
    call<C.HoldingListInput, C.HoldingReturn list> "Holding" "List" { accountName = account }

// ---- Account Snapshots ----

let private line security quantity price marketValue cost : C.AccountSnapshotLineContract =
    { securityName = security; quantity = quantity; price = price; marketValue = marketValue; reportedCostBasis = cost }

let private snapshotOf account date lines : C.AccountSnapshotInput =
    { accountName = account; snapshotDate = date; provenance = "Reported"; contributionBasis = None; lines = lines }

let private record (snapshots: C.AccountSnapshotInput list) =
    call<C.AccountSnapshotRecordInput, C.RecordedAccountSnapshotReturn list> "AccountSnapshot" "Record" { snapshots = snapshots }

let private lineSummary (l: C.AccountSnapshotLineContract) = l.securityName, l.quantity, l.price, l.marketValue, l.reportedCostBasis

let private listDates account beginDate endDate =
    call<C.AccountSnapshotListDatesInput, C.AccountSnapshotDateReturn list> "AccountSnapshot" "ListDates"
        { accountName = account; beginDate = beginDate; endDate = endDate }
    |> Result.map (List.map (fun d -> d.snapshotDate, d.provenance))

let private fetchSnapshot account date =
    call<C.AccountSnapshotFetchInput, C.AccountSnapshotReturn> "AccountSnapshot" "Fetch" { accountName = account; snapshotDate = date }

// ---- Properties ----

type private PropertySummary =
    string * string * string list * LocalDate * LocalDate option * decimal * C.LedgerAccountReturn option * C.LedgerAccountReturn list

let private propertySummary (p: C.PropertyReturn) : PropertySummary =
    p.propertyName, p.propertyUse, p.owners, p.acquisitionDate, p.disposalDate, p.purchaseBasis, p.assetAccount, p.mortgageAccounts

let private listProperties () = send "Property" "List" "" |> Result.bind Json.fromJson<C.PropertyReturn list>

let private storedProperty name =
    listProperties () |> Result.map (List.filter (fun p -> p.propertyName = name) >> List.map propertySummary)

let private createProperty (input: C.PropertyCreateInput) = call<C.PropertyCreateInput, C.PropertyReturn> "Property" "Create" input

let private updateProperty (input: C.PropertyUpdateInput) = call<C.PropertyUpdateInput, C.PropertyReturn> "Property" "Update" input

let private noPropertyChange name : C.PropertyUpdateInput =
    { propertyName = name
      propertyNameUpdate = NoChange
      propertyUseUpdate = NoChange
      ownersUpdate = NoChange
      acquisitionDateUpdate = NoChange
      disposalDateUpdate = NoChange
      purchaseBasisUpdate = NoChange
      assetAccountCodeUpdate = NoChange
      mortgageAccountCodesUpdate = NoChange }

// ---- Valuations ----

let private recordValuation property date value basis =
    call<C.ValuationRecordInput, C.ValuationReturn> "Valuation" "Record"
        { propertyName = property; valuationDate = date; value = value; basis = basis }

let private listValuations property =
    call<C.ValuationListInput, C.ValuationReturn list> "Valuation" "List" { propertyName = property }

let private valuationSummary (v: C.ValuationReturn) = v.propertyName, v.valuationDate, v.value, v.basis

[<Collection("SharedTestData")>]
type PositionsRoutesTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    /// A Taxable account owned by Alex, active from the fixture's begin date with no end and no ledger link.
    let accountInput name : C.InvestmentAccountCreateInput =
        { accountName = name
          institution = "Example Route Bank"
          accountGroup = "Route Group"
          taxTreatment = "Taxable"
          owners = [ PF.alex ]
          activeBegin = p.accountsActiveBegin
          activeEnd = None
          ledgerAccountCode = None }

    /// A route-made account holding the fixture's total market fund at average cost.
    let accountHoldingTotalMarket name =
        result {
            let! _ = createAccount (accountInput name)
            let! _ = createHolding name PF.totalMarket (Some "AverageCost")
            return ()
        }

    /// A Rental owned by Sam, acquired on the fixture's rental date, with no asset account and the given mortgages.
    let propertyInput name mortgages : C.PropertyCreateInput =
        { propertyName = name
          propertyUse = "Rental"
          owners = [ PF.sam ]
          acquisitionDate = p.rentalAcquired
          disposalDate = None
          purchaseBasis = 180000.00M
          assetAccountCode = None
          mortgageAccountCodes = mortgages }

    // ---- Dimension Values ----

    [<Fact>]
    member _.``REQ-POS-11.1 a DimensionValue Create payload creates the value in the named dimension, and the return carries its dimension and name`` () =
        let name = unique "Route Sector"
        try
            result {
                let! returned =
                    call<C.DimensionValueCreateInput, C.DimensionValueReturn> "DimensionValue" "Create"
                        { dimension = "Sector"; valueName = name }
                Assert.Equal(dimensionValue "Sector" name, returned)
                let! listed = listDimension "Sector"
                Assert.Contains(dimensionValue "Sector" name, listed)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpDimensionValueByName "Sector" name ]

    [<Fact>]
    member _.``REQ-POS-2.1 a DimensionValue Create payload naming a dimension outside the seven is rejected with a typed error naming the text given`` () =
        let name = unique "Route Colour"
        try
            call<C.DimensionValueCreateInput, C.DimensionValueReturn> "DimensionValue" "Create"
                { dimension = "Colour"; valueName = name }
            |> expectError (function AsError (PositionsInvalidDimension raw) -> Some raw | _ -> None) (fun raw -> Assert.Equal("Colour", raw))
        finally
            cleanUp [ fun () -> cleanUpDimensionValueByName "Colour" name ]

    [<Fact>]
    member _.``REQ-POS-11.1 a DimensionValue Rename payload renames the value addressed by dimension and current name, and the return carries the new name`` () =
        let name = unique "Route Objective"
        let newName = unique "Route Objective renamed"
        try
            result {
                let! _ =
                    call<C.DimensionValueCreateInput, C.DimensionValueReturn> "DimensionValue" "Create"
                        { dimension = "Objective"; valueName = name }
                let! returned =
                    call<C.DimensionValueRenameInput, C.DimensionValueReturn> "DimensionValue" "Rename"
                        { dimension = "Objective"; currentName = name; newName = newName }
                Assert.Equal(dimensionValue "Objective" newName, returned)
                let! listed = listDimension "Objective"
                Assert.Contains(dimensionValue "Objective" newName, listed)
                Assert.DoesNotContain(dimensionValue "Objective" name, listed)
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpDimensionValueByName "Objective" name); (fun () -> cleanUpDimensionValueByName "Objective" newName) ]

    [<Fact>]
    member _.``REQ-POS-11.1 the DimensionValue List route returns every value of the named dimension ordered by name, and none of another dimension`` () =
        result {
            let! listed = listDimension "Region"
            Assert.Equal<C.DimensionValueReturn list>(
                [ dimensionValue "Region" "Domestic"; dimensionValue "Region" "International" ],
                listed)
        }
        |> railroadWrapper

    // ---- Securities ----

    [<Fact>]
    member _.``REQ-POS-11.2 a Security Create payload creates the Security with its ticker and Dimension Values, and the return carries each of them`` () =
        let name = unique "Route Security"
        let ticker = uniqueTicker ()
        try
            result {
                let! returned =
                    createSecurity name (Some ticker)
                        [ dimensionValue "Region" "International"; dimensionValue "InvestmentType" "Equity Fund" ]
                let expected =
                    name, Some ticker, [ "InvestmentType", "Equity Fund"; "Region", "International" ]
                Assert.Equal(expected, returned |> securitySummary)
                let! stored = storedSecurity name
                Assert.Equal<(string * string option * (string * string) list) list>([ expected ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpSecurityByName name ]

    [<Fact>]
    member _.``REQ-POS-11.2 a Security Update payload naming the name and one dimension changes those two, leaves the ticker and the other dimensions unchanged, and the return carries the result`` () =
        let name = unique "Route Security"
        let newName = unique "Route Security renamed"
        let ticker = uniqueTicker ()
        try
            result {
                let! _ =
                    createSecurity name (Some ticker)
                        [ dimensionValue "InvestmentType" "Equity Fund"; dimensionValue "Region" "Domestic" ]
                let! returned =
                    updateSecurity
                        { securityName = name
                          securityNameUpdate = SetTo newName
                          tickerUpdate = NoChange
                          dimensionValueUpdates = [ { dimension = "Region"; valueName = Some "International" } ] }
                let expected =
                    newName, Some ticker, [ "InvestmentType", "Equity Fund"; "Region", "International" ]
                Assert.Equal(expected, returned |> securitySummary)
                let! stored = storedSecurity newName
                Assert.Equal<(string * string option * (string * string) list) list>([ expected ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpSecurityByName name); (fun () -> cleanUpSecurityByName newName) ]

    [<Fact>]
    member _.``REQ-POS-11.2 REQ-SYS-6.1 a Security Update payload naming no field is rejected with a typed no-change error, and the Security is unchanged`` () =
        let name = unique "Route Security"
        let ticker = uniqueTicker ()
        try
            result {
                let! created = createSecurity name (Some ticker) [ dimensionValue "InvestmentType" "Bond Fund" ]
                updateSecurity { securityName = name; securityNameUpdate = NoChange; tickerUpdate = NoChange; dimensionValueUpdates = [] }
                |> expectError (function AsError PositionsSecurityUpdateNoOp -> Some() | _ -> None) ignore
                let! stored = storedSecurity name
                Assert.Equal<(string * string option * (string * string) list) list>([ created |> securitySummary ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpSecurityByName name ]

    [<Fact>]
    member _.``REQ-POS-11.2 REQ-SYS-6.1 a Security Update payload naming only the stored ticker succeeds, leaving the Security as stored`` () =
        let name = unique "Route Security"
        let ticker = uniqueTicker ()
        try
            result {
                let! created = createSecurity name (Some ticker) [ dimensionValue "InvestmentType" "Bond Fund" ]
                let! returned =
                    updateSecurity
                        { securityName = name; securityNameUpdate = NoChange; tickerUpdate = SetTo(Some ticker); dimensionValueUpdates = [] }
                Assert.Equal(created |> securitySummary, returned |> securitySummary)
                let! stored = storedSecurity name
                Assert.Equal<(string * string option * (string * string) list) list>([ created |> securitySummary ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpSecurityByName name ]

    [<Fact>]
    member _.``REQ-POS-11.2 the Security List route returns every Security ordered by name, with its ticker and its value in each dimension`` () =
        result {
            let! listed = listSecurities ()
            Assert.Equal<(string * string option * (string * string) list) list>(
                [ PF.bondFund, None, [ "InvestmentType", "Bond Fund" ]
                  PF.international, Some "EXINX", [ "InvestmentType", "Equity Fund"; "Region", "International" ]
                  PF.stableValue, None, []
                  PF.totalMarket,
                  Some "EXTMX",
                  [ "InvestmentType", "Equity Fund"
                    "MarketCap", "Large Cap"
                    "IndexType", "Total Market"
                    "Sector", "Diversified"
                    "Region", "Domestic"
                    "Objective", "Growth"
                    "Benchmark", "Example Total Market Index" ] ],
                listed |> List.map securitySummary)
        }
        |> railroadWrapper

    // ---- Investment Accounts ----

    [<Fact>]
    member _.``REQ-POS-11.3 an InvestmentAccount Create payload creates the account, and the return carries every field with the linked ledger account's name beside its code`` () =
        let name = unique "Route Account"
        let activeEnd = p.accountsActiveBegin.PlusYears(3)
        try
            result {
                let! returned =
                    createAccount
                        { accountInput name with
                            owners = [ PF.sam; PF.alex ]
                            activeEnd = Some activeEnd
                            ledgerAccountCode = Some "F-1250" }
                let expected =
                    name, "Example Route Bank", "Route Group", "Taxable", [ PF.alex; PF.sam ], p.accountsActiveBegin, Some activeEnd,
                    Some(ledger "F-1250" "Roth IRA")
                Assert.Equal(expected, returned |> accountSummary)
                let! stored = storedAccount name
                Assert.Equal<AccountSummary list>([ expected ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-4.4 an InvestmentAccount Create payload with a tax treatment outside the four is rejected with a typed error naming the text given`` () =
        let name = unique "Route Account"
        try
            result {
                createAccount { accountInput name with taxTreatment = "Pension" }
                |> expectError
                    (function AsError (PositionsInvalidTaxTreatment raw) -> Some raw | _ -> None)
                    (fun raw -> Assert.Equal("Pension", raw))
                let! stored = storedAccount name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-4.7 an InvestmentAccount Create payload with no active begin is rejected with a typed error naming the missing active begin`` () =
        let name = unique "Route Account"
        try
            result {
                let! payload = accountInput name |> without "activeBegin"
                send "InvestmentAccount" "Create" payload
                |> expectError missingField (fun message -> Assert.Contains("activeBegin", message))
                let! stored = storedAccount name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-4.8 REQ-NGUI-1.4 an InvestmentAccount Create payload whose ledger account code matches no account fails with a typed error naming the code, and no account is created`` () =
        let name = unique "Route Account"
        try
            result {
                createAccount { accountInput name with ledgerAccountCode = Some "F-9919" }
                |> expectError codeNotFound (fun code -> Assert.Equal("F-9919", code))
                let! stored = storedAccount name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.3 an InvestmentAccount Update payload naming the institution and the owners changes those two, leaves every other field unchanged, and the return carries the result`` () =
        let name = unique "Route Account"
        try
            result {
                let! _ = createAccount { accountInput name with ledgerAccountCode = Some "F-1250" }
                let! returned =
                    updateAccount
                        { noAccountChange name with
                            institutionUpdate = SetTo "Example Other Bank"
                            ownersUpdate = SetTo [ PF.sam; PF.alex ] }
                let expected =
                    name, "Example Other Bank", "Route Group", "Taxable", [ PF.alex; PF.sam ], p.accountsActiveBegin, None,
                    Some(ledger "F-1250" "Roth IRA")
                Assert.Equal(expected, returned |> accountSummary)
                let! stored = storedAccount name
                Assert.Equal<AccountSummary list>([ expected ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-SYS-6.1 an InvestmentAccount Update payload naming no field is rejected with a typed no-change error, and the account is unchanged`` () =
        let name = unique "Route Account"
        try
            result {
                let! created = createAccount (accountInput name)
                updateAccount (noAccountChange name)
                |> expectError (function AsError PositionsInvestmentAccountUpdateNoOp -> Some() | _ -> None) ignore
                let! stored = storedAccount name
                Assert.Equal<AccountSummary list>([ created |> accountSummary ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-SYS-6.1 an InvestmentAccount Update payload naming only the stored institution succeeds, leaving the account as stored`` () =
        let name = unique "Route Account"
        try
            result {
                let! created = createAccount (accountInput name)
                let! returned = updateAccount { noAccountChange name with institutionUpdate = SetTo "Example Route Bank" }
                Assert.Equal(created |> accountSummary, returned |> accountSummary)
                let! stored = storedAccount name
                Assert.Equal<AccountSummary list>([ created |> accountSummary ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-NGUI-1.6 the InvestmentAccount List route returns every account ordered by name, each linked ledger account's name beside its code`` () =
        result {
            let! listed = listAccounts ()
            Assert.Equal<(string * string list * C.LedgerAccountReturn option) list>(
                [ PF.alexBrokerage, [ PF.alex ], Some(ledger "F-1260" "Fixture Brokerage at Cost")
                  PF.alexRoth, [ PF.alex ], None
                  PF.jointBrokerage, [ PF.alex; PF.sam ], None
                  PF.jordanCustodial, [ PF.jordan ], None
                  PF.oldBrokerage, [ PF.sam ], None
                  PF.sam401k, [ PF.sam ], None
                  PF.samHsa, [ PF.sam ], None ],
                listed |> List.map (fun a -> a.accountName, a.owners, a.ledgerAccount))
        }
        |> railroadWrapper

    // ---- Holdings ----

    [<Fact>]
    member _.``REQ-POS-11.5 a Holding Create payload creates the Holding, and the return carries the account, Security and basis method`` () =
        let name = unique "Route Account"
        try
            result {
                let! _ = createAccount (accountInput name)
                let! returned = createHolding name PF.international (Some "SpecificLot")
                Assert.Equal((name, PF.international, Some "SpecificLot"), returned |> holdingSummary)
                let! listed = listHoldings (Some name)
                Assert.Equal<(string * string * string option) list>(
                    [ name, PF.international, Some "SpecificLot" ],
                    listed |> List.map holdingSummary)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.5 a Holding UpdateBasisMethod payload changes the basis method, and the return carries the new one`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                let! returned =
                    call<C.HoldingUpdateBasisMethodInput, C.HoldingReturn> "Holding" "UpdateBasisMethod"
                        { accountName = name; securityName = PF.totalMarket; basisMethod = Some "SpecificLot" }
                Assert.Equal((name, PF.totalMarket, Some "SpecificLot"), returned |> holdingSummary)
                let! listed = listHoldings (Some name)
                Assert.Equal<(string * string * string option) list>(
                    [ name, PF.totalMarket, Some "SpecificLot" ],
                    listed |> List.map holdingSummary)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.5 the Holding List route with no account filter returns every Holding ordered by account name then Security name`` () =
        result {
            let! listed = listHoldings None
            Assert.Equal<(string * string * string option) list>(
                [ PF.alexBrokerage, PF.international, Some "SpecificLot"
                  PF.alexBrokerage, PF.totalMarket, Some "AverageCost"
                  PF.alexRoth, PF.bondFund, None
                  PF.alexRoth, PF.totalMarket, None
                  PF.jointBrokerage, PF.totalMarket, Some "AverageCost"
                  PF.oldBrokerage, PF.international, Some "AverageCost"
                  PF.sam401k, PF.bondFund, None
                  PF.sam401k, PF.totalMarket, None
                  PF.samHsa, PF.stableValue, None ],
                listed |> List.map holdingSummary)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.5 the Holding List route with an account filter returns every Holding of that account ordered by Security name, and none of another account`` () =
        result {
            let! listed = listHoldings (Some PF.sam401k)
            Assert.Equal<(string * string * string option) list>(
                [ PF.sam401k, PF.bondFund, None; PF.sam401k, PF.totalMarket, None ],
                listed |> List.map holdingSummary)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 REQ-NGUI-1.6 the Holding FetchAsOf route returns the same accounts and lines as the holdings-as-of computation for the date given, each linked ledger account's name beside its code`` () =
        result {
            let! returned =
                call<C.HoldingFetchAsOfInput, C.HoldingsAsOfAccountReturn list> "Holding" "FetchAsOf" { asOf = p.monthEnd3 }
            let! computed = fetchHoldingsAsOf (fresh ()) p.monthEnd3
            let fromComputed =
                computed
                |> List.map (fun a ->
                    a.investmentAccountName,
                    a.snapshotDate,
                    a.contributionBasis |> Option.map Money.amount,
                    a.lines
                    |> List.map (fun l ->
                        l.securityName,
                        l.quantity |> Quantity.amount,
                        l.price |> Price.amount,
                        l.marketValue |> Money.amount,
                        l.reportedCostBasis |> Option.map Money.amount))
            let fromRoute =
                returned
                |> List.map (fun a ->
                    a.accountName,
                    a.snapshotDate,
                    a.contributionBasis,
                    a.lines |> List.map (fun l -> l.securityName, l.quantity, l.price, l.marketValue, l.reportedCostBasis))
            Assert.NotEmpty(fromComputed)
            Assert.Equal<(string * LocalDate * decimal option * (string * decimal * decimal * decimal * decimal option) list) list>(
                fromComputed,
                fromRoute)
            Assert.Equal<(string * C.LedgerAccountReturn option) list>(
                computed
                |> List.map (fun a ->
                    a.investmentAccountName, a.ledgerAccountCodeAndName |> Option.map (fun (code, name) -> ledger code name)),
                returned |> List.map (fun a -> a.accountName, a.ledgerAccount))
            Assert.Contains((PF.alexBrokerage, Some(ledger "F-1260" "Fixture Brokerage at Cost")),
                            returned |> List.map (fun a -> a.accountName, a.ledgerAccount))
        }
        |> railroadWrapper

    // ---- Account Snapshots ----

    [<Fact>]
    member _.``REQ-POS-7.1 REQ-POS-7.3 an AccountSnapshot Record payload with one new snapshot and one for a date already recorded records both and returns each as stored, the first marked new and the second marked a replacement`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                let! _ = record [ snapshotOf name p.d1 [ line PF.totalMarket 10M 100.00M 1000.00M (Some 900.00M) ] ]
                let! returned =
                    record
                        [ snapshotOf name p.d2 [ line PF.totalMarket 10M 110.00M 1100.00M (Some 900.00M) ]
                          snapshotOf name p.d1 [ line PF.totalMarket 11M 100.00M 1100.00M (Some 1000.00M) ] ]
                Assert.Equal<(LocalDate * bool * (string * decimal * decimal * decimal * decimal option) list) list>(
                    [ p.d2, false, [ PF.totalMarket, 10M, 110M, 1100M, Some 900M ]
                      p.d1, true, [ PF.totalMarket, 11M, 100M, 1100M, Some 1000M ] ],
                    returned
                    |> List.map (fun r -> r.snapshot.snapshotDate, r.replacedExisting, r.snapshot.lines |> List.map lineSummary))
                let! replaced = fetchSnapshot name p.d1
                Assert.Equal<(string * decimal * decimal * decimal * decimal option) list>(
                    [ PF.totalMarket, 11M, 100M, 1100M, Some 1000M ],
                    replaced.lines |> List.map lineSummary)
                let! dates = listDates name p.d1 p.d4
                Assert.Equal<(LocalDate * string) list>([ p.d1, "Reported"; p.d2, "Reported" ], dates)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-7.1 an AccountSnapshot Record payload whose second snapshot has a line outside the tolerance records neither snapshot, as read back after the route returns`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                record
                    [ snapshotOf name p.d1 [ line PF.totalMarket 10M 100.00M 1000.00M None ]
                      snapshotOf name p.d2 [ line PF.totalMarket 10M 100.00M 1000.06M None ] ]
                |> expectError
                    (function AsError (PositionsSnapshotLineOutsideTolerance (_, date, security, _, marketValue)) -> Some(date, security, marketValue) | _ -> None)
                    (fun found -> Assert.Equal((p.d2, PF.totalMarket, 1000.06M), found))
                let! dates = listDates name p.d1 p.d4
                Assert.Empty(dates)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-QP-1.3 an AccountSnapshot Record payload with a seven-decimal quantity is rejected with a typed error naming the value, and nothing is recorded`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                record [ snapshotOf name p.d1 [ line PF.totalMarket 1.1234567M 100.00M 112.35M None ] ]
                |> expectError
                    (function AsError (QuantityFailedToConvertImproperPrecision raw) -> Some raw | _ -> None)
                    (fun raw -> Assert.Equal("1.1234567", raw.ToString(Globalization.CultureInfo.InvariantCulture)))
                let! dates = listDates name p.d1 p.d4
                Assert.Empty(dates)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-QP-2.3 an AccountSnapshot Record payload with a seven-decimal price is rejected with a typed error naming the value, and nothing is recorded`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                record [ snapshotOf name p.d1 [ line PF.totalMarket 10M 10.1234567M 101.23M None ] ]
                |> expectError
                    (function AsError (PriceFailedToConvertImproperPrecision raw) -> Some raw | _ -> None)
                    (fun raw -> Assert.Equal("10.1234567", raw.ToString(Globalization.CultureInfo.InvariantCulture)))
                let! dates = listDates name p.d1 p.d4
                Assert.Empty(dates)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-6.7 an AccountSnapshot Record payload's six-decimal quantity and price come back in the return exactly as sent`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                let! returned = record [ snapshotOf name p.d1 [ line PF.totalMarket 210.123456M 9.876543M 2075.29M None ] ]
                let returnedLine = returned |> List.exactlyOne |> _.snapshot.lines |> List.exactlyOne
                Assert.Equal(210.123456M, returnedLine.quantity)
                Assert.Equal(9.876543M, returnedLine.price)
                let! fetched = fetchSnapshot name p.d1
                let fetchedLine = fetched.lines |> List.exactlyOne
                Assert.Equal(210.123456M, fetchedLine.quantity)
                Assert.Equal(9.876543M, fetchedLine.price)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-7.4 an AccountSnapshot Delete payload deletes the snapshot for the account and date given, after which that account and date has no snapshot and the account's other snapshots remain`` () =
        let name = unique "Route Account"
        try
            result {
                do! accountHoldingTotalMarket name
                let! _ =
                    record
                        [ snapshotOf name p.d1 [ line PF.totalMarket 10M 100.00M 1000.00M None ]
                          snapshotOf name p.d2 [ line PF.totalMarket 10M 110.00M 1100.00M None ] ]
                let! deleted =
                    call<C.AccountSnapshotDeleteInput, C.AccountSnapshotReturn> "AccountSnapshot" "Delete"
                        { accountName = name; snapshotDate = p.d1 }
                Assert.Equal(p.d1, deleted.snapshotDate)
                Assert.Equal<(string * decimal * decimal * decimal * decimal option) list>(
                    [ PF.totalMarket, 10M, 100M, 1000M, None ],
                    deleted.lines |> List.map lineSummary)
                fetchSnapshot name p.d1
                |> expectError
                    (function AsError (PositionsSnapshotDoesntExist (account, date)) -> Some(account, date) | _ -> None)
                    (fun found -> Assert.Equal((name, p.d1), found))
                let! dates = listDates name p.d1 p.d4
                Assert.Equal<(LocalDate * string) list>([ p.d2, "Reported" ], dates)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-7.5 an AccountSnapshot Fetch payload returns the provenance, contribution basis and every line of the snapshot for the account and date given, exactly as recorded`` () =
        result {
            let! fetched = fetchSnapshot PF.alexRoth p.d1
            Assert.Equal((PF.alexRoth, p.d1, "Reported", Some 1200.00M), (fetched.accountName, fetched.snapshotDate, fetched.provenance, fetched.contributionBasis))
            Assert.Equal<(string * decimal * decimal * decimal * decimal option) list>(
                [ PF.bondFund, 100M, 10M, 1000M, None; PF.totalMarket, 5M, 100M, 500M, None ],
                fetched.lines |> List.map lineSummary)
            let! imported = fetchSnapshot PF.sam401k p.d1
            Assert.Equal(("Imported", None), (imported.provenance, imported.contributionBasis))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.5 an AccountSnapshot ListDates payload returns the account's snapshot dates and provenance in the range given, in date order, and no date outside the range`` () =
        result {
            let! dates = listDates PF.alexBrokerage p.d2 p.d3
            Assert.Equal<(LocalDate * string) list>([ p.d2, "Reported"; p.d3, "Reported" ], dates)
            let! samDates = listDates PF.sam401k p.d1 p.d4
            Assert.Equal<(LocalDate * string) list>([ p.d1, "Imported"; p.d3, "Reported" ], samDates)
        }
        |> railroadWrapper

    // ---- Properties ----

    [<Fact>]
    member _.``REQ-POS-11.6 a Property Create payload creates the Property, and the return carries every field with each linked ledger account's name beside its code`` () =
        let name = unique "Route Property"
        let mutable assetAccountId = None
        let disposed = p.rentalAcquired.PlusYears(5)
        try
            result {
                let! _, accountId =
                    runCommandRouteAndAutoCompleteTransaction FetchOnly (fun context ->
                        createTestAccountFromPrimitives
                            context "T-1591" "Route Cabin at Cost" "Asset" p.accountsActiveBegin None (Some "FixedAsset")
                            (Some fixture.Data.assets1000Id) None)
                assetAccountId <- Some accountId
                let! returned =
                    createProperty
                        { propertyInput name [ "F-2230"; "F-2220" ] with
                            owners = [ PF.sam; PF.alex ]
                            disposalDate = Some disposed
                            purchaseBasis = 175000.50M
                            assetAccountCode = Some "T-1591" }
                let expected =
                    name, "Rental", [ PF.alex; PF.sam ], p.rentalAcquired, Some disposed, 175000.50M,
                    Some(ledger "T-1591" "Route Cabin at Cost"),
                    [ ledger "F-2220" "Credit Card"; ledger "F-2230" "Fixture Loan Payable" ]
                Assert.Equal(expected, returned |> propertySummary)
                let! stored = storedProperty name
                Assert.Equal<PropertySummary list>([ expected ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpPropertyByName name); (fun () -> cleanUpAccountId assetAccountId) ]

    [<Fact>]
    member _.``REQ-POS-9.4 a Property Create payload with no acquisition date is rejected with a typed error naming the missing acquisition date`` () =
        let name = unique "Route Property"
        try
            result {
                let! payload = propertyInput name [] |> without "acquisitionDate"
                send "Property" "Create" payload
                |> expectError missingField (fun message -> Assert.Contains("acquisitionDate", message))
                let! stored = storedProperty name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-9.5 a Property Create payload with no purchase basis is rejected with a typed error naming the missing purchase basis`` () =
        let name = unique "Route Property"
        try
            result {
                let! payload = propertyInput name [] |> without "purchaseBasis"
                send "Property" "Create" payload
                |> expectError missingField (fun message -> Assert.Contains("purchaseBasis", message))
                let! stored = storedProperty name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-9.7 REQ-NGUI-1.4 a Property Create payload whose asset account code matches no account fails with a typed error naming the code`` () =
        let name = unique "Route Property"
        try
            result {
                createProperty { propertyInput name [] with assetAccountCode = Some "F-1599" }
                |> expectError codeNotFound (fun code -> Assert.Equal("F-1599", code))
                let! stored = storedProperty name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-9.8 REQ-NGUI-1.4 a Property Create payload whose mortgage account code matches no account fails with a typed error naming the code`` () =
        let name = unique "Route Property"
        try
            result {
                createProperty (propertyInput name [ "F-2230"; "F-2399" ])
                |> expectError codeNotFound (fun code -> Assert.Equal("F-2399", code))
                let! stored = storedProperty name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-11.6 a Property Update payload naming the purchase basis and the mortgage accounts changes those two, leaves every other field unchanged, and the return carries the result`` () =
        let name = unique "Route Property"
        try
            result {
                let! _ = createProperty (propertyInput name [ "F-2210" ])
                let! returned =
                    updateProperty
                        { noPropertyChange name with
                            purchaseBasisUpdate = SetTo 222222.22M
                            mortgageAccountCodesUpdate = SetTo [ "F-2230"; "F-2220" ] }
                let expected =
                    name, "Rental", [ PF.sam ], p.rentalAcquired, None, 222222.22M, None,
                    [ ledger "F-2220" "Credit Card"; ledger "F-2230" "Fixture Loan Payable" ]
                Assert.Equal(expected, returned |> propertySummary)
                let! stored = storedProperty name
                Assert.Equal<PropertySummary list>([ expected ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-SYS-6.1 a Property Update payload naming no field is rejected with a typed no-change error, and the Property is unchanged`` () =
        let name = unique "Route Property"
        try
            result {
                let! created = createProperty (propertyInput name [ "F-2210" ])
                updateProperty (noPropertyChange name)
                |> expectError (function AsError PositionsPropertyUpdateNoOp -> Some() | _ -> None) ignore
                let! stored = storedProperty name
                Assert.Equal<PropertySummary list>([ created |> propertySummary ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-SYS-6.1 a Property Update payload naming only the stored use succeeds, leaving the Property as stored`` () =
        let name = unique "Route Property"
        try
            result {
                let! created = createProperty (propertyInput name [ "F-2210" ])
                let! returned = updateProperty { noPropertyChange name with propertyUseUpdate = SetTo "Rental" }
                Assert.Equal(created |> propertySummary, returned |> propertySummary)
                let! stored = storedProperty name
                Assert.Equal<PropertySummary list>([ created |> propertySummary ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-NGUI-1.6 the Property List route returns every Property ordered by name, each linked ledger account's name beside its code`` () =
        result {
            let! listed = listProperties ()
            Assert.Equal<(string * C.LedgerAccountReturn option * C.LedgerAccountReturn list) list>(
                [ PF.residence, Some(ledger "F-1510" "Fixture Residence at Cost"), [ ledger "F-2310" "Fixture Residence Mortgage" ]
                  PF.rental, None, [ ledger "F-2320" "Fixture Rental Mortgage" ]
                  PF.formerResidence, None, [] ],
                listed |> List.map (fun x -> x.propertyName, x.assetAccount, x.mortgageAccounts))
        }
        |> railroadWrapper

    // ---- Valuations ----

    [<Fact>]
    member _.``REQ-POS-11.8 a Valuation Record payload records the Valuation, and the return carries its date, value and basis`` () =
        let name = unique "Route Property"
        try
            result {
                let! _ = createProperty (propertyInput name [])
                let! returned = recordValuation name p.d2 191500.25M "Route appraisal"
                Assert.Equal((name, p.d2, 191500.25M, "Route appraisal"), returned |> valuationSummary)
                let! listed = listValuations name
                Assert.Equal<(string * LocalDate * decimal * string) list>(
                    [ name, p.d2, 191500.25M, "Route appraisal" ],
                    listed |> List.map valuationSummary)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-11.8 a Valuation Delete payload deletes the Valuation for the Property and date given, after which the Property has none on that date and its other Valuations remain`` () =
        let name = unique "Route Property"
        try
            result {
                let! _ = createProperty (propertyInput name [])
                let! _ = recordValuation name p.d1 185000.00M "Route appraisal"
                let! _ = recordValuation name p.d3 187500.00M "Route comparables"
                let! deleted =
                    call<C.ValuationDeleteInput, C.ValuationReturn> "Valuation" "Delete" { propertyName = name; valuationDate = p.d1 }
                Assert.Equal((name, p.d1, 185000.00M, "Route appraisal"), deleted |> valuationSummary)
                let! listed = listValuations name
                Assert.Equal<(string * LocalDate * decimal * string) list>(
                    [ name, p.d3, 187500.00M, "Route comparables" ],
                    listed |> List.map valuationSummary)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPropertyByName name ]

    [<Fact>]
    member _.``REQ-POS-11.8 the Valuation List route returns every Valuation of the named Property in date order, and none of another Property`` () =
        result {
            let! listed = listValuations PF.residence
            Assert.Equal<(string * LocalDate * decimal * string) list>(
                [ PF.residence, p.valuation1, 420000.00M, "Appraisal"
                  PF.residence, p.valuation2, 430000.00M, "Comparable sales" ],
                listed |> List.map valuationSummary)
            let! rentalListed = listValuations PF.rental
            Assert.Empty(rentalListed)
        }
        |> railroadWrapper

    // Placeholders committed before the Src was read (audit 2026-10-04a remediation)

    [<Fact>]
    member _.``REQ-POS-11.10 a Holding Delete payload removes only the Holding of the account and Security given: the Holding List route no longer returns it and still returns the account's other Holdings`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.11 a Property Delete payload removes only the Property named: the Property List route no longer returns it and still returns every other Property`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-POS-9.7 a Property Create payload with two asset accounts creates the Property linked to both, and the return and the Property List route each carry both codes with their names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-POS-9.7 a Property Update payload giving a new set of two asset accounts replaces the stored set, so the Property is linked to exactly those two and not to the one it had`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.7 a Property Create payload giving the same asset account code twice is rejected with a typed error naming the code, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.7 a Property Create payload naming as an asset account one already linked to another Property is rejected with a typed error naming the code and that Property, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.7 for each of an Asset account of a subtype other than FixedAsset and an account of a type other than Asset, a Property Create payload naming it as an asset account is rejected with a typed error naming the code and what is wrong, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.8 a Property Create payload giving the same mortgage account code twice is rejected with a typed error naming the code, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.1 REQ-SYS-6.1 an AccountSnapshot Record payload with an empty list of snapshots is rejected with a typed no-snapshots error`` () =
        Assert.Fail "Not yet implemented"
