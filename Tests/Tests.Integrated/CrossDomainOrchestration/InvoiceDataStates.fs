module Tests.Integrated.CrossDomainOrchestration.InvoiceDataStates

open System
open System.Globalization
open System.Text.Json.Nodes
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Every test builds its own agreements, monthly on the 1st, with 100.00 legs: Outgo on F-2230 and F-1280, Income on
   F-1280 and F-4290. Each has an Instance dated 1 March 2049, holding a 100.00 Invoice for each leg the test asks for.
   The routes commit, so the setup commits too; tests read back from a fresh context and delete every agreement they
   built, with everything under it, in a finally. The two model-level tests run in a transaction that rolls back.
   "No Invoice is stored" means the leg has no Invoice anywhere and the Instance's Invoices are as before the call.
   "Unchanged" means the stored Invoice reads back equal to what it was before the call. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private march (day: int) = LocalDate(2049, 3, day)

let private april1 = LocalDate(2049, 4, 1)

type private Made =
    { agreementId: MasterAgreementId
      agreementName: string
      legIds: PaymentAgreementId list
      legNames: string list
      instanceId: InstanceId
      /// The Invoices on the Instance, by leg index.
      invoiceIds: Map<int, InvoiceId> }

/// Builds an agreement with the legs and its 1 March Instance, with an Invoice for each leg index given.
let private build
    (fixture: TestDataFixture) (context: Context.Context) (direction: FlowDirection) (legCount: int) (invoiced: int list) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    result {
        let name = $"Invoice data state test {Guid.NewGuid():N}"
        let! agreementName = name |> AgreementName.create
        let! first = 1 |> Cadence.DateInMonthNumber.fromInt
        let! counterparty = "Invoice data state test counterparty" |> Counterparty.create
        let! activityPeriod =
            ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
        let debit, credit =
            match direction with
            | Outgo -> accountIdOf "F-2230", accountIdOf "F-1280"
            | Income -> accountIdOf "F-1280", accountIdOf "F-4290"
        let legNames = List.init legCount (fun i -> $"{name} leg {i + 1}")
        let! legs =
            legNames
            |> List.map (fun legName ->
                result {
                    let! paName = legName |> PaymentAgreementName.create
                    let! expected = Money.fromDecimal 100.00M
                    let! due = 0 |> DaysDueAfterInvoiceDate.create
                    return (paName, (DebitAccount.create debit), (CreditAccount.create credit), Some expected, Some due, None)
                })
            |> convertListOfResultsToResultsList
        let! agreement =
            AgreementOrchestration.constructNewAndPersist
                context agreementName direction (Cadence.Monthly(Cadence.DateInMonth first)) { nextInstance = march 1 }
                counterparty activityPeriod None legs
        let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
        let legIds =
            legNames
            |> List.map (fun legName ->
                agreement
                |> AgreementOrchestration.paymentAgreements
                |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                |> PaymentAgreement.paymentAgreementId)
        let state = match direction with | Outgo -> InvoiceReceived | Income -> InvoiceSent
        let! amount = Money.fromDecimal 100.00M
        let invoices =
            invoiced
            |> List.map (fun i ->
                (legIds[i], None, (InvoiceDate.create (march 1)), (DueDate.create (march 31)),
                 (InvoiceAmount.create amount), state, None, None, []))
        let! created = InstanceOrchestration.constructNewAndPersist context agreementId (march 1) invoices
        let invoiceIds =
            invoiced
            |> List.map (fun i ->
                i,
                created
                |> InstanceOrchestration.invoiceComposites
                |> List.map InstanceOrchestration.invoice
                |> List.find (fun inv -> inv |> Invoice.paymentAgreementId = legIds[i])
                |> Invoice.invoiceId)
            |> Map.ofList
        return
            { agreementId = agreementId
              agreementName = name
              legIds = legIds
              legNames = legNames
              instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
              invoiceIds = invoiceIds }
    }

/// Every Invoice stored for the leg, on any Instance.
let private invoicesOfLeg (context: Context.Context) (legId: PaymentAgreementId) =
    Invoice.query context None Invoice.invoiceSelectFields None (Some "inv.payment_agreement_id = @leg") None None None
        [ { name = "@leg"; value = UniqueId(legId |> PaymentAgreementId.value) } ] AnyQuantityIsAcceptable

