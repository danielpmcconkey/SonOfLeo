module Tests.Integrated.CrossDomainOrchestration.InvestmentAccountMaintenance

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General
open Business.General.BizGeneralError
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.InvestmentAccountOrchestration
open Business.CrossDomainOrchestration.HoldingOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// An account as the list shows it: name, institution, group, tax treatment, owners' names, linked ledger account,
/// active begin and active end.
type private AccountSummary =
    string * string * string * TaxTreatment * string list * (string * string) option * LocalDate * LocalDate option

let private summary (view: InvestmentAccountView) : AccountSummary =
    let a = view.investmentAccount
    a |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value,
    a |> InvestmentAccount.institution |> Institution.value,
    a |> InvestmentAccount.accountGroup |> AccountGroup.value,
    a |> InvestmentAccount.taxTreatment,
    view.ownerNames,
    view.ledgerAccountCodeAndName,
    a |> InvestmentAccount.activityPeriod |> ActivityPeriod.activeBegin,
    a |> InvestmentAccount.activityPeriod |> ActivityPeriod.activeEnd

let private listed context name =
    listInvestmentAccounts context
    |> Result.map (List.map summary >> List.filter (fun (n, _, _, _, _, _, _, _) -> n = name))

let private ownersOf context name =
    listed context name |> Result.map (List.exactlyOne >> fun (_, _, _, _, owners, _, _, _) -> owners)

type private NewAccount = {
    name: InvestmentAccountName
    institution: Institution
    accountGroup: AccountGroup
    taxTreatment: TaxTreatment
    owners: PersonComponent.PersonName list
    activityPeriod: ActivityPeriod.ActivityPeriod
    ledgerAccountId: AccountId option
}

type private AccountUpdate = {
    currentName: InvestmentAccountName
    nameUpdate: FieldUpdate<InvestmentAccountName>
    institutionUpdate: FieldUpdate<Institution>
    accountGroupUpdate: FieldUpdate<AccountGroup>
    taxTreatmentUpdate: FieldUpdate<TaxTreatment>
    ownersUpdate: FieldUpdate<PersonComponent.PersonName list>
    activeBeginUpdate: FieldUpdate<LocalDate>
    activeEndUpdate: FieldUpdate<LocalDate option>
    ledgerAccountIdUpdate: FieldUpdate<AccountId option>
}

let private newAccount name treatment (owners: string list) ledgerAccountId (activeBegin: LocalDate) activeEnd =
    { name = toAccountName name
      institution = toInstitution "Example Brokerage"
      accountGroup = toAccountGroup "Brokerage"
      taxTreatment = treatment
      owners = owners |> List.map toPersonName
      activityPeriod = toActivityPeriod activeBegin activeEnd
      ledgerAccountId = ledgerAccountId }

let private noChange name =
    { currentName = toAccountName name
      nameUpdate = NoChange
      institutionUpdate = NoChange
      accountGroupUpdate = NoChange
      taxTreatmentUpdate = NoChange
      ownersUpdate = NoChange
      activeBeginUpdate = NoChange
      activeEndUpdate = NoChange
      ledgerAccountIdUpdate = NoChange }

// the route path: owners are addressed by name and resolved to IDs as the route's converter resolves them
let private createInvestmentAccount context (a: NewAccount) =
    result {
        let! owners = a.owners |> List.map PersonComponent.PersonName.value |> PositionsLookups.personIdsOf context
        return!
            InvestmentAccountOrchestration.constructNewAndPersist
                context a.name a.institution a.accountGroup a.taxTreatment owners a.activityPeriod a.ledgerAccountId
    }

