module Tests.Integrated.CrossDomainOrchestration.BalanceAccountMaintenance

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General.BizGeneralError
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

(* The fixture's Balance Accounts (see PositionsFixture.fs), created in the order Fixture Savings, Fixture Checking,
   Fixture Card, Fixture Loan, Fixture Rental Mortgage; by name they run Card, Checking, Loan, Rental Mortgage, Savings.
   Fixture Loan (Debt, Sam, Example Lender) has Balances of 1,500.00 on monthEnd8, 0.00 on monthEnd7 and 0.00 on
   ledgerStart. Fixture Rental Mortgage is 34 Example Avenue's mortgage Balance Account. *)

/// A Balance Account as the list shows it: name, kind, institution and owners' names.
type private BalanceAccountSummary = string * string * string * string list

let private summary context (b: BalanceAccount.BalanceAccount) : BalanceAccountSummary =
    b |> BalanceAccount.balanceAccountName |> BalanceAccountName.value,
    b |> BalanceAccount.kind |> BalanceAccountKind.toString,
    b |> BalanceAccount.institution |> Institution.value,
    b |> BalanceAccount.owners |> PositionsLookups.personNamesOf context

let private listed context name =
    BalanceAccountOrchestration.listBalanceAccounts context
    |> Result.map (List.map (summary context) >> List.filter (fun (n, _, _, _) -> n = name))

let private listedOne context name = listed context name |> Result.map List.exactlyOne

// the route path: names are resolved to IDs as the route's converters resolve them
let private create context name kind institution (owners: string list) =
    result {
        let! name = BalanceAccountName.create name
        let! institution = Institution.create institution
        let! owners = owners |> PositionsLookups.personIdsOf context
        return! BalanceAccountOrchestration.constructNewAndPersist context name kind institution owners
    }

let private noChange name : BalanceAccountUpdateInput =
    { balanceAccountName = name; balanceAccountNameUpdate = NoChange; institutionUpdate = NoChange; ownersUpdate = NoChange }

let private update context (input: BalanceAccountUpdateInput) =
    input
    |> ``convert [BalanceAccountUpdateInput] to [BalanceAccountFieldUpdates]`` context
    |> Result.bind (BalanceAccountOrchestration.updateBalanceAccount context)

let private record context (entries: (string * LocalDate * decimal) list) =
    entries
    |> List.map (fun (name, date, amount) ->
        ({ balanceAccountName = name; balanceDate = date; amount = amount }: BalanceAccountBalanceEntryInput)
        |> ``convert [BalanceAccountBalanceEntryInput] to [BalanceInput]`` context)
    |> convertListOfResultsToResultsList
    |> Result.bind (BalanceAccountOrchestration.recordBalances context)

/// Each recorded Balance: its Balance Account's name, date, amount and whether it replaced one.
let private recordedSummary context (recorded: BalanceAccountOrchestration.RecordedBalance list) =
    recorded
    |> List.map (fun r ->
        let b = r.balance
        (b |> BalanceAccountBalance.balanceAccountId |> BalanceAccount.fetchById context |> mustBe
         |> BalanceAccount.balanceAccountName |> BalanceAccountName.value),
        b |> BalanceAccountBalance.balanceDate,
        b |> BalanceAccountBalance.amount |> Money.amount,
        r.replacedExisting)

/// The account's Balances in the range as (date, amount).
let private balancesOf context name beginDate endDate =
    PositionsLookups.balanceAccountIdOf context name
    |> Result.bind (fun id -> BalanceAccountOrchestration.listBalances context id beginDate endDate)
    |> Result.map (List.map (fun b -> b |> BalanceAccountBalance.balanceDate, b |> BalanceAccountBalance.amount |> Money.amount))

/// Every Balance the account has, as (date, amount).
let private allBalancesOf context name = balancesOf context name (LocalDate(1900, 1, 1)) (LocalDate(2999, 12, 31))

let private deleteByName context name =
    PositionsLookups.balanceAccountIdOf context name |> Result.bind (BalanceAccountOrchestration.deleteBalanceAccount context)

let private today (context: Context.Context) = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

let private nameDoesntMatch = function AsError (PositionsBalanceAccountNameDoesntMatch n) -> Some n | _ -> None

[<Collection("SharedTestData")>]
type BalanceAccountMaintenanceTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-POS-16.7 REQ-POS-16.4 creating a Balance Account stores its name, kind, institution and owners, and the list returns each of them with its owners' names`` () =
        runCommandRouteAndAutoRollback PositionsCreateBalanceAccount (fun context ->
            result {
                let! _ = create context "Example Joint Savings" BalanceAccountKind.Cash "Example Credit Union" [ PF.sam; PF.alex ]
                let! found = listedOne context "Example Joint Savings"
                Assert.Equal<BalanceAccountSummary>(
                    ("Example Joint Savings", "Cash", "Example Credit Union", [ PF.alex; PF.sam ]), found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.1 creating a Balance Account whose name exactly matches an existing one's is rejected with a typed error naming the name, and no second Balance Account is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateBalanceAccount (fun context ->
            result {
                create context PF.checking BalanceAccountKind.Cash "Another Bank" [ PF.sam ]
                |> expectError
                    (function AsError (PositionsBalanceAccountNameAlreadyExists n) -> Some n | _ -> None)
                    (fun n -> Assert.Equal(PF.checking, n))
                let! found = listed context PF.checking
                Assert.Equal<BalanceAccountSummary list>([ PF.checking, "Cash", "Example Bank", [ PF.alex; PF.sam ] ], found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.1 a Balance Account whose name differs from an existing one's only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PositionsCreateBalanceAccount (fun context ->
            result {
                let! _ = create context "FIXTURE CHECKING" BalanceAccountKind.Cash "Example Bank" [ PF.sam ]
                let! upper = listed context "FIXTURE CHECKING"
                let! original = listed context PF.checking
                Assert.Equal((1, 1), (upper.Length, original.Length))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.4 a Balance Account given no owners is rejected with a typed error naming the Balance Account`` () =
        runCommandRouteAndAutoRollback PositionsCreateBalanceAccount (fun context ->
            create context "Nobody's Savings" BalanceAccountKind.Cash "Example Bank" []
            |> expectError
                (function AsError (PositionsBalanceAccountHasNoOwners n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Nobody's Savings", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.4 a Balance Account given the same Person twice among its owners is rejected with a typed error naming the Person`` () =
        runCommandRouteAndAutoRollback PositionsCreateBalanceAccount (fun context ->
            create context "Twice Savings" BalanceAccountKind.Cash "Example Bank" [ PF.sam; PF.sam ]
            |> expectError
                (function AsError (PositionsBalanceAccountOwnerRepeated (n, person)) -> Some(n, person) | _ -> None)
                (fun found -> Assert.Equal(("Twice Savings", PF.sam), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.4 REQ-PER-2.4 a Balance Account with an owner name that matches no Person fails with a typed error naming that name`` () =
        runCommandRouteAndAutoRollback PositionsCreateBalanceAccount (fun context ->
            create context "Ghost Savings" BalanceAccountKind.Cash "Example Bank" [ PF.sam; "Ghost Example" ]
            |> expectError
                (function AsError (PersonNameDoesntMatchId n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Ghost Example", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.7 updating a Balance Account addressed by its current name changes its name, institution and owners, and the list shows every new value`` () =
        runCommandRouteAndAutoRollback PositionsUpdateBalanceAccount (fun context ->
            result {
                let! _ =
                    update
                        context
                        { noChange PF.card with
                            balanceAccountNameUpdate = SetTo "Example Travel Card"
                            institutionUpdate = SetTo "Example Travel Bank"
                            ownersUpdate = SetTo [ PF.sam; PF.jordan ] }
                let! renamed = listedOne context "Example Travel Card"
                let! old = listed context PF.card
                Assert.Equal<BalanceAccountSummary>(
                    ("Example Travel Card", "Debt", "Example Travel Bank", [ PF.jordan; PF.sam ]), renamed)
                Assert.Empty(old)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.7 an update giving a Balance Account's owners replaces the set whole: an owner left out is gone and a new owner is present`` () =
        runCommandRouteAndAutoRollback PositionsUpdateBalanceAccount (fun context ->
            result {
                let! _ = update context { noChange PF.checking with ownersUpdate = SetTo [ PF.jordan; PF.sam ] }
                let! (_, _, _, owners) = listedOne context PF.checking
                Assert.Equal<string list>([ PF.jordan; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.2 a Debt Balance Account updated in its name, institution and owners is still of kind Debt, and its Balances keep their amounts`` () =
        runCommandRouteAndAutoRollback PositionsUpdateBalanceAccount (fun context ->
            result {
                let! _ =
                    update
                        context
                        { noChange PF.loan with
                            balanceAccountNameUpdate = SetTo "Example Car Loan"
                            institutionUpdate = SetTo "Example Auto Lender"
                            ownersUpdate = SetTo [ PF.alex ] }
                let! (_, kind, _, _) = listedOne context "Example Car Loan"
                let! balances = allBalancesOf context "Example Car Loan"
                Assert.Equal("Debt", kind)
                Assert.Equal<(LocalDate * decimal) list>(
                    [ p.monthEnd8, 1500.00M; p.monthEnd7, 0.00M; p.ledgerStart, 0.00M ], balances)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.7 listing Balance Accounts returns every fixture Balance Account ordered by name, from a fixture set whose stored order differs from it, with its kind, institution and owners' names, and no other`` () =
        let context = Context.create NoTransaction FetchOnly
        BalanceAccountOrchestration.listBalanceAccounts context
        |> Result.map (fun found ->
            Assert.Equal<BalanceAccountSummary list>(
                [ PF.card, "Debt", "Example Card Issuer", [ PF.alex ]
                  PF.checking, "Cash", "Example Bank", [ PF.alex; PF.sam ]
                  PF.loan, "Debt", "Example Lender", [ PF.sam ]
                  PF.rentalMortgageBalanceAccount, "Debt", "Example Mortgage Lender", [ PF.sam ]
                  PF.savings, "Cash", "Example Bank", [ PF.alex; PF.sam ] ],
                found |> List.map (summary context)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 updating a Balance Account by a name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name`` () =
        runCommandRouteAndAutoRollback PositionsUpdateBalanceAccount (fun context ->
            update context { noChange "Missing Example Account" with institutionUpdate = SetTo "Example Bank" }
            |> expectError nameDoesntMatch (fun n -> Assert.Equal("Missing Example Account", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 recording a Balance for a Balance Account name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name, and nothing is recorded`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                record context [ PF.checking, p.d2, 3000.00M; "Missing Example Account", p.d2, 10.00M ]
                |> expectError nameDoesntMatch (fun n -> Assert.Equal("Missing Example Account", n))
                let! checking = balancesOf context PF.checking p.d2 p.d2
                Assert.Empty(checking)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.5 REQ-POS-16.8 recording a Balance for a Cash and one for a Debt Balance Account stores each and returns each as stored with its Balance Account, date and amount, marked as not replacing one`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let! recorded = record context [ PF.checking, p.d2, 3200.00M; PF.card, p.d3, 410.25M ]
                Assert.Equal<(string * LocalDate * decimal * bool) list>(
                    [ PF.checking, p.d2, 3200.00M, false; PF.card, p.d3, 410.25M, false ], recorded |> recordedSummary context)
                let! checking = balancesOf context PF.checking p.d2 p.d2
                let! card = balancesOf context PF.card p.d3 p.d3
                Assert.Equal<(LocalDate * decimal) list>([ p.d2, 3200.00M; p.d3, 410.25M ], checking @ card)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.5 a Balance of 0.00 and one of -15.00 are each recorded and listed exactly as recorded`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let! _ = record context [ PF.savings, p.d1, 0.00M; PF.savings, p.d2, -15.00M ]
                let! listed = balancesOf context PF.savings p.d1 p.d2
                Assert.Equal<(LocalDate * decimal) list>([ p.d1, 0.00M; p.d2, -15.00M ], listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.5 a Balance dated the operation's current date is recorded, and one dated the day after is rejected with a typed error naming the Balance Account and the date, and is not recorded`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let current = today context
                let! _ = record context [ PF.savings, current, 120.00M ]
                record context [ PF.savings, current.PlusDays(1), 130.00M ]
                |> expectError
                    (function AsError (PositionsBalanceDateLaterThanCurrentDate (n, d)) -> Some(n, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.savings, current.PlusDays(1)), found))
                let! listed = balancesOf context PF.savings current (current.PlusDays(1))
                Assert.Equal<(LocalDate * decimal) list>([ current, 120.00M ], listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.8 recording a Balance for a Balance Account and date that already has one replaces its amount, returns it marked as replacing one, and listing gives only the new amount for that date`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let! recorded = record context [ PF.loan, p.monthEnd8, 1450.00M ]
                Assert.Equal<(string * LocalDate * decimal * bool) list>(
                    [ PF.loan, p.monthEnd8, 1450.00M, true ], recorded |> recordedSummary context)
                let! listed = balancesOf context PF.loan p.monthEnd8 p.monthEnd8
                Assert.Equal<(LocalDate * decimal) list>([ p.monthEnd8, 1450.00M ], listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.8 one operation naming the same Balance Account and date twice is rejected with a typed error naming both, and neither Balance is recorded`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                record context [ PF.savings, p.d2, 50.00M; PF.savings, p.d2, 60.00M ]
                |> expectError
                    (function AsError (PositionsBalanceRepeatedInRequest (n, d)) -> Some(n, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.savings, p.d2), found))
                let! listed = balancesOf context PF.savings p.d2 p.d2
                Assert.Empty(listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.9 deleting the Balance for a Balance Account and date removes it and leaves the account's Balances on other dates`` () =
        runCommandRouteAndAutoRollback PositionsDeleteBalanceAccountBalance (fun context ->
            result {
                let! checkingId = PositionsLookups.balanceAccountIdOf context PF.checking
                let! deleted = BalanceAccountOrchestration.deleteBalance context checkingId p.monthEnd8
                let! remaining = allBalancesOf context PF.checking
                Assert.Equal(2000.00M, deleted |> BalanceAccountBalance.amount |> Money.amount)
                Assert.Equal<(LocalDate * decimal) list>([ p.monthEnd7, 2500.00M; p.ledgerStart, 0.00M ], remaining)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.9 deleting for a Balance Account and date with no Balance fails with a typed not-found error naming the Balance Account and the date`` () =
        runCommandRouteAndAutoRollback PositionsDeleteBalanceAccountBalance (fun context ->
            result {
                let! checkingId = PositionsLookups.balanceAccountIdOf context PF.checking
                BalanceAccountOrchestration.deleteBalance context checkingId p.d2
                |> expectError
                    (function AsError (PositionsBalanceDoesntExist (n, d)) -> Some(n, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.checking, p.d2), found))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.9 listing a Balance Account's Balances between two dates returns every Balance of that account dated in the range, both ends included, in date order, and none outside the range or of another account`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let! _ =
                    record
                        context
                        [ PF.savings, p.d4, 400.00M
                          PF.savings, p.d2, 200.00M
                          PF.savings, p.d1, 100.00M
                          PF.savings, p.d3, 300.00M
                          PF.checking, p.d2, 999.00M ]
                let! listed = balancesOf context PF.savings p.d2 p.d3
                Assert.Equal<(LocalDate * decimal) list>([ p.d2, 200.00M; p.d3, 300.00M ], listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.9 listing a Balance Account's Balances with the end date the day before the begin date fails with a typed error naming both dates`` () =
        balancesOf (Context.create NoTransaction FetchOnly) PF.savings p.d2 (p.d2.PlusDays(-1))
        |> expectError
            (function AsError (PositionsBalanceListEndBeforeBegin (b, e)) -> Some(b, e) | _ -> None)
            (fun found -> Assert.Equal((p.d2, p.d2.PlusDays(-1)), found))

    [<Fact>]
    member _.``REQ-POS-16.10 deleting a Balance Account with no Balance and no Property naming it removes it with its owners, and the list no longer returns it`` () =
        runCommandRouteAndAutoRollback PositionsDeleteBalanceAccount (fun context ->
            result {
                let! created = create context "Short-lived Savings" BalanceAccountKind.Cash "Example Bank" [ PF.alex; PF.sam ]
                let! deleted = deleteByName context "Short-lived Savings"
                let! found = listed context "Short-lived Savings"
                Assert.Equal(created |> BalanceAccount.balanceAccountId, deleted |> BalanceAccount.balanceAccountId)
                Assert.Empty(found)
                created |> BalanceAccount.balanceAccountId |> BalanceAccount.fetchById context
                |> expectError (function AsError (PositionsBalanceAccountIdDoesntExist _) -> Some() | _ -> None) ignore
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.10 deleting a Balance Account that has a Balance is rejected with a typed error naming the Balance Account, and it and its Balances remain`` () =
        runCommandRouteAndAutoRollback PositionsDeleteBalanceAccount (fun context ->
            result {
                deleteByName context PF.savings
                |> expectError
                    (function AsError (PositionsBalanceAccountHasBalances n) -> Some n | _ -> None)
                    (fun n -> Assert.Equal(PF.savings, n))
                let! found = listed context PF.savings
                let! balances = allBalancesOf context PF.savings
                Assert.Equal(1, found.Length)
                Assert.Equal<(LocalDate * decimal) list>([ p.monthEnd8, 100.00M; p.ledgerStart, 0.00M ], balances)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.10 deleting a Balance Account that is a Property's mortgage is rejected with a typed error naming the Balance Account and the Property, and it remains`` () =
        runCommandRouteAndAutoRollback PositionsDeleteBalanceAccount (fun context ->
            result {
                let! mortgage = create context "Cabin Mortgage" BalanceAccountKind.Debt "Example Mortgage Lender" [ PF.sam ]
                let! sam = PositionsLookups.personIdOf context PF.sam
                let! _ =
                    RealEstateOrchestration.constructNewAndPersist
                        context (toPropertyName "Example Cabin") Rental [ sam ] (toOwnedPeriod p.rentalAcquired None)
                        (toPurchaseBasis 90000.00M) [] [] [ mortgage |> BalanceAccount.balanceAccountId ]
                deleteByName context "Cabin Mortgage"
                |> expectError
                    (function AsError (PositionsBalanceAccountIsPropertyMortgage (n, property)) -> Some(n, property) | _ -> None)
                    (fun found -> Assert.Equal(("Cabin Mortgage", "Example Cabin"), found))
                let! found = listed context "Cabin Mortgage"
                Assert.Equal(1, found.Length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-16.10 REQ-POS-11.9 deleting a Balance Account by a name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name`` () =
        runCommandRouteAndAutoRollback PositionsDeleteBalanceAccount (fun context ->
            deleteByName context "Missing Example Account"
            |> expectError nameDoesntMatch (fun n -> Assert.Equal("Missing Example Account", n))
            |> Ok)
        |> railroadWrapper