let private invoicesOfInstance (context: Context.Context) (instanceId: InstanceId) =
    instanceId
    |> InstanceOrchestration.fetchCompositeByInstanceId context
    |> Result.map (InstanceOrchestration.invoiceComposites >> List.map InstanceOrchestration.invoice)

let private storedInvoice (invoiceId: InvoiceId) = invoiceId |> Invoice.fetchById (fresh ())

let private send (verb: string) (json: string) = routeUiCommandForTesting "CashFlow" verb [] json

let private invoiceFor (legName: string) : Contracts.NewInvoiceFieldsInput =
    { paymentAgreementName = legName
      externalInvoiceId = None
      invoiceDate = march 1
      dueDate = march 31
      amount = 100.00M
      invoiceState = "InvoiceReceived"
      blocker = None
      memo = None
      payments = [] }

let private createInvoicePayload (instanceId: InstanceId) (invoice: Contracts.NewInvoiceFieldsInput) =
    ({ instanceId = instanceId |> InstanceId.value; invoice = invoice } : Contracts.CreateInvoiceInput) |> Json.toJson

/// Sends CreateInvoice; returns the one Invoice the payload named, as stored.
let private createInvoice (instanceId: InstanceId) (invoice: Contracts.NewInvoiceFieldsInput) =
    result {
        let! json = createInvoicePayload instanceId invoice
        let! returned = send "CreateInvoice" json
        let! composite = Json.fromJson<Contracts.InstanceCompositeReturn> returned
        let! invoiceId =
            composite.invoiceComposites
            |> List.map _.invoice
            |> List.filter (fun i -> i.paymentAgreementName = invoice.paymentAgreementName)
            |> List.exactlyOne
            |> fun i -> Ok (i.invoiceId |> InvoiceId.fromGuid)
        return! storedInvoice invoiceId
    }

/// Sends CreateInvoice after the edit is made to the payload's invoice.
let private createInvoiceEdited (instanceId: InstanceId) (invoice: Contracts.NewInvoiceFieldsInput) (edit: JsonNode -> unit) =
    result {
        let! json = createInvoicePayload instanceId invoice
        let node = JsonNode.Parse(json)
        edit node["invoice"]
        return! send "CreateInvoice" (node.ToJsonString())
    }

let private noInvoiceChange (invoiceId: InvoiceId) : Contracts.UpdateInvoiceInput =
    { invoiceId = invoiceId |> InvoiceId.value
      externalInvoiceIdUpdate = NoChange
      invoiceDateUpdate = NoChange
      dueDateUpdate = NoChange
      amountUpdate = NoChange
      invoiceStateUpdate = NoChange
      blockerUpdate = NoChange
      memoUpdate = NoChange }

let private updateInvoice (input: Contracts.UpdateInvoiceInput) = input |> Json.toJson |> Result.bind (send "UpdateInvoice")

/// Sends UpdateInvoice with the field set to the raw JSON value.
let private updateInvoiceSetting (invoiceId: InvoiceId) (field: string) (rawValue: string) =
    result {
        let! json = noInvoiceChange invoiceId |> Json.toJson
        let node = JsonNode.Parse(json)
        node[field] <- JsonNode.Parse($"""{{"Case":"SetTo","Fields":[{rawValue}]}}""")
        return! send "UpdateInvoice" (node.ToJsonString())
    }

let private createInstancePayload (made: Made) (invoices: Contracts.NewInvoiceFieldsInput list) =
    ({ masterAgreementName = made.agreementName; instanceDate = april1; invoices = invoices } : Contracts.CreateInstanceInput)
    |> Json.toJson
    |> Result.bind (send "CreateInstance")

let private instanceDatesOf (made: Made) =
    [ made.agreementId ] |> Instance.fetchByMasterAgreementIdList (fresh ()) |> Result.map (List.map Instance.instanceDate >> List.sort)

let private jsonString (text: string) = if isNull text then "null" else JsonValue.Create(text).ToJsonString()