// the route path: the update goes through the route's converter as an input contract, then to the orchestrator. The
// ledger link is given as an ID, not a code, because the codes' route-lifetime cache would outlive this test's rollback.
let private updateInvestmentAccount context (u: AccountUpdate) =
    result {
        let input: InvestmentAccountUpdateInput =
            { accountName = u.currentName |> InvestmentAccountName.value
              accountNameUpdate = u.nameUpdate |> map InvestmentAccountName.value
              institutionUpdate = u.institutionUpdate |> map Institution.value
              accountGroupUpdate = u.accountGroupUpdate |> map AccountGroup.value
              taxTreatmentUpdate = u.taxTreatmentUpdate |> map TaxTreatment.toString
              ownersUpdate = u.ownersUpdate |> map (List.map PersonComponent.PersonName.value)
              activeBeginUpdate = u.activeBeginUpdate
              activeEndUpdate = u.activeEndUpdate
              ledgerAccountCodeUpdate = NoChange }
        let! fieldUpdates = input |> ``convert [InvestmentAccountUpdateInput] to [InvestmentAccountFieldUpdates]`` context
        return!
            InvestmentAccountOrchestration.updateInvestmentAccount
                context
                { fieldUpdates with ledgerAccountIdUpdate = u.ledgerAccountIdUpdate }
    }

let private ownersNotAllowed = function AsError (PositionsInvestmentAccountOwnersNotAllowed (a, t, n)) -> Some(a, t, n) | _ -> None
let private breaksHoldings = function AsError (PositionsTaxTreatmentChangeBreaksHoldings (a, t, s)) -> Some(a, t, s) | _ -> None

