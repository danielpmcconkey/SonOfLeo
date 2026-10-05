module Tests.Integrated.CrossDomainOrchestration.MaintenanceOperations

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Every test builds its own Outgo agreements with 100.00 legs on F-2230 (debit, where Payments land) and F-1280.
   A "monthly" agreement is due on the 1st and has an Instance dated 1 March 2049; a "daily" one has an Instance dated
   today, so a staged line dated today falls inside its Invoices' dates for linkage (REQ-CF-13.2). Each Instance has a
   100.00 Invoice on each leg the test asks for. Payments point at journal entry lines or Classified staged lines dated
   today. The routes commit, so the setup commits too; tests read back from a fresh context, and a finally deletes the
   agreements (links included), then the staged entries, then the journal entries. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private march (day: int) = LocalDate(2049, 3, day)

let private april1 = LocalDate(2049, 4, 1)

let private unique (label: string) = $"{label} {Guid.NewGuid():N}"

type private Made =
    { agreementId: MasterAgreementId
      agreementName: string
      legIds: PaymentAgreementId list
      legNames: string list
      instanceId: InstanceId
      /// The Invoices on the Instance, by leg index.
      invoiceIds: Map<int, InvoiceId> }

/// Builds an Outgo agreement with the legs and its one Instance, with an Invoice for each leg index given.
let private build (fixture: TestDataFixture) (context: Context.Context) (daily: bool) (legCount: int) (invoiced: int list) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    result {
        let name = unique "Maintenance operations test"
        let! agreementName = name |> AgreementName.create
        let! first = 1 |> Cadence.DateInMonthNumber.fromInt
        let! counterparty = "Maintenance operations test counterparty" |> Counterparty.create
        let! activityPeriod =
            ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
        let instanceDate = if daily then Calendar.today () else march 1
        let cadence = if daily then Cadence.Daily else Cadence.Monthly(Cadence.DateInMonth first)
        let legNames = List.init legCount (fun i -> $"{name} leg {i + 1}")
        let! legs =
            legNames
            |> List.map (fun legName ->
                result {
                    let! paName = legName |> PaymentAgreementName.create
                    let! expected = Money.fromDecimal 100.00M
                    let! due = 0 |> DaysDueAfterInvoiceDate.create
                    return (paName, DebitAccount.create(accountIdOf "F-2230"), CreditAccount.create(accountIdOf "F-1280"), Some expected, Some due, None)
                })
            |> convertListOfResultsToResultsList
        let! agreement =
            AgreementOrchestration.constructNewAndPersist
                context agreementName Outgo cadence { nextInstance = instanceDate } counterparty activityPeriod None legs
        let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
        let legIds =
            legNames
            |> List.map (fun legName ->
                agreement
                |> AgreementOrchestration.paymentAgreements
                |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                |> PaymentAgreement.paymentAgreementId)
        let! amount = Money.fromDecimal 100.00M
        let invoices =
            invoiced
            |> List.map (fun i ->
                (legIds[i], None, (InvoiceDate.create instanceDate), (DueDate.create (instanceDate.PlusDays(30))),
                 (InvoiceAmount.create amount), InvoiceReceived, None, None, []))
        let! created = InstanceOrchestration.constructNewAndPersist context agreementId instanceDate invoices
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

let private send (verb: string) (json: string) = routeUiCommandForTesting "CashFlow" verb [] json

let private sendInput (verb: string) (input: 'a) = input |> Json.toJson |> Result.bind (send verb)

let private rawQuery (sql: string) (parameters: QueryParameter list) (read: RowReader -> 'r) =
    executeReaderQuery (fresh () |> Context.getDatabaseTransaction) sql parameters read Ok AnyQuantityIsAcceptable

let private text (name: string) (value: string) = { name = name; value = CharString value }

let private uuid (name: string) (value: Guid) = { name = name; value = UniqueId value }

let private masterAgreementIdsNamed (name: string) =
    rawQuery "select unique_id from cashflow.master_agreement where agreement_name = @name" [ text "@name" name ]
        (RowReader.getUuid "unique_id")

let private paymentAgreementIdsNamed (name: string) =
    rawQuery "select unique_id from cashflow.payment_agreement where payment_agreement_name = @name" [ text "@name" name ]
        (RowReader.getUuid "unique_id")

/// Each stored Payment Agreement of the Master Agreement named, as (name, debit account code, credit account code).
let private legsOfAgreementNamed (name: string) =
    rawQuery
        """select pa.payment_agreement_name, d.code as debit_code, c.code as credit_code
           from cashflow.payment_agreement pa
           join cashflow.master_agreement m on m.unique_id = pa.master_agreement_id
           join ledger.account d on d.unique_id = pa.debit_account
           join ledger.account c on c.unique_id = pa.credit_account
           where m.agreement_name = @name"""
        [ text "@name" name ]
        (fun row ->
            row |> RowReader.getString "payment_agreement_name",
            row |> RowReader.getString "debit_code",
            row |> RowReader.getString "credit_code")

/// The ids of every stored Payment whose journal entry line or staged line is the one given.
let private paymentsPointingAt (lineUuid: Guid) =
    rawQuery "select unique_id from cashflow.payment where journal_entry_line_id = @line or stage_entry_line_id = @line"
        [ uuid "@line" lineUuid ] (RowReader.getUuid "unique_id")

let private instancesOf (made: Made) = [ made.agreementId ] |> Instance.fetchByMasterAgreementIdList (fresh ())

let private invoicesOfLeg (legId: PaymentAgreementId) =
    Invoice.query (fresh ()) None Invoice.invoiceSelectFields None (Some "inv.payment_agreement_id = @leg") None None None
        [ uuid "@leg" (legId |> PaymentAgreementId.value) ] AnyQuantityIsAcceptable

let private invoicesOfInstance (instanceId: InstanceId) =
    instanceId
    |> InstanceOrchestration.fetchCompositeByInstanceId (fresh ())
    |> Result.map (InstanceOrchestration.invoiceComposites >> List.map InstanceOrchestration.invoice)

let private paymentsOf (invoiceId: InvoiceId) = [ invoiceId ] |> Payment.fetchByInvoiceIdList (fresh ())

let private storedInvoice (invoiceId: InvoiceId) = invoiceId |> Invoice.fetchById (fresh ())

let private linksOf (lineId: StageEntryLineId) = lineId |> PaymentAgreementLink.fetchByStageEntryLineId (fresh ())

let private pointerUuid (pointer: TransactionPointer) =
    match pointer with
    | Posted(line, _) -> line |> JournalEntryLineId.value
    | Staged line -> line |> StageEntryLineId.value

let private pointersOf (invoiceId: InvoiceId) =
    paymentsOf invoiceId |> Result.map (List.map (Payment.transactionPointer >> pointerUuid) >> Set.ofList)

let private toContract (pointer: TransactionPointer) =
    match pointer with
    | Posted(line, _) -> Contracts.TransactionPointerContract.Posted(line |> JournalEntryLineId.value)
    | Staged line -> Contracts.TransactionPointerContract.Staged(line |> StageEntryLineId.value)

let private paymentFor (pointer: TransactionPointer) (amount: decimal) : Contracts.CreatePaymentFieldsInput =
    { transactionPointer = pointer |> toContract
      postedToFiDate = None
      postedToLedgerDate = None
      memo = None }

let private invoiceFor (legName: string) (blocker: Contracts.BlockerContract option)
    (payments: Contracts.CreatePaymentFieldsInput list) : Contracts.NewInvoiceFieldsInput =
    { paymentAgreementName = legName
      externalInvoiceId = None
      invoiceDate = march 1
      dueDate = march 31
      amount = 100.00M
      invoiceState = "InvoiceReceived"
      blocker = blocker
      memo = None
      payments = payments }

let private createPayment (invoiceId: InvoiceId) (payment: Contracts.CreatePaymentFieldsInput) =
    sendInput "CreatePayment" ({ invoiceId = invoiceId |> InvoiceId.value; payment = payment } : Contracts.CreatePaymentInput)

let private createInvoice (instanceId: InstanceId) (invoice: Contracts.NewInvoiceFieldsInput) =
    sendInput "CreateInvoice" ({ instanceId = instanceId |> InstanceId.value; invoice = invoice } : Contracts.CreateInvoiceInput)

let private createInstance (agreementName: string) (date: LocalDate) (invoices: Contracts.NewInvoiceFieldsInput list) =
    sendInput "CreateInstance"
        ({ masterAgreementName = agreementName; instanceDate = date; invoices = invoices } : Contracts.CreateInstanceInput)

let private deletePayment (paymentId: Guid) =
    sendInput "DeletePayment" ({ paymentId = paymentId } : Contracts.DeletePaymentInput)

let private noInvoiceChange (invoiceId: InvoiceId) : Contracts.UpdateInvoiceInput =
    { invoiceId = invoiceId |> InvoiceId.value
      externalInvoiceIdUpdate = NoChange
      invoiceDateUpdate = NoChange
      dueDateUpdate = NoChange
      amountUpdate = NoChange
      invoiceStateUpdate = NoChange
      blockerUpdate = NoChange
      memoUpdate = NoChange }

let private noAgreementChange (name: string) : Contracts.UpdateAgreementInput =
    { agreementName = name
      agreementNameUpdate = NoChange
      directionUpdate = NoChange
      cadenceUpdate = NoChange
      counterpartyUpdate = NoChange
      activeBeginUpdate = NoChange
      activeEndUpdate = NoChange
      memoUpdate = NoChange
      paymentAgreementUpdates = []
      newPaymentAgreements = [] }

let private noLegChange (name: string) : Contracts.UpdatePaymentAgreementInput =
    { paymentAgreementName = name
      paymentAgreementNameUpdate = NoChange
      debitAccountCodeUpdate = NoChange
      creditAccountCodeUpdate = NoChange
      expectedAmountUpdate = NoChange
      daysDueAfterInvoiceDateUpdate = NoChange
      memoUpdate = NoChange }

let private legInput (name: string) (debitCode: string) (creditCode: string) : Contracts.CreatePaymentAgreementFieldsInput =
    { paymentAgreementName = name
      debitAccountCode = debitCode
      creditAccountCode = creditCode
      expectedAmount = Some 100.00M
      daysDueAfterInvoiceDate = Some 0
      memo = None }

let private agreementInput (name: string) (legs: Contracts.CreatePaymentAgreementFieldsInput list) : Contracts.CreateAgreementInput =
    { agreementName = name
      direction = "Outgo"
      cadence =
        { cadenceType = Contracts.CadenceTypeContract.Monthly(Contracts.MonthDayContract.DateInMonth 1)
          nextInstance = march 1 }
      counterparty = "Maintenance operations test counterparty"
      activeBegin = (Calendar.today ()).PlusYears(-1)
      activeEnd = None
      memo = None
      paymentAgreements = legs }

let private stagedLineId (pointer: TransactionPointer) =
    match pointer with
    | Staged line -> line
    | Posted _ -> failwith "expected a staged pointer"

/// Builds what a test needs, each piece in a transaction of its own that commits, and remembers it for clean-up.
type private World(fixture: TestDataFixture) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
    let agreements = ResizeArray<MasterAgreementId>()
    let agreementNames = ResizeArray<string>()
    let stageEntries = ResizeArray<StageEntryHeaderId>()
    let journalEntries = ResizeArray<JournalEntryHeaderId>()
    let committed body = runCommandRouteAndAutoCompleteTransaction CashFlowCreatePayment body

    member _.Agreements = agreements |> List.ofSeq
    member _.AgreementNames = agreementNames |> List.ofSeq
    member _.StageEntries = stageEntries |> List.ofSeq
    member _.JournalEntries = journalEntries |> List.ofSeq
    member _.today = Calendar.today ()

    /// Remembers a name the test will create an agreement under through a route.
    member _.named (name: string) = agreementNames.Add name; name

    member _.monthly (legCount: int) (invoiced: int list) =
        committed (fun context -> build fixture context false legCount invoiced)
        |> Result.map (fun made -> agreements.Add made.agreementId; made)

    member _.daily (legCount: int) (invoiced: int list) =
        committed (fun context -> build fixture context true legCount invoiced)
        |> Result.map (fun made -> agreements.Add made.agreementId; made)

    /// A journal entry dated today with a Debit line of the amount on F-2230, balanced on F-1280.
    member this.jeLine (amount: decimal) =
        committed (fun context ->
            result {
                let! entry, headerId =
                    createTestJournalEntryFromPrimitives
                        context $"Maintenance operations test {Guid.NewGuid()}" None this.today
                        [ (accountIdOf "F-2230", amount, "Debit", None)
                          (accountIdOf "F-1280", amount, "Credit", None) ] [] []
                journalEntries.Add headerId
                return
                    entry
                    |> JournalEntryOrchestration.jeLines
                    |> List.find (fun l -> l |> JournalEntryLine.accountId = accountIdOf "F-2230")
                    |> JournalEntryLine.journalEntryLineId
                    |> fun line -> Posted(line, None)
            })

    /// A Classified staged entry dated today with the description, a Debit line of the amount on F-2230 and a Credit
    /// line on F-1280. Returns the Debit line.
    member this.stagedLine (description: string) (amount: decimal) =
        committed (fun context ->
            result {
                let start = Clock.now ()
                let! staged =
                    createStageEntryForTest context "/tmp/maintenance-operations-test.dat" description
                        (Guid.NewGuid().ToString()) testBank this.today
                        [ (amount, "Debit", Some "F-2230", None, None)
                          (amount, "Credit", Some "F-1280", None, None) ]
                        [ (None, "Ingested", start, "StageIngestion")
                          (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier") ]
                stageEntries.Add(staged |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                return
                    staged
                    |> StageEntryOrchestration.seLines
                    |> List.find (fun l -> l |> StageEntryLine.accountId = Some(accountIdOf "F-2230"))
                    |> StageEntryLine.stageEntryLineId
            })

    member _.link (legId: PaymentAgreementId) (lineId: StageEntryLineId) =
        runCommandRouteAndAutoCompleteTransaction CreatePaymentAgreementLink (fun context ->
            CashFlowOps.constructNewAndPersist context legId lineId)

    /// Runs linkage and matching with an active rule claiming entries of the description for the leg, rolls it all
    /// back, and returns the line's links as the run left them.
    member _.linkageRunClaiming (legId: PaymentAgreementId) (description: string) (lineId: StageEntryLineId) =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let! name = $"Maintenance operations test rule {Guid.NewGuid()}" |> ClassificationRuleName.create
                let! pattern = description |> StringSearchPattern.create
                let! groups = [ ("And", [ FieldMatch.Description pattern ], None) ] |> createClassificationRuleGroupListForTest
                let! _ =
                    ClassificationOrchestration.constructNewAndPersist
                        context name (ClassificationClaimant.PaymentAgreement legId) 500 groups
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                return! lineId |> PaymentAgreementLink.fetchByStageEntryLineId context
            })

[<Collection("SharedTestData")>]
type MaintenanceOperationsTests(fixture: TestDataFixture) =

    (* Runs the test with a World that commits what it builds, then deletes it all. *)
    let withWorld (test: World -> Result<unit, IAppError>) =
        let world = World(fixture)
        let cleanUpFailures = ResizeArray<string>()
        try
            test world |> railroadWrapper
        finally
            let byName =
                world.AgreementNames
                |> List.collect (fun name ->
                    match masterAgreementIdsNamed name with
                    | Ok ids -> ids |> List.map Ok
                    | Error e -> [ Error e ])
            [ for id in world.Agreements do yield Cleanup.cleanUpMasterAgreementTree (Some(id |> MasterAgreementId.value))
              for id in byName do yield id |> Result.bind (Some >> Cleanup.cleanUpMasterAgreementTree)
              for id in world.StageEntries do yield Cleanup.cleanUpStageEntryHeaderId (Some id)
              for id in world.JournalEntries do yield Cleanup.cleanUpJournalEntryId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

    // =========================================================================
    // REQ-CF-14.1 — create an agreement with its Payment Agreements
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.1 a CreateAgreement payload stores the Master Agreement and every Payment Agreement, each Payment Agreement with the accounts whose codes it gave`` () =
        withWorld (fun w ->
            result {
                let name = w.named (unique "Maintenance operations test")
                let! _ =
                    agreementInput name [ legInput $"{name} leg 1" "F-2230" "F-1280"; legInput $"{name} leg 2" "F-2230" "F-1290" ]
                    |> sendInput "CreateAgreement"
                let! masters = masterAgreementIdsNamed name
                let! legs = legsOfAgreementNamed name
                Assert.Equal(1, masters.Length)
                Assert.Equal<(string * string * string) list>(
                    [ ($"{name} leg 1", "F-2230", "F-1280"); ($"{name} leg 2", "F-2230", "F-1290") ], legs |> List.sort)
            })

    [<Fact>]
    member _.``REQ-CF-14.1 a CreateAgreement payload whose second Payment Agreement gives an account code that doesn't exist is rejected with a typed error and nothing is written, not even the first Payment Agreement`` () =
        withWorld (fun w ->
            result {
                let name = w.named (unique "Maintenance operations test")
                let attempt =
                    agreementInput name [ legInput $"{name} leg 1" "F-2230" "F-1280"; legInput $"{name} leg 2" "ZZ-NOPE" "F-1280" ]
                    |> sendInput "CreateAgreement"
                let namesTheCode =
                    match attempt with
                    | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId code)) -> code = "ZZ-NOPE"
                    | _ -> false
                let! masters = masterAgreementIdsNamed name
                let! firstLeg = paymentAgreementIdsNamed $"{name} leg 1"
                Assert.True(namesTheCode)
                Assert.Empty(masters)
                Assert.Empty(firstLeg)
            })

    // =========================================================================
    // REQ-CF-14.3 — fetch one agreement's whole tree by name
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.3 FetchAgreementSummary by name returns the agreement with every one of its Payment Agreements, Instances, Invoices and Payments, and nothing of another agreement's`` () =
        withWorld (fun w ->
            result {
                let! wanted = w.monthly 2 [ 0; 1 ]
                let! other = w.monthly 1 [ 0 ]
                let! wantedLine = w.jeLine 40.00M
                let! otherLine = w.jeLine 40.00M
                let! _ = createPayment wanted.invoiceIds[0] (paymentFor wantedLine 40.00M)
                let! _ = createPayment other.invoiceIds[0] (paymentFor otherLine 40.00M)
                let! secondJson = createInstance wanted.agreementName april1 []
                let! second = Json.fromJson<Contracts.InstanceCompositeReturn> secondJson
                let! wantedPayments = paymentsOf wanted.invoiceIds[0]
                let! json = sendInput "FetchAgreementSummary" ({ agreementName = wanted.agreementName } : Contracts.FetchAgreementSummaryInput)
                let! summary = Json.fromJson<Contracts.AgreementReturn> json
                Assert.Equal(wanted.agreementId |> MasterAgreementId.value, summary.masterAgreement.agreementId)
                Assert.Equal<Set<Guid>>(
                    wanted.legIds |> List.map PaymentAgreementId.value |> Set.ofList,
                    summary.paymentAgreements |> List.map _.paymentAgreementId |> Set.ofList)
                Assert.Equal<Set<Guid>>(
                    set [ wanted.instanceId |> InstanceId.value; second.instance.instanceId ],
                    summary.instances |> List.map _.instanceId |> Set.ofList)
                Assert.Equal<Set<Guid>>(
                    wanted.invoiceIds |> Map.values |> Seq.map InvoiceId.value |> Set.ofSeq,
                    summary.invoices |> List.map _.invoiceId |> Set.ofList)
                Assert.Equal<Set<Guid>>(
                    wantedPayments |> List.map (Payment.paymentId >> PaymentId.value) |> Set.ofList,
                    summary.payments |> List.map _.paymentId |> Set.ofList)
            })

    // =========================================================================
    // REQ-CF-14.4 — create an Instance with its Invoices and Payments
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.4 a CreateInstance payload stores the Instance on the named Master Agreement with every Invoice it carries and every Payment each Invoice carries`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 2 []
                let! first = w.jeLine 30.00M
                let! second = w.jeLine 40.00M
                let! third = w.jeLine 50.00M
                let! _ =
                    createInstance made.agreementName april1
                        [ invoiceFor made.legNames[0] None [ paymentFor first 30.00M; paymentFor second 40.00M ]
                          invoiceFor made.legNames[1] None [ paymentFor third 50.00M ] ]
                let! instances = instancesOf made
                let created = instances |> List.filter (fun i -> i |> Instance.instanceDate = april1) |> List.exactlyOne
                let! invoices = invoicesOfInstance (created |> Instance.instanceId)
                let onLeg i = invoices |> List.find (fun inv -> inv |> Invoice.paymentAgreementId = made.legIds[i])
                let! firstPointers = pointersOf (onLeg 0 |> Invoice.invoiceId)
                let! secondPointers = pointersOf (onLeg 1 |> Invoice.invoiceId)
                Assert.Equal(2, invoices.Length)
                Assert.Equal<Set<Guid>>(set [ pointerUuid first; pointerUuid second ], firstPointers)
                Assert.Equal<Set<Guid>>(set [ pointerUuid third ], secondPointers)
            })

    [<Fact>]
    member _.``REQ-CF-14.4 a CreateInstance payload carrying no Invoices stores the Instance with no Invoices`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 []
                let! _ = createInstance made.agreementName april1 []
                let! instances = instancesOf made
                let created = instances |> List.filter (fun i -> i |> Instance.instanceDate = april1)
                let! invoices = invoicesOfInstance (created |> List.exactlyOne |> Instance.instanceId)
                Assert.Single(created) |> ignore
                Assert.Empty(invoices)
            })

    [<Fact>]
    member _.``REQ-CF-14.4 REQ-CF-9.3 a CreateInstance payload whose second Invoice carries a blocker and Payments summing to its amount is rejected and nothing is written, not even the Instance or the first Invoice`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 2 []
                let! first = w.jeLine 100.00M
                let! second = w.jeLine 100.00M
                let attempt =
                    createInstance made.agreementName april1
                        [ invoiceFor made.legNames[0] None [ paymentFor first 100.00M ]
                          invoiceFor made.legNames[1] (Some Contracts.BlockerContract.NoFunds) [ paymentFor second 100.00M ] ]
                let! instances = instancesOf made
                let! firstLegInvoices = invoicesOfLeg made.legIds[0]
                let! secondLegInvoices = invoicesOfLeg made.legIds[1]
                let! onFirst = paymentsPointingAt (pointerUuid first)
                let! onSecond = paymentsPointingAt (pointerUuid second)
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowInvoiceFullyPaidWithBlocker _)) -> ()
                    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Equal<LocalDate list>([ march 1 ], instances |> List.map Instance.instanceDate)
                Assert.Empty(firstLegInvoices)
                Assert.Empty(secondLegInvoices)
                Assert.Empty(onFirst)
                Assert.Empty(onSecond)
            })

    // =========================================================================
    // REQ-CF-14.5 — add an Invoice, update an Invoice, add a Payment
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.5 a CreateInvoice payload stores the Invoice on the named existing Instance with every Payment it carries`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 2 [ 0 ]
                let! first = w.jeLine 40.00M
                let! second = w.jeLine 60.00M
                let! _ =
                    createInvoice made.instanceId (invoiceFor made.legNames[1] None [ paymentFor first 40.00M; paymentFor second 60.00M ])
                let! invoices = invoicesOfLeg made.legIds[1]
                let invoice = Assert.Single(invoices)
                let! pointers = pointersOf (invoice |> Invoice.invoiceId)
                Assert.Equal(made.instanceId, invoice |> Invoice.instanceId)
                Assert.Equal<Set<Guid>>(set [ pointerUuid first; pointerUuid second ], pointers)
            })

    [<Fact>]
    member _.``REQ-CF-14.5 a CreateInvoice payload carrying no Payments stores the Invoice with no Payments`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 2 [ 0 ]
                let! _ = createInvoice made.instanceId (invoiceFor made.legNames[1] None [])
                let! invoices = invoicesOfLeg made.legIds[1]
                let invoice = Assert.Single(invoices)
                let! payments = paymentsOf (invoice |> Invoice.invoiceId)
                Assert.Equal(made.instanceId, invoice |> Invoice.instanceId)
                Assert.Empty(payments)
            })

    [<Fact>]
    member _.``REQ-CF-14.5 REQ-CF-9.3 a CreateInvoice payload carrying a blocker and Payments summing to its amount is rejected and nothing is written`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 2 [ 0 ]
                let! line = w.jeLine 100.00M
                let attempt =
                    createInvoice made.instanceId
                        (invoiceFor made.legNames[1] (Some Contracts.BlockerContract.NoFunds) [ paymentFor line 100.00M ])
                let! invoices = invoicesOfLeg made.legIds[1]
                let! onLine = paymentsPointingAt (pointerUuid line)
                let! instance = made.instanceId |> Instance.fetchById (fresh ())
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowInvoiceFullyPaidWithBlocker _)) -> ()
                    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Empty(invoices)
                Assert.Empty(onLine)
                Assert.False(instance |> Instance.isFulfilled)
            })

    [<Fact>]
    member _.``REQ-CF-14.5 REQ-CF-9.3 a CreatePayment payload, valid on its own, that would make a blocked Invoice FullyPaid is rejected and nothing is written`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! _ =
                    { noInvoiceChange invoiceId with blockerUpdate = SetTo(Some Contracts.BlockerContract.NoFunds) }
                    |> sendInput "UpdateInvoice"
                let! line = w.jeLine 100.00M
                let attempt = createPayment invoiceId (paymentFor line 100.00M)
                let! payments = paymentsOf invoiceId
                let! onLine = paymentsPointingAt (pointerUuid line)
                let! stored = storedInvoice invoiceId
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowInvoiceFullyPaidWithBlocker uuid)) ->
                        Assert.Equal(invoiceId |> InvoiceId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Empty(payments)
                Assert.Empty(onLine)
                Assert.Equal(NotYetPaid, ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState))
            })

    [<Fact>]
    member _.``REQ-CF-14.5 an UpdateInvoice payload changing the external invoice ID, invoice date, due date, amount, invoice state, blocker and memo stores every one of the new values`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! _ =
                    { noInvoiceChange invoiceId with
                        externalInvoiceIdUpdate = SetTo(Some "EXT-14.5")
                        invoiceDateUpdate = SetTo(march 2)
                        dueDateUpdate = SetTo(march 20)
                        amountUpdate = SetTo 150.00M
                        invoiceStateUpdate = SetTo "InvoiceExpected"
                        blockerUpdate = SetTo(Some(Contracts.BlockerContract.NeedsDecision "which account"))
                        memoUpdate = SetTo(Some "updated memo") }
                    |> sendInput "UpdateInvoice"
                let! stored = storedInvoice invoiceId
                let state = stored |> Invoice.invoiceLifeCycleState
                let! note = "which account" |> BlockerNote.create
                Assert.Equal(Some "EXT-14.5", stored |> Invoice.externalInvoiceId |> Option.map ExternalInvoiceId.value)
                Assert.Equal(march 2, ((stored |> Invoice.invoiceDate) |> CashFlowComponent.InvoiceDate.value))
                Assert.Equal(march 20, ((stored |> Invoice.dueDate) |> CashFlowComponent.DueDate.value))
                Assert.Equal(150.00M, ((stored |> Invoice.amount) |> CashFlowComponent.InvoiceAmount.value) |> Money.amount)
                Assert.Equal(InvoiceExpected, (state |> CashFlowComponent.InvoiceLifeCycleState.invoiceState))
                Assert.Equal(Some(Blocker.NeedsDecision note), (state |> CashFlowComponent.InvoiceLifeCycleState.blocker))
                Assert.Equal(Some "updated memo", stored |> Invoice.memo |> Option.map InvoiceMemo.value)
            })

    [<Fact>]
    member _.``REQ-CF-14.5 REQ-CF-9.4 an UpdateInvoice payload that changes the memo and sets a blocker on a FullyPaid Invoice is rejected and the Invoice is unchanged, memo included`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line = w.jeLine 100.00M
                let! _ = createPayment invoiceId (paymentFor line 100.00M)
                let! before = storedInvoice invoiceId
                let attempt =
                    { noInvoiceChange invoiceId with
                        memoUpdate = SetTo(Some "should not stick")
                        blockerUpdate = SetTo(Some Contracts.BlockerContract.NoFunds) }
                    |> sendInput "UpdateInvoice"
                let! after = storedInvoice invoiceId
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowInvoiceFullyPaidWithBlocker uuid)) ->
                        Assert.Equal(invoiceId |> InvoiceId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Equal(FullyPaid, ((before |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState))
                Assert.Equal(None, after |> Invoice.memo)
                Assert.Equal(None, ((after |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.blocker))
                Assert.Equal(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-14.5 a CreatePayment payload stores the Payment on the named existing Invoice`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line = w.jeLine 40.00M
                let! _ = createPayment invoiceId (paymentFor line 40.00M)
                let! payments = paymentsOf invoiceId
                let payment = Assert.Single(payments)
                Assert.Equal(invoiceId, payment |> Payment.invoiceId)
                Assert.Equal(pointerUuid line, payment |> Payment.transactionPointer |> pointerUuid)
            })

    [<Fact>]
    member _.``REQ-CF-14.5 REQ-CF-9.8 REQ-CF-9.10 a CreatePayment payload that takes a FullyPaid Invoice's Payments past its amount is stored, the Invoice's payment state becomes PartiallyPaid, and its Instance is no longer fulfilled`` () =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! full = w.jeLine 100.00M
                let! extra = w.jeLine 10.00M
                let! _ = createPayment invoiceId (paymentFor full 100.00M)
                let! paidInstance = made.instanceId |> Instance.fetchById (fresh ())
                let! paidInvoice = storedInvoice invoiceId
                let! _ = createPayment invoiceId (paymentFor extra 10.00M)
                let! pointers = pointersOf invoiceId
                let! stored = storedInvoice invoiceId
                let! instance = made.instanceId |> Instance.fetchById (fresh ())
                Assert.Equal(FullyPaid, ((paidInvoice |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState))
                Assert.True(paidInstance |> Instance.isFulfilled)
                Assert.Equal<Set<Guid>>(set [ pointerUuid full; pointerUuid extra ], pointers)
                Assert.Equal(PartiallyPaid, ((stored |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState))
                Assert.False(instance |> Instance.isFulfilled)
            })

    // =========================================================================
    // REQ-CF-14.6 — delete a Payment and the link that produced it
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.6 DeletePayment on the only Payment referencing a line deletes the Payment and the Payment Agreement Link that produced it, and the line is a linkage candidate again`` () =
        withWorld (fun w ->
            result {
                let! made = w.daily 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let description = unique "Maintenance operations test payment"
                let! lineId = w.stagedLine description 100.00M
                let! _ = w.link made.legIds[0] lineId
                let! _ = createPayment invoiceId (paymentFor (Staged lineId) 100.00M)
                let! created = paymentsOf invoiceId
                let! _ = deletePayment (created |> List.exactlyOne |> Payment.paymentId |> PaymentId.value)
                let! payments = paymentsOf invoiceId
                let! onLine = paymentsPointingAt (lineId |> StageEntryLineId.value)
                let! links = linksOf lineId
                let! relinked = w.linkageRunClaiming made.legIds[0] description lineId
                Assert.Empty(payments)
                Assert.Empty(onLine)
                Assert.Empty(links)
                Assert.Equal(made.legIds[0], relinked |> List.exactlyOne |> PaymentAgreementLink.paymentAgreementId)
            })

    [<Fact>]
    member _.``REQ-CF-14.6 DeletePayment on one of two Payments referencing the same line deletes only that Payment, keeps the Payment Agreement Link, and the line is not a linkage candidate`` () =
        withWorld (fun w ->
            result {
                let! made = w.daily 2 [ 0; 1 ]
                let description = unique "Maintenance operations test payment"
                let! lineId = w.stagedLine description 50.00M
                let! link = w.link made.legIds[0] lineId
                let! _ = createPayment made.invoiceIds[0] (paymentFor (Staged lineId) 50.00M)
                let! _ = createPayment made.invoiceIds[1] (paymentFor (Staged lineId) 50.00M)
                let! onFirst = paymentsOf made.invoiceIds[0]
                let! kept = paymentsOf made.invoiceIds[1]
                let! _ = deletePayment (onFirst |> List.exactlyOne |> Payment.paymentId |> PaymentId.value)
                let! firstAfter = paymentsOf made.invoiceIds[0]
                let! secondAfter = paymentsOf made.invoiceIds[1]
                let! links = linksOf lineId
                let! afterRun = w.linkageRunClaiming made.legIds[1] description lineId
                let linkId = link |> PaymentAgreementLink.paymentAgreementLinkId
                Assert.Empty(firstAfter)
                Assert.Equal<PaymentId list>(kept |> List.map Payment.paymentId, secondAfter |> List.map Payment.paymentId)
                Assert.Equal(linkId, links |> List.exactlyOne |> PaymentAgreementLink.paymentAgreementLinkId)
                Assert.Equal<PaymentAgreementLinkId list>([ linkId ], afterRun |> List.map PaymentAgreementLink.paymentAgreementLinkId)
            })

    [<Fact>]
    member _.``REQ-CF-14.6 a DeletePayment payload whose Payment ID names no Payment is rejected with a typed error naming the ID and no Payment or Payment Agreement Link is deleted`` () =
        withWorld (fun w ->
            result {
                let! made = w.daily 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! lineId = w.stagedLine (unique "Maintenance operations test payment") 100.00M
                let! _ = w.link made.legIds[0] lineId
                let! _ = createPayment invoiceId (paymentFor (Staged lineId) 100.00M)
                let! before = paymentsOf invoiceId
                let missing = Guid.NewGuid()
                let attempt = deletePayment missing
                let namesTheId =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowPaymentIdDoesntExist id)) -> id = missing
                    | _ -> false
                let! after = paymentsOf invoiceId
                let! links = linksOf lineId
                Assert.True(namesTheId)
                Assert.Equal<PaymentId list>(before |> List.map Payment.paymentId, after |> List.map Payment.paymentId)
                Assert.Single(links) |> ignore
            })

    // =========================================================================
    // REQ-CF-14.7 — names that match nothing
    // =========================================================================

    [<Theory>]
    [<InlineData("FetchAgreementSummary")>]
    [<InlineData("CreateInstance")>]
    [<InlineData("UpdateAgreement")>]
    member _.``REQ-CF-14.7 for every route that looks up a Master Agreement by name (FetchAgreementSummary, CreateInstance, UpdateAgreement), a name that matches nothing fails with a typed error naming it`` (route:string) =
        let name = unique "No such agreement"
        let attempt =
            match route with
            | "FetchAgreementSummary" ->
                sendInput route ({ agreementName = name } : Contracts.FetchAgreementSummaryInput)
            | "CreateInstance" -> createInstance name april1 []
            | _ -> sendInput route { noAgreementChange name with memoUpdate = SetTo(Some "a memo") }
        let namesIt =
            match attempt with
            | Error (AsError (CashFlowError.CashflowAgreementNameDoesntMatchId n)) -> n = name
            | _ -> false
        Assert.True(namesIt)

    [<Theory>]
    [<InlineData("CreateInstance")>]
    [<InlineData("CreateInvoice")>]
    [<InlineData("UpdateAgreement")>]
    member _.``REQ-CF-14.7 for every route whose payload names a Payment Agreement (CreateInstance, CreateInvoice, UpdateAgreement), a Payment Agreement name that matches nothing fails with a typed error naming it`` (route:string) =
        withWorld (fun w ->
            result {
                let! made = w.monthly 1 []
                let name = unique "No such payment agreement"
                let attempt =
                    match route with
                    | "CreateInstance" -> createInstance made.agreementName april1 [ invoiceFor name None [] ]
                    | "CreateInvoice" -> createInvoice made.instanceId (invoiceFor name None [])
                    | _ ->
                        sendInput route
                            { noAgreementChange made.agreementName with
                                paymentAgreementUpdates = [ { noLegChange name with memoUpdate = SetTo(Some "a memo") } ] }
                let namesIt =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementNameDoesntMatchId n)) -> n = name
                    | _ -> false
                Assert.True(namesIt)
            })