let private blockerJson (case: string) (fields: string list option) =
    match fields with
    | None -> JsonNode.Parse($"""{{"Case":{jsonString case}}}""")
    | Some fs -> JsonNode.Parse($"""{{"Case":{jsonString case},"Fields":[{fs |> String.concat ","}]}}""")

let private noteOf (length: int) = String('n', length)

let private blockerNote (text: string) =
    match text |> BlockerNote.create with
    | Ok note -> note
    | Error e -> failwith (e.ToMessage())

/// Fails unless the attempt was refused with an error `isExpected` accepts, naming whatever came back instead.
let private expectRefusal (isExpected: IAppError -> bool) (attempt: Result<'a, IAppError>) =
    match attempt with
    | Error e when isExpected e -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

/// A payload the contract cannot carry (a null where the contract has no option, a blocker case it does not have, a
/// note on a blocker that takes none) is refused loudly while it is deserialized into the contract; no domain case
/// exists for it.
let private refusedAsJsonFor<'contract> (e: IAppError) =
    match e with
    | AsError (UtilityError.JsonDeserializationFailed(typeName, _, _)) -> typeName = typeof<'contract>.ToString()
    | _ -> false

let private refusedAsCreateInvoiceJson = refusedAsJsonFor<Contracts.CreateInvoiceInput>

let private refusedAsUpdateInvoiceJson = refusedAsJsonFor<Contracts.UpdateInvoiceInput>

let private nonPositiveAmountOf (amount: string) (e: IAppError) =
    match e with
    | AsError (CashFlowError.CashflowInvoiceNonPositiveAmount(_, refused)) ->
        refused = Decimal.Parse(amount, CultureInfo.InvariantCulture)
    | _ -> false

let private invalidInvoiceStateOf (state: string) (e: IAppError) =
    match e with
    | AsError (CashFlowError.CashflowInvalidInvoiceState refused) -> refused = state
    | _ -> false

let private emptyInvoiceMemo (e: IAppError) =
    match e with
    | AsError (CashFlowError.CashflowInvoiceMemoIsEmpty _) -> true
    | _ -> false

[<Collection("SharedTestData")>]
type InvoiceDataStatesTests(fixture: TestDataFixture) =

    (* Runs the test with a builder that commits what it builds, then deletes every agreement built. *)
    let withBuilt (test: (FlowDirection -> int -> int list -> Result<Made, IAppError>) -> Result<unit, IAppError>) =
        let built = ResizeArray<MasterAgreementId>()
        let make direction legCount invoiced =
            runCommandRouteAndAutoCompleteTransaction CashFlowCreateInstance (fun context ->
                build fixture context direction legCount invoiced)
            |> Result.map (fun made -> built.Add made.agreementId; made)
        let cleanUpFailures = ResizeArray<string>()
        try
            test make |> railroadWrapper
        finally
            for id in built do
                match Cleanup.cleanUpMasterAgreementTree (Some(id |> MasterAgreementId.value)) with
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage())
        Assert.Empty(cleanUpFailures)

    let rolledBack (body: Context.Context -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback CashFlowCreateInvoice body |> railroadWrapper


    [<Fact>]
    member _.``REQ-CF-5.2 two Invoices created through CreateInvoice carry distinct, non-empty Invoice IDs`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 2 []
                let! first = createInvoice made.instanceId (invoiceFor made.legNames[0])
                let! second = createInvoice made.instanceId (invoiceFor made.legNames[1])
                let ids = [ first; second ] |> List.map (Invoice.invoiceId >> InvoiceId.value)
                Assert.Equal(2, ids |> List.distinct |> List.length)
                Assert.DoesNotContain(Guid.Empty, ids)
            })

    [<Fact>]
    member _.``REQ-CF-5.3 a CreateInvoice payload whose Instance ID names no stored Instance is rejected with a typed error and no Invoice is stored anywhere for that Payment Agreement`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let missing = InstanceId.create ()
                let! json = createInvoicePayload missing (invoiceFor made.legNames[0])
                let attempt = send "CreateInvoice" json
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowInstanceIdDoesntExist uuid) -> uuid = (missing |> InstanceId.value)
                    | _ -> false)
                Assert.Empty(leg)
            })

    [<Fact>]
    member _.``REQ-CF-5.3 adding an Invoice at the model level whose Instance ID names no stored Instance is rejected with a typed error and no Invoice is stored`` () =
        rolledBack (fun context ->
            result {
                let! made = build fixture context Outgo 1 []
                let! amount = Money.fromDecimal 100.00M
                let invoice =
                    (made.legIds[0], None, (InvoiceDate.create (march 1)), (DueDate.create (march 31)),
                     (InvoiceAmount.create amount), InvoiceReceived, None, None, [])
                let missing = InstanceId.create ()
                let update : InstanceOrchestration.InstanceCompositeUpdate =
                    { instanceUpdates =
                        { instanceIdToUpdate = missing; isFulfilledUpdate = NoChange }
                      invoiceCompositeUpdates = []
                      newInvoices = [ invoice ] }
                let attempt = update |> InstanceOrchestration.updateInstanceComposite context
                let! leg = invoicesOfLeg context made.legIds[0]
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowInstanceIdDoesntExist uuid) -> uuid = (missing |> InstanceId.value)
                    | _ -> false)
                Assert.Empty(leg)
            })

    [<Fact>]
    member _.``REQ-CF-5.3 REQ-CF-5.4 an Invoice created through CreateInvoice is stored referencing the Instance and the Payment Agreement it named`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 2 []
                let! stored = createInvoice made.instanceId (invoiceFor made.legNames[1])
                Assert.Equal(made.instanceId, stored |> Invoice.instanceId)
                Assert.Equal(made.legIds[1], stored |> Invoice.paymentAgreementId)
            })

    [<Fact>]
    member _.``REQ-CF-5.4 a CreateInvoice payload naming a Payment Agreement that does not exist is rejected with a typed error and no Invoice is stored`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n ->
                    n["paymentAgreementName"] <- JsonValue.Create($"No such leg {Guid.NewGuid():N}"))
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowPaymentAgreementNameDoesntMatchId name) -> name.StartsWith "No such leg "
                    | _ -> false)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.4 adding an Invoice at the model level whose Payment Agreement ID names no stored Payment Agreement is rejected with a typed error and no Invoice is stored`` () =
        rolledBack (fun context ->
            result {
                let! made = build fixture context Outgo 1 []
                let! amount = Money.fromDecimal 100.00M
                let invoice =
                    (PaymentAgreementId.create (), None, (InvoiceDate.create (march 1)), (DueDate.create (march 31)),
                     (InvoiceAmount.create amount), InvoiceReceived, None, None, [])
                let update : InstanceOrchestration.InstanceCompositeUpdate =
                    { instanceUpdates =
                        { instanceIdToUpdate = made.instanceId; isFulfilledUpdate = NoChange }
                      invoiceCompositeUpdates = []
                      newInvoices = [ invoice ] }
                let attempt = update |> InstanceOrchestration.updateInstanceComposite context
                let! onInstance = invoicesOfInstance context made.instanceId
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowPaymentAgreementIdDoesntExist _) -> true
                    | _ -> false)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.5 a CreateInvoice payload naming a Payment Agreement that belongs to a different Master Agreement than the Instance's is rejected with a typed error and no Invoice is stored`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let! other = make Outgo 1 []
                let attempt =
                    createInvoicePayload made.instanceId (invoiceFor other.legNames[0]) |> Result.bind (send "CreateInvoice")
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                let! foreignLeg = invoicesOfLeg (fresh ()) other.legIds[0]
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowInvoiceDiamondMismatch _) -> true
                    | _ -> false)
                Assert.Empty(onInstance)
                Assert.Empty(foreignLeg)
            })

    [<Theory>]
    [<InlineData("Missing")>]
    [<InlineData("Foreign")>]
    member _.``REQ-CF-5.4 REQ-CF-5.5 for each of a Payment Agreement that does not exist and one belonging to a different Master Agreement, a CreateInstance payload carrying an Invoice naming it is rejected with a typed error and no Instance is stored`` (paymentAgreement:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let! other = make Outgo 1 []
                let legName =
                    match paymentAgreement with
                    | "Missing" -> $"No such leg {Guid.NewGuid():N}"
                    | _ -> other.legNames[0]
                let attempt = createInstancePayload made [ invoiceFor legName ]
                let! dates = instanceDatesOf made
                attempt
                |> expectRefusal (fun e ->
                    match paymentAgreement, e with
                    | "Missing", AsError (CashFlowError.CashflowPaymentAgreementNameDoesntMatchId name) -> name = legName
                    | "Foreign", AsError (CashFlowError.CashflowInvoiceDiamondMismatch _) -> true
                    | _ -> false)
                Assert.Equal<LocalDate list>([ march 1 ], dates)
            })

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("0.00")>]
    [<InlineData("-0.01")>]
    member _.``REQ-CF-5.6 for each of null, zero and minus one cent, a CreateInvoice payload with that amount is refused, the null as the payload is read and the others with a typed error, and no Invoice is stored`` (amount:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["amount"] <- (if isNull amount then null else JsonValue.Create(Decimal.Parse(amount, CultureInfo.InvariantCulture)) :> JsonNode))
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt
                |> expectRefusal (if isNull amount then refusedAsCreateInvoiceJson else nonPositiveAmountOf amount)
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.6 a CreateInvoice payload with an amount of three decimal places is rejected with a typed error and no Invoice is stored`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["amount"] <- JsonValue.Create(100.005M))
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt
                |> expectRefusal (function
                    | AsError (BizFinServError.MoneyFailedToConvertImproperPrecision raw) -> raw = 100.005M
                    | _ -> false)
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.6 a CreateInvoice payload with an amount of one cent is stored with that amount`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let! stored = createInvoice made.instanceId { invoiceFor made.legNames[0] with amount = 0.01M }
                Assert.Equal(0.01M, ((stored |> Invoice.amount) |> CashFlowComponent.InvoiceAmount.value) |> Money.amount)
            })

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("0.00")>]
    [<InlineData("-0.01")>]
    member _.``REQ-CF-5.6 for each of null, zero and minus one cent, an UpdateInvoice payload setting the amount to that value is refused, the null as the payload is read and the others with a typed error, and the Invoice is unchanged`` (amount:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! before = storedInvoice invoiceId
                let attempt = updateInvoiceSetting invoiceId "amountUpdate" (if isNull amount then "null" else amount)
                let! after = storedInvoice invoiceId
                attempt
                |> expectRefusal (if isNull amount then refusedAsUpdateInvoiceJson else nonPositiveAmountOf amount)
                Assert.Equal<Invoice.Invoice>(before, after)
            })

    [<Theory>]
    [<InlineData("invoiceDate")>]
    [<InlineData("dueDate")>]
    member _.``REQ-CF-5.7 REQ-CF-5.8 for each of the invoice date and the due date, a CreateInvoice payload with that date null is refused as it is read and no Invoice is stored`` (field:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n[field] <- null)
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt |> expectRefusal refusedAsCreateInvoiceJson
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.7 REQ-CF-5.8 a CreateInvoice payload with different invoice and due dates is stored with each date in its own field exactly as given`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let! stored =
                    createInvoice made.instanceId { invoiceFor made.legNames[0] with invoiceDate = march 3; dueDate = march 20 }
                Assert.Equal(march 3, ((stored |> Invoice.invoiceDate) |> CashFlowComponent.InvoiceDate.value))
                Assert.Equal(march 20, ((stored |> Invoice.dueDate) |> CashFlowComponent.DueDate.value))
            })

    [<Theory>]
    [<InlineData("invoiceDate")>]
    [<InlineData("dueDate")>]
    member _.``REQ-CF-5.7 REQ-CF-5.8 for each of the invoice date and the due date, an UpdateInvoice payload setting that date to null is refused as it is read and the Invoice is unchanged`` (field:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! before = storedInvoice invoiceId
                let attempt = updateInvoiceSetting invoiceId (field + "Update") "null"
                let! after = storedInvoice invoiceId
                attempt |> expectRefusal refusedAsUpdateInvoiceJson
                Assert.Equal<Invoice.Invoice>(before, after)
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("Paid")>]
    [<InlineData("invoicereceived")>]
    member _.``REQ-CF-5.9 for each of the empty string, 'Paid' and 'invoicereceived', a CreateInvoice payload with that invoice state is rejected with a typed error and no Invoice is stored`` (state:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["invoiceState"] <- JsonValue.Create(state))
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt |> expectRefusal (invalidInvoiceStateOf state)
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Theory>]
    [<InlineData("InvoiceGenerated")>]
    [<InlineData("InvoiceSent")>]
    [<InlineData("InvoiceExpected")>]
    [<InlineData("InvoiceReceived")>]
    member _.``REQ-CF-5.9 for each of the four invoice states, a CreateInvoice payload with that state on an agreement of the direction it suits is stored with that state`` (state:string) =
        withBuilt (fun make ->
            result {
                let direction, expected =
                    match state with
                    | "InvoiceGenerated" -> Income, InvoiceGenerated
                    | "InvoiceSent" -> Income, InvoiceSent
                    | "InvoiceExpected" -> Outgo, InvoiceExpected
                    | _ -> Outgo, InvoiceReceived
                let! made = make direction 1 []
                let! stored = createInvoice made.instanceId { invoiceFor made.legNames[0] with invoiceState = state }
                Assert.Equal(expected, ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.invoiceState))
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("Paid")>]
    [<InlineData("invoicereceived")>]
    member _.``REQ-CF-5.9 for each of the empty string, 'Paid' and 'invoicereceived', an UpdateInvoice payload setting that invoice state is rejected with a typed error and the Invoice is unchanged`` (state:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! before = storedInvoice invoiceId
                let attempt = updateInvoiceSetting invoiceId "invoiceStateUpdate" (jsonString state)
                let! after = storedInvoice invoiceId
                attempt |> expectRefusal (invalidInvoiceStateOf state)
                Assert.Equal<Invoice.Invoice>(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-5.13 a CreateInvoice payload with no blocker is stored with no blocker`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let! stored = createInvoice made.instanceId (invoiceFor made.legNames[0])
                Assert.Equal(None, ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.blocker))
            })

    [<Theory>]
    [<InlineData("NoFunds")>]
    [<InlineData("Irresponsible")>]
    [<InlineData("NeedsDecision")>]
    [<InlineData("Other")>]
    member _.``REQ-CF-5.13 for each of NoFunds, Irresponsible, NeedsDecision with a note and Other with a note, a CreateInvoice payload with that blocker is stored with that blocker and note`` (blocker:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let given, expected =
                    match blocker with
                    | "NoFunds" -> Contracts.BlockerContract.NoFunds, Blocker.NoFunds
                    | "Irresponsible" -> Contracts.BlockerContract.Irresponsible, Blocker.Irresponsible
                    | "NeedsDecision" -> Contracts.BlockerContract.NeedsDecision "a note", Blocker.NeedsDecision(blockerNote "a note")
                    | _ -> Contracts.BlockerContract.Other "a note", Blocker.Other(blockerNote "a note")
                let! stored = createInvoice made.instanceId { invoiceFor made.legNames[0] with blocker = Some given }
                Assert.Equal(Some expected, ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.blocker))
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("Broke")>]
    [<InlineData("nofunds")>]
    member _.``REQ-CF-5.13 for each of the empty string, 'Broke' and 'nofunds', a CreateInvoice payload with that blocker state is refused as it is read and no Invoice is stored`` (blocker:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["blocker"] <- blockerJson blocker None)
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt |> expectRefusal refusedAsCreateInvoiceJson
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Theory>]
    [<InlineData("NeedsDecision", null)>]
    [<InlineData("NeedsDecision", "")>]
    [<InlineData("NeedsDecision", " \t ")>]
    [<InlineData("Other", null)>]
    [<InlineData("Other", "")>]
    [<InlineData("Other", " \t ")>]
    member _.``REQ-CF-5.14 for each of NeedsDecision and Other, a CreateInvoice payload giving that blocker with no note, an empty note, or a whitespace-only note is refused, the missing note as the payload is read and the others with a typed error, and no Invoice is stored`` (blocker:string, note:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["blocker"] <- blockerJson blocker (Some [ jsonString note ]))
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt
                |> expectRefusal (fun e ->
                    match isNull note, e with
                    | true, _ -> refusedAsCreateInvoiceJson e
                    | false, AsError (CashFlowError.CashflowBlockerNoteIsEmpty _) -> true
                    | _ -> false)
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Theory>]
    [<InlineData("NeedsDecision")>]
    [<InlineData("Other")>]
    member _.``REQ-CF-5.14 for each of NeedsDecision and Other, a CreateInvoice payload giving that blocker with a 501-character note is rejected with a typed error and no Invoice is stored, while a 500-character note is stored`` (blocker:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let noted length =
                    let note = noteOf length
                    match blocker with
                    | "NeedsDecision" -> Contracts.BlockerContract.NeedsDecision note
                    | _ -> Contracts.BlockerContract.Other note
                let attempt =
                    createInvoicePayload made.instanceId { invoiceFor made.legNames[0] with blocker = Some(noted 501) }
                    |> Result.bind (send "CreateInvoice")
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowBlockerNoteTooLong(_, limit)) -> limit = 500
                    | _ -> false)
                Assert.Empty(leg)
                let! stored = createInvoice made.instanceId { invoiceFor made.legNames[0] with blocker = Some(noted 500) }
                let storedNote =
                    match ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.blocker) with
                    | Some(Blocker.NeedsDecision note) | Some(Blocker.Other note) -> note |> BlockerNote.value
                    | _ -> ""
                Assert.Equal<string>(noteOf 500, storedNote)
            })

    [<Theory>]
    [<InlineData("NoFunds")>]
    [<InlineData("Irresponsible")>]
    member _.``REQ-CF-5.14 for each of NoFunds and Irresponsible, a CreateInvoice payload giving that blocker with a note attached is refused as it is read and no Invoice is stored`` (blocker:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["blocker"] <- blockerJson blocker (Some [ jsonString "a note" ]))
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt |> expectRefusal refusedAsCreateInvoiceJson
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.14 an UpdateInvoice payload setting the blocker to NeedsDecision without a note is refused as it is read and the Invoice is unchanged`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! before = storedInvoice invoiceId
                let attempt = updateInvoiceSetting invoiceId "blockerUpdate" ((blockerJson "NeedsDecision" None).ToJsonString())
                let! after = storedInvoice invoiceId
                attempt |> expectRefusal refusedAsUpdateInvoiceJson
                Assert.Equal<Invoice.Invoice>(before, after)
            })

    [<Theory>]
    [<InlineData("NeedsDecision")>]
    [<InlineData("Other")>]
    member _.``REQ-CF-5.14 for each of NeedsDecision and Other, an UpdateInvoice payload clearing the blocker of an Invoice holding that blocker with a note is accepted, and the Invoice read back from the store has no blocker and no note`` (blocker:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let given =
                    match blocker with
                    | "NeedsDecision" -> Contracts.BlockerContract.NeedsDecision "a note"
                    | _ -> Contracts.BlockerContract.Other "a note"
                let! created = createInvoice made.instanceId { invoiceFor made.legNames[0] with blocker = Some given }
                let invoiceId = created |> Invoice.invoiceId
                let! _ = updateInvoice { noInvoiceChange invoiceId with blockerUpdate = SetTo None }
                (* The store refuses to read back a note without a blocker, so a clean read shows the note is gone too. *)
                let! stored = storedInvoice invoiceId
                Assert.Equal(None, ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.blocker))
            })

    [<Theory>]
    [<InlineData("NoFunds")>]
    [<InlineData("Irresponsible")>]
    member _.``REQ-CF-5.14 for each of NoFunds and Irresponsible, an UpdateInvoice payload setting that blocker with a note attached is refused as it is read and the Invoice is unchanged`` (blocker:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! before = storedInvoice invoiceId
                let attempt = updateInvoiceSetting invoiceId "blockerUpdate" ((blockerJson blocker (Some [ jsonString "a note" ])).ToJsonString())
                let! after = storedInvoice invoiceId
                attempt |> expectRefusal refusedAsUpdateInvoiceJson
                Assert.Equal<Invoice.Invoice>(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-5.15 a CreateInvoice payload with no memo is stored with no memo`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let! stored = createInvoice made.instanceId (invoiceFor made.legNames[0])
                Assert.Equal(None, stored |> Invoice.memo)
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-5.15 for each of the empty string, a single space and a string of spaces and tabs, a CreateInvoice payload with that memo is rejected with a typed error and no Invoice is stored`` (memo:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInvoiceEdited made.instanceId (invoiceFor made.legNames[0]) (fun n -> n["memo"] <- JsonValue.Create(memo))
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt |> expectRefusal emptyInvoiceMemo
                Assert.Empty(leg)
                Assert.Empty(onInstance)
            })

    [<Fact>]
    member _.``REQ-CF-5.15 a CreateInvoice payload with a 2001-character memo is rejected with a typed error and no Invoice is stored, while a 2000-character memo is stored`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt =
                    createInvoicePayload made.instanceId { invoiceFor made.legNames[0] with memo = Some(String('m', 2001)) }
                    |> Result.bind (send "CreateInvoice")
                let! leg = invoicesOfLeg (fresh ()) made.legIds[0]
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowInvoiceMemoTooLong(_, limit)) -> limit = 2000
                    | _ -> false)
                Assert.Empty(leg)
                let! stored = createInvoice made.instanceId { invoiceFor made.legNames[0] with memo = Some(String('m', 2000)) }
                Assert.Equal(Some(String('m', 2000)), stored |> Invoice.memo |> Option.map InvoiceMemo.value)
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-5.15 for each of the empty string, a single space and a string of spaces and tabs, an UpdateInvoice payload setting that memo is rejected with a typed error and the Invoice is unchanged`` (memo:string) =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! before = storedInvoice invoiceId
                let attempt = updateInvoiceSetting invoiceId "memoUpdate" (jsonString memo)
                let! after = storedInvoice invoiceId
                attempt |> expectRefusal emptyInvoiceMemo
                Assert.Equal<Invoice.Invoice>(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-5.16 a CreateInvoice payload for a Payment Agreement that already has an Invoice on the Instance is rejected with a typed error and the Instance still has exactly its first Invoice for that Payment Agreement`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let attempt =
                    createInvoicePayload made.instanceId (invoiceFor made.legNames[0]) |> Result.bind (send "CreateInvoice")
                let! onInstance = invoicesOfInstance (fresh ()) made.instanceId
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowInstanceManyInvoicesForPaymentAgreement(instance, leg, count)) ->
                        instance = (made.instanceId |> InstanceId.value) && leg = (made.legIds[0] |> PaymentAgreementId.value)
                        && count = 2
                    | _ -> false)
                Assert.Equal<InvoiceId list>([ made.invoiceIds[0] ], onInstance |> List.map Invoice.invoiceId)
            })

    [<Fact>]
    member _.``REQ-CF-5.16 a CreateInvoice payload for a Payment Agreement that has an Invoice on another Instance of its Master Agreement is stored, and both Instances each read back exactly one Invoice for that Payment Agreement`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 [ 0 ]
                let! returned = createInstancePayload made []
                let! second = Json.fromJson<Contracts.InstanceCompositeReturn> returned
                let secondId = second.instance.instanceId |> InstanceId.fromGuid
                let! _ = createInvoice secondId (invoiceFor made.legNames[0])
                let! onFirst = invoicesOfInstance (fresh ()) made.instanceId
                let! onSecond = invoicesOfInstance (fresh ()) secondId
                let forLeg invoices = invoices |> List.filter (fun i -> i |> Invoice.paymentAgreementId = made.legIds[0])
                Assert.Single(forLeg onFirst) |> ignore
                Assert.Single(forLeg onSecond) |> ignore
            })

    [<Fact>]
    member _.``REQ-CF-5.16 a CreateInstance payload carrying two Invoices for the same Payment Agreement is rejected with a typed error and no Instance is stored`` () =
        withBuilt (fun make ->
            result {
                let! made = make Outgo 1 []
                let attempt = createInstancePayload made [ invoiceFor made.legNames[0]; invoiceFor made.legNames[0] ]
                let! dates = instanceDatesOf made
                attempt
                |> expectRefusal (function
                    | AsError (CashFlowError.CashflowInstanceManyInvoicesForPaymentAgreement(_, leg, count)) ->
                        leg = (made.legIds[0] |> PaymentAgreementId.value) && count = 2
                    | _ -> false)
                Assert.Equal<LocalDate list>([ march 1 ], dates)
            })