[<Collection("SharedTestData")>]
type InvestmentAccountMaintenanceTests(fixture: TestDataFixture) =

    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let begin' = fixture.Data.positions.accountsActiveBegin

    [<Fact>]
    member _.``REQ-POS-11.3 creating an Investment Account stores its name, institution, account group, tax treatment, owners, active begin, active end and ledger link, and the list returns each of them with the linked account's code and name`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let activeEnd = begin'.PlusYears(3)
                let! _ =
                    createInvestmentAccount
                        context
                        { newAccount "Sam Brokerage" TaxTreatment.Taxable [ PF.sam ] (Some(accountIdOf "F-1250")) begin' (Some activeEnd) with
                            institution = toInstitution "Example Discount Brokerage"
                            accountGroup = toAccountGroup "Taxable Savings" }
                let! found = listed context "Sam Brokerage"
                Assert.Equal<AccountSummary list>(
                    [ "Sam Brokerage", "Example Discount Brokerage", "Taxable Savings", TaxTreatment.Taxable, [ PF.sam ],
                      Some("F-1250", "Roth IRA"), begin', Some activeEnd ],
                    found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.1 creating an Investment Account whose name exactly matches an existing one's is rejected with a typed error naming the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            createInvestmentAccount context (newAccount PF.samHsa TaxTreatment.Hsa [ PF.sam ] None begin' None)
            |> expectError
                (function AsError (PositionsInvestmentAccountNameAlreadyExists n) -> Some n | _ -> None)
                (fun n -> Assert.Equal(PF.samHsa, n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.1 an Investment Account whose name differs from an existing one's only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let! _ = createInvestmentAccount context (newAccount "SAM HSA" TaxTreatment.Hsa [ PF.sam ] None begin' None)
                let! upper = listed context "SAM HSA"
                let! original = listed context PF.samHsa
                Assert.Equal(1, upper.Length)
                Assert.Equal(1, original.Length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.5 REQ-PER-2.4 creating an Investment Account with an owner name that matches no Person fails with a typed error naming that name, and no account is stored`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                createInvestmentAccount context (newAccount "Ghost Brokerage" TaxTreatment.Taxable [ PF.alex; "Ghost Example" ] None begin' None)
                |> expectError (function AsError (PersonNameDoesntMatchId n) -> Some n | _ -> None) (fun n -> Assert.Equal("Ghost Example", n))
                let! found = listed context "Ghost Brokerage"
                Assert.Empty(found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.5 an Investment Account given the same Person twice among its owners is rejected with a typed error naming the Person`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            createInvestmentAccount context (newAccount "Twice Brokerage" TaxTreatment.Taxable [ PF.alex; PF.sam; PF.alex ] None begin' None)
            |> expectError
                (function AsError (PositionsInvestmentAccountOwnerRepeated (a, p)) -> Some(a, p) | _ -> None)
                (fun found -> Assert.Equal(("Twice Brokerage", PF.alex), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.5 an Investment Account given no owners is rejected with a typed error naming the account`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            createInvestmentAccount context (newAccount "Ownerless Brokerage" TaxTreatment.Taxable [] None begin' None)
            |> expectError
                (function AsError (PositionsInvestmentAccountHasNoOwners a) -> Some a | _ -> None)
                (fun a -> Assert.Equal("Ownerless Brokerage", a))
            |> Ok)
        |> railroadWrapper

    [<Theory>]
    [<InlineData("TaxDeferred")>]
    [<InlineData("Roth")>]
    [<InlineData("Hsa")>]
    member _.``REQ-POS-4.6 for each of TaxDeferred, Roth and Hsa, an Investment Account with two owners is rejected with a typed error naming the account and its tax treatment`` (treatment: string) =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            let taxTreatment = TaxTreatment.fromString treatment |> mustBe
            createInvestmentAccount context (newAccount "Shared Retirement" taxTreatment [ PF.alex; PF.sam ] None begin' None)
            |> expectError ownersNotAllowed (fun (a, t, n) ->
                Assert.Equal("Shared Retirement", a)
                Assert.Equal(treatment, t)
                Assert.Equal(2, n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.6 a Taxable Investment Account with two owners is created, and the list shows both owners`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let! _ = createInvestmentAccount context (newAccount "Sam and Jordan" TaxTreatment.Taxable [ PF.sam; PF.jordan ] None begin' None)
                let! owners = ownersOf context "Sam and Jordan"
                Assert.Equal<string list>([ PF.jordan; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("F-1270", "Asset", "Cash")>]
    [<InlineData("F-2220", "Liability", "CurrentLiability")>]
    member _.``REQ-POS-4.8 for each of an Asset account of a subtype other than Investment and an account of a type other than Asset, linking it to an Investment Account fails with a typed error naming the code and what is wrong`` (code: string, accountType: string, subtype: string) =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            createInvestmentAccount context (newAccount "Wrongly Linked" TaxTreatment.Taxable [ PF.alex ] (Some(accountIdOf code)) begin' None)
            |> expectError
                (function AsError (PositionsInvestmentLedgerAccountNotAssetInvestment (c, t, s)) -> Some(c, t, s) | _ -> None)
                (fun found -> Assert.Equal((code, accountType, Some subtype), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-4.9 linking an Investment Account to a ledger account already linked to another Investment Account is rejected with a typed error naming the code and that other account`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            updateInvestmentAccount
                context { noChange PF.jordanCustodial with ledgerAccountIdUpdate = SetTo(Some fixture.Data.positions.brokerageAtCost1260Id) }
            |> expectError
                (function AsError (PositionsInvestmentLedgerAccountAlreadyLinked (c, n)) -> Some(c, n) | _ -> None)
                (fun found -> Assert.Equal(("F-1260", PF.alexBrokerage), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.3 updating an Investment Account addressed by its current name changes its name, institution, account group, active begin, active end and ledger link, and the list shows every new value`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let newBegin = begin'.PlusDays(10)
                let newEnd = begin'.PlusYears(5)
                let! _ =
                    updateInvestmentAccount
                        context
                        { noChange PF.jordanCustodial with
                            nameUpdate = SetTo(toAccountName "Jordan College Fund")
                            institutionUpdate = SetTo(toInstitution "Example College Bank")
                            accountGroupUpdate = SetTo(toAccountGroup "Education")
                            activeBeginUpdate = SetTo newBegin
                            activeEndUpdate = SetTo(Some newEnd)
                            ledgerAccountIdUpdate = SetTo(Some(accountIdOf "F-1250")) }
                let! found = listed context "Jordan College Fund"
                Assert.Equal<AccountSummary list>(
                    [ "Jordan College Fund", "Example College Bank", "Education", TaxTreatment.Taxable, [ PF.jordan ],
                      Some("F-1250", "Roth IRA"), newBegin, Some newEnd ],
                    found)
                let! old = listed context PF.jordanCustodial
                Assert.Empty(old)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.3 an update that clears an Investment Account's active end and ledger link leaves it with neither`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! _ =
                    createInvestmentAccount
                        context (newAccount "Closing Brokerage" TaxTreatment.Taxable [ PF.alex ] (Some(accountIdOf "F-1250")) begin' (Some(begin'.PlusYears(2))))
                let! _ =
                    updateInvestmentAccount
                        context { noChange "Closing Brokerage" with activeEndUpdate = SetTo None; ledgerAccountIdUpdate = SetTo None }
                let! found = listed context "Closing Brokerage"
                let _, _, _, _, _, ledger, _, activeEnd = found |> List.exactlyOne
                Assert.Equal(None, ledger)
                Assert.Equal(None, activeEnd)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-POS-5.4 an update giving owners replaces the whole set: an account owned by two Persons and updated to one of them and a third is owned by exactly those two`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! _ =
                    updateInvestmentAccount
                        context { noChange PF.jointBrokerage with ownersUpdate = SetTo [ toPersonName PF.sam; toPersonName PF.jordan ] }
                let! owners = ownersOf context PF.jointBrokerage
                Assert.Equal<string list>([ PF.jordan; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update adding a second owner to a Roth account is rejected with a typed error naming the account and its tax treatment, and the owners are unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                updateInvestmentAccount
                    context { noChange PF.alexRoth with ownersUpdate = SetTo [ toPersonName PF.alex; toPersonName PF.sam ] }
                |> expectError ownersNotAllowed (fun (a, t, _) -> Assert.Equal((PF.alexRoth, "Roth"), (a, t)))
                let! owners = ownersOf context PF.alexRoth
                Assert.Equal<string list>([ PF.alex ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update changing a jointly owned Taxable account to Roth without changing its owners is rejected with a typed error naming the account and Roth`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! _ = createInvestmentAccount context (newAccount "Joint Empty" TaxTreatment.Taxable [ PF.alex; PF.sam ] None begin' None)
                updateInvestmentAccount context { noChange "Joint Empty" with taxTreatmentUpdate = SetTo TaxTreatment.Roth }
                |> expectError ownersNotAllowed (fun (a, t, n) -> Assert.Equal(("Joint Empty", "Roth", 2), (a, t, n)))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update changing a jointly owned Taxable account to Roth and reducing its owners to one in the same operation succeeds`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! _ = createInvestmentAccount context (newAccount "Joint Empty" TaxTreatment.Taxable [ PF.alex; PF.sam ] None begin' None)
                let! _ =
                    updateInvestmentAccount
                        context
                        { noChange "Joint Empty" with
                            taxTreatmentUpdate = SetTo TaxTreatment.Roth
                            ownersUpdate = SetTo [ toPersonName PF.sam ] }
                let! found = listed context "Joint Empty"
                let _, _, _, treatment, owners, _, _, _ = found |> List.exactlyOne
                Assert.Equal(TaxTreatment.Roth, treatment)
                Assert.Equal<string list>([ PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update changing a single-owner Roth account to Taxable and adding a second owner in the same operation succeeds`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! _ = createInvestmentAccount context (newAccount "Solo Roth" TaxTreatment.Roth [ PF.alex ] None begin' None)
                let! _ =
                    updateInvestmentAccount
                        context
                        { noChange "Solo Roth" with
                            taxTreatmentUpdate = SetTo TaxTreatment.Taxable
                            ownersUpdate = SetTo [ toPersonName PF.alex; toPersonName PF.sam ] }
                let! found = listed context "Solo Roth"
                let _, _, _, treatment, owners, _, _, _ = found |> List.exactlyOne
                Assert.Equal(TaxTreatment.Taxable, treatment)
                Assert.Equal<string list>([ PF.alex; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.5 an update giving an empty owner set is rejected with a typed error naming the account, and the owners are unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                updateInvestmentAccount context { noChange PF.jointBrokerage with ownersUpdate = SetTo [] }
                |> expectError
                    (function AsError (PositionsInvestmentAccountHasNoOwners a) -> Some a | _ -> None)
                    (fun a -> Assert.Equal(PF.jointBrokerage, a))
                let! owners = ownersOf context PF.jointBrokerage
                Assert.Equal<string list>([ PF.alex; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.3 changing the tax treatment of a Taxable account holding two Securities with basis methods to TaxDeferred is rejected with a typed error naming both Securities`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            updateInvestmentAccount context { noChange PF.alexBrokerage with taxTreatmentUpdate = SetTo TaxTreatment.TaxDeferred }
            |> expectError breaksHoldings (fun (a, t, securities) ->
                Assert.Equal((PF.alexBrokerage, "TaxDeferred"), (a, t))
                Assert.Equal<string list>([ PF.international; PF.totalMarket ], securities))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.3 changing the tax treatment of a Roth account holding two Securities to Taxable is rejected with a typed error naming both Securities`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            updateInvestmentAccount context { noChange PF.alexRoth with taxTreatmentUpdate = SetTo TaxTreatment.Taxable }
            |> expectError breaksHoldings (fun (a, t, securities) ->
                Assert.Equal((PF.alexRoth, "Taxable"), (a, t))
                Assert.Equal<string list>([ PF.bondFund; PF.totalMarket ], securities))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.3 changing the tax treatment of a TaxDeferred account with Holdings to Roth succeeds, since its Holdings carry no basis method under either`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! updated = updateInvestmentAccount context { noChange PF.sam401k with taxTreatmentUpdate = SetTo TaxTreatment.Roth }
                Assert.Equal(TaxTreatment.Roth, updated |> InvestmentAccount.taxTreatment)
                let! sam401kId = PositionsLookups.investmentAccountIdOf context PF.sam401k
                let! holdings = listHoldings context (Some sam401kId)
                Assert.Equal<(string * BasisMethod option) list>(
                    [ PF.bondFund, None; PF.totalMarket, None ],
                    holdings |> List.map (fun v -> v.securityName, v.holding |> Holding.basisMethod))
                let! found = listed context PF.sam401k
                let _, _, _, treatment, _, _, _, _ = found |> List.exactlyOne
                Assert.Equal(TaxTreatment.Roth, treatment)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period so that snapshots fall before the new begin and after the new end is rejected with a typed error naming the earliest and latest offending snapshot dates`` () =
        let p = fixture.Data.positions
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            updateInvestmentAccount
                context
                { noChange PF.alexBrokerage with
                    activeBeginUpdate = SetTo(p.d1.PlusDays(1))
                    activeEndUpdate = SetTo(Some(p.d4.PlusDays(-1))) }
            |> expectError
                (function AsError (PositionsActivePeriodExcludesSnapshots (a, earliest, latest)) -> Some(a, earliest, latest) | _ -> None)
                (fun found -> Assert.Equal((PF.alexBrokerage, p.d1, p.d4), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period to exactly its first and last snapshot dates succeeds`` () =
        let p = fixture.Data.positions
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! updated =
                    updateInvestmentAccount
                        context { noChange PF.alexBrokerage with activeBeginUpdate = SetTo p.d1; activeEndUpdate = SetTo(Some p.d4) }
                let period = updated |> InvestmentAccount.activityPeriod
                Assert.Equal((p.d1, Some p.d4), (period |> ActivityPeriod.activeBegin, period |> ActivityPeriod.activeEnd))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 updating an Investment Account by a name that matches no account fails with a typed not-found error naming Investment Account and the name`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            updateInvestmentAccount context { noChange "Missing Brokerage" with institutionUpdate = SetTo(toInstitution "Elsewhere") }
            |> expectError
                (function AsError (PositionsInvestmentAccountNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Missing Brokerage", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.3 listing Investment Accounts returns every fixture account ordered by name, each with its institution, account group, tax treatment, owners' names, linked account's code and name, active begin and active end, and no other account`` () =
        let p = fixture.Data.positions
        listInvestmentAccounts (Context.create NoTransaction FetchOnly)
        |> Result.map (fun all ->
            Assert.Equal<AccountSummary list>(
                [ PF.alexBrokerage, "Example Brokerage", "Brokerage", TaxTreatment.Taxable, [ PF.alex ],
                  Some("F-1260", "Fixture Brokerage at Cost"), begin', None
                  PF.alexRoth, "Example Brokerage", "Retirement", TaxTreatment.Roth, [ PF.alex ], None, begin', None
                  PF.jointBrokerage, "Example Brokerage", "Brokerage", TaxTreatment.Taxable, [ PF.alex; PF.sam ], None, begin', None
                  PF.jordanCustodial, "Example Brokerage", "Custodial", TaxTreatment.Taxable, [ PF.jordan ], None, begin', None
                  PF.oldBrokerage, "Example Brokerage", "Brokerage", TaxTreatment.Taxable, [ PF.sam ], None, begin', Some p.monthEnd4
                  PF.sam401k, "Example Retirement Services", "Retirement", TaxTreatment.TaxDeferred, [ PF.sam ], None, begin', None
                  PF.samHsa, "Example Health Bank", "Health", TaxTreatment.Hsa, [ PF.sam ], None, begin', None ],
                all |> List.map summary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-PER-2.4 an Investment Account update giving an owner name that matches no Person fails with a typed error naming that name, and the owners are unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                updateInvestmentAccount
                    context { noChange PF.jointBrokerage with ownersUpdate = SetTo [ toPersonName PF.alex; toPersonName "Ghost Example" ] }
                |> expectError (function AsError (PersonNameDoesntMatchId n) -> Some n | _ -> None) (fun n -> Assert.Equal("Ghost Example", n))
                let! owners = ownersOf context PF.jointBrokerage
                Assert.Equal<string list>([ PF.alex; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.5 changing a Roth account's tax treatment to TaxDeferred while three of its snapshots, recorded out of date order, carry a contribution basis is rejected with a typed error naming the account and the earliest and latest of those snapshot dates, and the account stays Roth`` () =
        let p = fixture.Data.positions
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! account = createInvestmentAccount context (newAccount "Basis Roth" TaxTreatment.Roth [ PF.sam ] None begin' None)
                let accountId = account |> InvestmentAccount.investmentAccountId
                let withBasis date amount : AccountSnapshotOrchestration.Snapshot =
                    accountId, date, Reported, Some(toContributionBasis amount), []
                // recorded d3, d1, d2: the first and last recorded are not the earliest and latest dated
                let! _ =
                    AccountSnapshotOrchestration.recordSnapshots
                        context [ withBasis p.d3 300.00M; withBasis p.d1 100.00M; withBasis p.d2 200.00M ]
                updateInvestmentAccount context { noChange "Basis Roth" with taxTreatmentUpdate = SetTo TaxTreatment.TaxDeferred }
                |> expectError
                    (function AsError (PositionsTaxTreatmentChangeStrandsContributionBasis (a, earliest, latest)) -> Some(a, earliest, latest) | _ -> None)
                    (fun found -> Assert.Equal(("Basis Roth", p.d1, p.d3), found))
                let! found = listed context "Basis Roth"
                let _, _, _, treatment, _, _, _, _ = found |> List.exactlyOne
                Assert.Equal(TaxTreatment.Roth, treatment)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.5 a Roth account with snapshots, none of which carries a contribution basis, changes to TaxDeferred and reads back TaxDeferred`` () =
        let p = fixture.Data.positions
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! account = createInvestmentAccount context (newAccount "Basisless Roth" TaxTreatment.Roth [ PF.sam ] None begin' None)
                let accountId = account |> InvestmentAccount.investmentAccountId
                let noBasis date : AccountSnapshotOrchestration.Snapshot = accountId, date, Reported, None, []
                let! recorded = AccountSnapshotOrchestration.recordSnapshots context [ noBasis p.d1; noBasis p.d2 ]
                Assert.Equal<LocalDate list>(
                    [ p.d1; p.d2 ],
                    recorded |> List.map (fun r -> r.snapshot.header |> AccountSnapshotHeader.snapshotDate))
                let! _ = updateInvestmentAccount context { noChange "Basisless Roth" with taxTreatmentUpdate = SetTo TaxTreatment.TaxDeferred }
                let! found = listed context "Basisless Roth"
                let _, _, _, treatment, _, _, _, _ = found |> List.exactlyOne
                Assert.Equal(TaxTreatment.TaxDeferred, treatment)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.6 for each of TaxDeferred, Roth and Hsa, changing the tax treatment of a Taxable account whose snapshots carry lots on two dates is rejected with a typed error naming the account and the earliest and latest snapshot dates carrying lots, and the account stays Taxable`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period so that activities fall before the new begin and after the new end is rejected with a typed error naming the earliest and latest offending activity dates, and the active period is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period so that both snapshots and activities fall outside it is rejected with one typed error naming the earliest and latest offending snapshot dates and the earliest and latest offending activity dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period to exactly its earliest and latest activity dates, which are outside its first and last snapshot dates, succeeds`` () =
        Assert.Fail "Not yet implemented"
