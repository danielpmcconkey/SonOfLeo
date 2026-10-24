module Tests.Integrated.InterfaceBridge.BalanceAccountRoutes

open System
open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices.Positions.PositionsError
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module C = Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
module PF = PositionsFixture

(* Every Balance Account route that writes commits. So a test that writes makes its own Balance Accounts, named
   uniquely, through the routes; reads back after the route returns, through the routes; and deletes what it made by
   name in a finally. Tests that only read use the fixture's Balance Accounts (see Tests.Helpers/PositionsFixture.fs). *)

let private unique (prefix: string) = $"{prefix} {Guid.NewGuid():N}"

let private send domain verb (payload: string) = routeUiCommandForTesting domain verb [] payload

let private call<'i, 'r> domain verb (input: 'i) : Result<'r, IAppError> =
    input |> Json.toJson<'i> |> Result.bind (send domain verb) |> Result.bind Json.fromJson<'r>

let private cleanUp (cleanUps: (unit -> Result<unit, IAppError>) list) = cleanUps |> cleanUpAll |> railroadWrapper

let private today () = Calendar.today ()

let private createBalanceAccount name kind (owners: string list) =
    call<C.BalanceAccountCreateInput, C.BalanceAccountReturn> "BalanceAccount" "Create"
        { balanceAccountName = name; kind = kind; institution = "Example Route Bank"; owners = owners }

let private listBalanceAccounts () =
    send "BalanceAccount" "List" "" |> Result.bind Json.fromJson<C.BalanceAccountReturn list>

let private record (entries: (string * LocalDate * decimal) list) =
    call<C.BalanceAccountBalanceRecordInput, C.RecordedBalanceAccountBalanceReturn list> "BalanceAccountBalance" "Record"
        { balances = entries |> List.map (fun (name, date, amount) -> { balanceAccountName = name; balanceDate = date; amount = amount }) }

let private listBalances name beginDate endDate =
    call<C.BalanceAccountBalanceListInput, C.BalanceAccountBalanceReturn list> "BalanceAccountBalance" "List"
        { balanceAccountName = name; beginDate = beginDate; endDate = endDate }

/// Every Balance the account has, as (date, amount).
let private allBalances name =
    listBalances name (LocalDate(1900, 1, 1)) (today ())
    |> Result.map (List.map (fun b -> b.balanceDate, b.amount))

/// A Balance Account as a route returns it: name, kind, institution and owners' names.
let private summary (b: C.BalanceAccountReturn) = b.balanceAccountName, b.kind, b.institution, b.owners

let private fixtureNames = set [ PF.savings; PF.checking; PF.card; PF.loan; PF.rentalMortgageBalanceAccount ]

[<Collection("SharedTestData")>]
type BalanceAccountRoutesTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-POS-16.7 a BalanceAccount Create payload creates the Balance Account, and the return carries its name, kind, institution and owners' names`` () =
        let name = unique "Route Savings"
        try
            createBalanceAccount name "Cash" [ PF.sam; PF.alex ]
            |> Result.map (fun created ->
                Assert.Equal((name, "Cash", "Example Route Bank", [ PF.alex; PF.sam ]), summary created))
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpBalanceAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-16.7 a BalanceAccount Update payload naming only the institution changes it, leaves every other field unchanged, and the return carries the result`` () =
        let name = unique "Route Card"
        try
            result {
                let! _ = createBalanceAccount name "Debt" [ PF.alex ]
                let! updated =
                    call<C.BalanceAccountUpdateInput, C.BalanceAccountReturn> "BalanceAccount" "Update"
                        { balanceAccountName = name
                          balanceAccountNameUpdate = NoChange
                          institutionUpdate = SetTo "Example Route Credit Union"
                          ownersUpdate = NoChange }
                let! listed = listBalanceAccounts ()
                Assert.Equal((name, "Debt", "Example Route Credit Union", [ PF.alex ]), summary updated)
                Assert.Equal<(string * string * string * string list) list>(
                    [ summary updated ], listed |> List.filter (fun b -> b.balanceAccountName = name) |> List.map summary)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpBalanceAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-16.7 the BalanceAccount List route returns every Balance Account ordered by name with its kind, institution and owners' names`` () =
        listBalanceAccounts ()
        |> Result.map (fun listed ->
            let names = listed |> List.map (fun b -> b.balanceAccountName)
            Assert.Equal<string list>(names |> List.sortWith (fun a b -> String.CompareOrdinal(a, b)), names)
            Assert.Equal<(string * string * string * string list) list>(
                [ PF.card, "Debt", "Example Card Issuer", [ PF.alex ]
                  PF.checking, "Cash", "Example Bank", [ PF.alex; PF.sam ]
                  PF.loan, "Debt", "Example Lender", [ PF.sam ]
                  PF.rentalMortgageBalanceAccount, "Debt", "Example Mortgage Lender", [ PF.sam ]
                  PF.savings, "Cash", "Example Bank", [ PF.alex; PF.sam ] ],
                listed |> List.filter (fun b -> fixtureNames |> Set.contains b.balanceAccountName) |> List.map summary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.10 a BalanceAccount Delete payload removes only the Balance Account named: the BalanceAccount List route no longer returns it and still returns every other`` () =
        let doomed = unique "Route Doomed"
        let kept = unique "Route Kept"
        try
            result {
                let! _ = createBalanceAccount doomed "Cash" [ PF.sam ]
                let! _ = createBalanceAccount kept "Cash" [ PF.sam ]
                let! deleted =
                    call<C.BalanceAccountDeleteInput, C.BalanceAccountReturn> "BalanceAccount" "Delete" { balanceAccountName = doomed }
                let! listed = listBalanceAccounts ()
                let names = listed |> List.map (fun b -> b.balanceAccountName) |> Set.ofList
                Assert.Equal(doomed, deleted.balanceAccountName)
                Assert.DoesNotContain(doomed, names)
                Assert.True(Set.isSubset (fixtureNames |> Set.add kept) names, "a Balance Account other than the one deleted is missing")
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpBalanceAccountByName doomed); (fun () -> cleanUpBalanceAccountByName kept) ]

    [<Fact>]
    member _.``REQ-POS-16.8 a BalanceAccountBalance Record payload records each Balance and returns each with its Balance Account's name, the date, the amount and whether it replaced one`` () =
        let name = unique "Route Loan"
        try
            result {
                let! _ = createBalanceAccount name "Debt" [ PF.sam ]
                let! first = record [ name, p.d1, 900.00M; name, p.d2, 800.00M ]
                let! second = record [ name, p.d1, 950.00M ]
                let shown (r: C.RecordedBalanceAccountBalanceReturn) =
                    r.balance.balanceAccountName, r.balance.balanceDate, r.balance.amount, r.replacedExisting
                Assert.Equal<(string * LocalDate * decimal * bool) list>(
                    [ name, p.d1, 900.00M, false; name, p.d2, 800.00M, false ], first |> List.map shown)
                Assert.Equal<(string * LocalDate * decimal * bool) list>([ name, p.d1, 950.00M, true ], second |> List.map shown)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpBalanceAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-16.8 a BalanceAccountBalance Record payload whose second Balance is dated after today records neither, as read back after the route returns`` () =
        let name = unique "Route Wallet"
        try
            result {
                let! _ = createBalanceAccount name "Cash" [ PF.sam ]
                record [ name, p.d1, 10.00M; name, (today ()).PlusDays(1), 20.00M ]
                |> expectError
                    (function AsError (PositionsBalanceDateLaterThanCurrentDate (n, d)) -> Some(n, d) | _ -> None)
                    (fun found -> Assert.Equal((name, (today ()).PlusDays(1)), found))
                let! balances = allBalances name
                Assert.Empty(balances)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpBalanceAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-16.9 a BalanceAccountBalance Delete payload deletes the Balance for the Balance Account and date given, after which the BalanceAccountBalance List route no longer returns it and still returns the account's other Balances`` () =
        let name = unique "Route Checking"
        try
            result {
                let! _ = createBalanceAccount name "Cash" [ PF.sam ]
                let! _ = record [ name, p.d1, 100.00M; name, p.d2, 200.00M ]
                let! deleted =
                    call<C.BalanceAccountBalanceDeleteInput, C.BalanceAccountBalanceReturn> "BalanceAccountBalance" "Delete"
                        { balanceAccountName = name; balanceDate = p.d1 }
                let! remaining = allBalances name
                Assert.Equal((name, p.d1, 100.00M), (deleted.balanceAccountName, deleted.balanceDate, deleted.amount))
                Assert.Equal<(LocalDate * decimal) list>([ p.d2, 200.00M ], remaining)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpBalanceAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-11.6 a Property Create payload naming two mortgage Balance Accounts creates the Property linked to both, and the return and the Property List route each carry their names`` () =
        let propertyName = unique "Route Cabin"
        let second = unique "Route Second Mortgage"
        let first = unique "Route First Mortgage"
        try
            result {
                let! _ = createBalanceAccount second "Debt" [ PF.sam ]
                let! _ = createBalanceAccount first "Debt" [ PF.sam ]
                let! created =
                    call<C.PropertyCreateInput, C.PropertyReturn> "Property" "Create"
                        { propertyName = propertyName
                          propertyUse = "Rental"
                          owners = [ PF.sam ]
                          acquisitionDate = p.rentalAcquired
                          disposalDate = None
                          purchaseBasis = 120000.00M
                          assetAccountCodes = []
                          mortgageAccountCodes = []
                          mortgageBalanceAccountNames = [ second; first ] }
                let! listed = send "Property" "List" "" |> Result.bind Json.fromJson<C.PropertyReturn list>
                let expected = [ first; second ] |> List.sort
                Assert.Equal<string list>(expected, created.mortgageBalanceAccounts)
                Assert.Equal<string list list>(
                    [ expected ],
                    listed |> List.filter (fun x -> x.propertyName = propertyName) |> List.map (fun x -> x.mortgageBalanceAccounts))
            }
            |> railroadWrapper
        finally
            cleanUp
                [ (fun () -> cleanUpPropertyByName propertyName)
                  (fun () -> cleanUpBalanceAccountByName first)
                  (fun () -> cleanUpBalanceAccountByName second) ]
