module Tests.Integrated.CrossDomainOrchestration.PaymentDataStates

open System
open System.Text.Json.Nodes
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
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

(* Every test builds its own agreements, monthly on the 1st, with 100.00 legs: Outgo on F-2230 (debit, where its
   Payments land) and F-1280, Income on F-1280 and F-4290 (credit, where its Payments land). Each agreement has an
   Instance dated 1 March 2049 with a 100.00 Invoice on each leg the test asks for. The lines Payments point at are
   journal entries dated today and Classified staged entries, built by the test. The routes commit, so the setup
   commits too; tests read back from a fresh context, and a finally deletes the agreements, then the staged entries,
   then the journal entries. "No Payment is stored" means the Invoice's Payments are as before the call. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private march (day: int) = LocalDate(2049, 3, day)

let private april1 = LocalDate(2049, 4, 1)

let private direction (name: string) = if name = "Income" then Income else Outgo

type private Made =
    { agreementId: MasterAgreementId
      agreementName: string
      direction: FlowDirection
      legIds: PaymentAgreementId list
      legNames: string list
      instanceId: InstanceId
      /// The Invoices on the Instance, by leg index.
      invoiceIds: Map<int, InvoiceId> }

/// The account codes an agreement of the direction uses: where its Payments land, its other account, and one it
/// doesn't use.
let private accountsFor (direction: FlowDirection) =
    match direction with
    | Outgo -> "F-2230", "F-1280", "F-4290"
    | Income -> "F-4290", "F-1280", "F-2230"

/// Builds an agreement with the legs and its 1 March Instance, with an Invoice for each leg index given.
let private build
    (fixture: TestDataFixture) (context: Context.Context) (direction: FlowDirection) (legCount: int) (invoiced: int list) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    result {
        let name = $"Payment data state test {Guid.NewGuid():N}"
        let! agreementName = name |> AgreementName.create
        let! first = 1 |> Cadence.DateInMonthNumber.fromInt
        let! counterparty = "Payment data state test counterparty" |> Counterparty.create
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
              direction = direction
              legIds = legIds
              legNames = legNames
              instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
              invoiceIds = invoiceIds }
    }

let private paymentsOf (invoiceId: InvoiceId) = [ invoiceId ] |> Payment.fetchByInvoiceIdList (fresh ())

/// The ids of every stored Payment whose journal entry line or staged line is the one given.
let private paymentsPointingAt (lineUuid: Guid) =
    executeReaderQuery
        (fresh () |> Context.getDatabaseTransaction)
        "select unique_id from cashflow.payment where journal_entry_line_id = @line or stage_entry_line_id = @line"
        [ { name = "@line"; value = UniqueId lineUuid } ]
        (fun row -> row |> RowReader.getUuid "unique_id")
        Ok
        AnyQuantityIsAcceptable

let private invoicesOfLeg (legId: PaymentAgreementId) =
    Invoice.query (fresh ()) None Invoice.invoiceSelectFields None (Some "inv.payment_agreement_id = @leg") None None None
        [ { name = "@leg"; value = UniqueId(legId |> PaymentAgreementId.value) } ] AnyQuantityIsAcceptable

let private send (verb: string) (json: string) = routeUiCommandForTesting "CashFlow" verb [] json

let private pointerUuid (pointer: TransactionPointer) =
    match pointer with
    | Posted line -> line |> JournalEntryLineId.value
    | Staged line -> line |> StageEntryLineId.value

let private toContract (pointer: TransactionPointer) =
    match pointer with
    | Posted line -> Contracts.TransactionPointerContract.Posted(line |> JournalEntryLineId.value)
    | Staged line -> Contracts.TransactionPointerContract.Staged(line |> StageEntryLineId.value)

let private paymentFor (pointer: TransactionPointer) (amount: decimal) : Contracts.CreatePaymentFieldsInput =
    { transactionPointer = pointer |> toContract
      postedToFiDate = None
      postedToLedgerDate = None
      memo = None }

let private createPaymentPayload (invoiceId: InvoiceId) (payment: Contracts.CreatePaymentFieldsInput) =
    ({ invoiceId = invoiceId |> InvoiceId.value; payment = payment } : Contracts.CreatePaymentInput) |> Json.toJson

let private sendCreatePayment (invoiceId: InvoiceId) (payment: Contracts.CreatePaymentFieldsInput) =
    createPaymentPayload invoiceId payment |> Result.bind (send "CreatePayment")

/// Sends CreatePayment; returns the stored Payment pointing at the payload's line.
let private createPayment (invoiceId: InvoiceId) (pointer: TransactionPointer) (payment: Contracts.CreatePaymentFieldsInput) =
    result {
        let! _ = sendCreatePayment invoiceId payment
        let! payments = paymentsOf invoiceId
        return
            payments
            |> List.filter (fun p ->
                p |> Payment.transactionPointer |> pointerUuid = pointerUuid pointer
                || (match p |> Payment.transactionPointer, pointer with
                    | Posted _, Staged _ -> true
                    | _ -> false))
            |> List.exactlyOne
    }

let private invoiceFor (direction: FlowDirection) (legName: string) (payments: Contracts.CreatePaymentFieldsInput list)
    : Contracts.NewInvoiceFieldsInput =
    { paymentAgreementName = legName
      externalInvoiceId = None
      invoiceDate = march 1
      dueDate = march 31
      amount = 100.00M
      invoiceState = (match direction with | Outgo -> "InvoiceReceived" | Income -> "InvoiceSent")
      blocker = None
      memo = None
      payments = payments }

/// Builds what a test needs, each piece in a transaction of its own that commits, and remembers it for clean-up.
type private World(fixture: TestDataFixture) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
    let agreements = ResizeArray<MasterAgreementId>()
    let stageEntries = ResizeArray<StageEntryHeaderId>()
    let journalEntries = ResizeArray<JournalEntryHeaderId>()
    let committed body = runCommandRouteAndAutoCompleteTransaction CashFlowCreatePayment body
    let opposite lineType = if lineType = "Debit" then "Credit" else "Debit"
    let counterCode code = if code = "F-1280" then "F-2230" else "F-1280"
    let stagedTransitions (posted: bool) =
        let start = Clock.now ()
        [ yield (None, "Ingested", start, "StageIngestion")
          yield (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier")
          if posted then yield (Some "Classified", "Posted", start.Plus(Duration.FromMilliseconds(20L)), "LedgerPoster") ]

    member _.Agreements = agreements |> List.ofSeq
    member _.StageEntries = stageEntries |> List.ofSeq
    member _.JournalEntries = journalEntries |> List.ofSeq
    member _.today = Calendar.today ()

    member _.make (direction: FlowDirection) (legCount: int) (invoiced: int list) =
        committed (fun context -> build fixture context direction legCount invoiced)
        |> Result.map (fun made -> agreements.Add made.agreementId; made)

    /// A journal entry dated today with a line of the type and amount on the account, balanced on another account.
    /// Returns the pointer to that line and the entry's date.
    member this.jeLine (code: string) (lineType: string) (amount: decimal) =
        committed (fun context ->
            result {
                let! entry, headerId =
                    createTestJournalEntryFromPrimitives
                        context $"Payment data state test {Guid.NewGuid()}" None this.today
                        [ (accountIdOf code, amount, lineType, None)
                          (accountIdOf (counterCode code), amount, opposite lineType, None) ] [] []
                journalEntries.Add headerId
                let line =
                    entry
                    |> JournalEntryOrchestration.jeLines
                    |> List.find (fun l -> l |> JournalEntryLine.accountId = accountIdOf code)
                    |> JournalEntryLine.journalEntryLineId
                return Posted line, this.today
            })

    /// A Classified staged entry with a line of the type and amount on the account. Returns the pointer to that line.
    member this.stagedLine (code: string) (lineType: string) (amount: decimal) =
        committed (fun context ->
            result {
                let! staged =
                    createStageEntryForTest context "/tmp/payment-data-state-test.dat" $"Payment data state test {Guid.NewGuid()}"
                        (Guid.NewGuid().ToString()) testBank this.today
                        [ (amount, lineType, Some code, None, None)
                          (amount, opposite lineType, Some (counterCode code), None, None) ]
                        (stagedTransitions false)
                stageEntries.Add(staged |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                return
                    staged
                    |> StageEntryOrchestration.seLines
                    |> List.find (fun l -> l |> StageEntryLine.accountId = Some(accountIdOf code))
                    |> StageEntryLine.stageEntryLineId
                    |> Staged
            })

    /// A journal entry dated today with a Debit line of jeAmount on the account, and a Posted staged entry of
    /// stagedAmount whose Debit line on the account records that journal entry line. Returns the staged pointer, the
    /// journal entry pointer and the entry's date.
    member this.postedStagedLine (code: string) (stagedAmount: decimal) (jeAmount: decimal) =
        committed (fun context ->
            result {
                let! entry, headerId =
                    createTestJournalEntryFromPrimitives
                        context $"Payment data state test {Guid.NewGuid()}" None this.today
                        [ (accountIdOf code, jeAmount, "Debit", None)
                          (accountIdOf (counterCode code), jeAmount, "Credit", None) ] [] []
                journalEntries.Add headerId
                let jeLineOn c =
                    entry
                    |> JournalEntryOrchestration.jeLines
                    |> List.find (fun l -> l |> JournalEntryLine.accountId = accountIdOf c)
                    |> JournalEntryLine.journalEntryLineId
                let! staged =
                    createStageEntryForTest context "/tmp/payment-data-state-test.dat" $"Payment data state test {Guid.NewGuid()}"
                        (Guid.NewGuid().ToString()) testBank this.today
                        [ (stagedAmount, "Debit", Some code, None, Some(jeLineOn code))
                          (stagedAmount, "Credit", Some (counterCode code), None, Some(jeLineOn (counterCode code))) ]
                        (stagedTransitions true)
                stageEntries.Add(staged |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                let stagedLine =
                    staged
                    |> StageEntryOrchestration.seLines
                    |> List.find (fun l -> l |> StageEntryLine.accountId = Some(accountIdOf code))
                    |> StageEntryLine.stageEntryLineId
                return Staged stagedLine, Posted(jeLineOn code), this.today
            })

[<Collection("SharedTestData")>]
type PaymentDataStatesTests(fixture: TestDataFixture) =

    (* Runs the test with a World that commits what it builds, then deletes it all. *)
    let withWorld (test: World -> Result<unit, IAppError>) =
        let world = World(fixture)
        let cleanUpFailures = ResizeArray<string>()
        try
            test world |> railroadWrapper
        finally
            [ for id in world.Agreements do yield Cleanup.cleanUpMasterAgreementTree (Some(id |> MasterAgreementId.value))
              for id in world.StageEntries do yield Cleanup.cleanUpStageEntryHeaderId (Some id)
              for id in world.JournalEntries do yield Cleanup.cleanUpJournalEntryId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)


    [<Fact>]
    member _.``REQ-CF-6.2 two Payments created through CreatePayment carry distinct, non-empty Payment IDs`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! first, _ = w.jeLine "F-2230" "Debit" 50.00M
                let! second, _ = w.jeLine "F-2230" "Debit" 50.00M
                let! _ = sendCreatePayment invoiceId (paymentFor first 50.00M)
                let! _ = sendCreatePayment invoiceId (paymentFor second 50.00M)
                let! payments = paymentsOf invoiceId
                let ids = payments |> List.map (Payment.paymentId >> PaymentId.value)
                Assert.Equal(2, ids |> List.distinct |> List.length)
                Assert.DoesNotContain(Guid.Empty, ids)
            })

    [<Fact>]
    member _.``REQ-CF-6.3 a CreatePayment payload whose Invoice ID names no stored Invoice is rejected with a typed error and no Payment is stored pointing at its line`` () =
        withWorld (fun w ->
            result {
                let! _ = w.make Outgo 1 [ 0 ]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let attempt = sendCreatePayment (InvoiceId.create ()) (paymentFor line 100.00M)
                let! pointing = paymentsPointingAt (pointerUuid line)
                Assert.True(attempt |> Result.isError)
                Assert.Empty(pointing)
            })

    [<Fact>]
    member _.``REQ-CF-6.3 with two Invoices stored, a Payment created through CreatePayment is stored on the Invoice it named and the other Invoice's Payments are unchanged`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 2 [ 0; 1 ]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment made.invoiceIds[1] line (paymentFor line 100.00M)
                let! onOther = paymentsOf made.invoiceIds[0]
                Assert.Equal(made.invoiceIds[1], stored |> Payment.invoiceId)
                Assert.Empty(onOther)
            })

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.4 for each of a journal entry line and a staged line, a CreatePayment payload pointing at that line is stored with exactly that pointer and no pointer of the other kind`` (pointer:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line =
                    match pointer with
                    | "Posted" -> w.jeLine "F-2230" "Debit" 100.00M |> Result.map fst
                    | _ -> w.stagedLine "F-2230" "Debit" 100.00M
                let! stored = createPayment invoiceId line (paymentFor line 100.00M)
                let! stagedColumn = stored |> Payment.paymentId |> Payment.fetchStageEntryLineIdById (fresh ())
                (* A stored journal entry line would read back as Posted, so a Staged read shows that column is empty. *)
                Assert.Equal(line, stored |> Payment.transactionPointer)
                match line with
                | Posted _ -> Assert.Equal(None, stagedColumn)
                | Staged stagedLine -> Assert.Equal(Some stagedLine, stagedColumn)
            })

    [<Fact>]
    member _.``REQ-CF-6.4 a CreatePayment payload with a null transaction pointer is rejected with a typed error and no Payment is stored`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let attempt = 
                    result {
                        let! json = createPaymentPayload invoiceId (paymentFor line 100.00M)
                        let node = JsonNode.Parse(json)
                        node["payment"].["transactionPointer"] <- null
                        return! send "CreatePayment" (node.ToJsonString())
                    }
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    [<Fact>]
    member _.``REQ-CF-6.4 REQ-CF-6.5 REQ-CF-6.10 a Payment that pointed at a staged line, once that line is posted and payments transition, is stored with both the journal entry line and the original staged line, reads as Posted, and takes its amount and posted-to-ledger date from the journal entry line`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! stagedLine, jeLine, entryDate = w.postedStagedLine "F-2230" 30.00M 40.00M
                let! _ = sendCreatePayment invoiceId (paymentFor stagedLine 30.00M)
                let! _ = routeUiCommandForTesting "CashFlow" "TransitionPaymentsToPosted" [] "{}"
                let! payment = paymentsOf invoiceId |> Result.map List.exactlyOne
                let! stagedColumn = payment |> Payment.paymentId |> Payment.fetchStageEntryLineIdById (fresh ())
                let stagedLineId = match stagedLine with | Staged l -> Some l | Posted _ -> None
                Assert.Equal(jeLine, payment |> Payment.transactionPointer)
                Assert.Equal(stagedLineId, stagedColumn)
                Assert.Equal(40.00M, ((payment |> Payment.amount) |> CashFlowComponent.PaymentAmount.value) |> Money.amount)
                Assert.Equal(Some entryDate, payment |> Payment.postedToLedgerDate |> Option.map CashFlowComponent.PostedToLedgerDate.value)
            })

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.5 for each of a Posted and a Staged pointer, a CreatePayment payload giving an amount different from its line's is read back with the line's amount`` (pointer:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line =
                    match pointer with
                    | "Posted" -> w.jeLine "F-2230" "Debit" 40.00M |> Result.map fst
                    | _ -> w.stagedLine "F-2230" "Debit" 40.00M
                (* Both amounts leave the 100.00 Invoice PartiallyPaid, so only the read-back amount can differ. *)
                let! stored = createPayment invoiceId line (paymentFor line 30.00M)
                Assert.Equal(40.00M, ((stored |> Payment.amount) |> CashFlowComponent.PaymentAmount.value) |> Money.amount)
            })

    [<Fact>]
    member _.``REQ-CF-6.6 a CreatePayment payload with no posted-to-FI date is stored with none`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment invoiceId line { paymentFor line 100.00M with postedToFiDate = None }
                Assert.Equal(None, stored |> Payment.postedToFiDate)
            })

    [<Fact>]
    member _.``REQ-CF-6.6 a CreatePayment payload with a posted-to-FI date is stored with exactly that date`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment invoiceId line { paymentFor line 100.00M with postedToFiDate = Some(march 5) }
                Assert.Equal(Some(march 5), stored |> Payment.postedToFiDate |> Option.map CashFlowComponent.PostedToFiDate.value)
            })

    [<Fact>]
    member _.``REQ-CF-6.7 a CreatePayment payload with no memo is stored with no memo`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment invoiceId line { paymentFor line 100.00M with memo = None }
                Assert.Equal(None, stored |> Payment.memo)
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-6.7 for each of the empty string, a single space and a string of spaces and tabs, a CreatePayment payload with that memo is rejected with a typed error and no Payment is stored`` (memo:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let attempt = sendCreatePayment invoiceId { paymentFor line 100.00M with memo = Some memo }
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    [<Fact>]
    member _.``REQ-CF-6.7 a CreatePayment payload with a 2001-character memo is rejected with a typed error and no Payment is stored, while a 2000-character memo is stored with exactly that memo`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let attempt = sendCreatePayment invoiceId { paymentFor line 100.00M with memo = Some(String('m', 2001)) }
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
                let! stored = createPayment invoiceId line { paymentFor line 100.00M with memo = Some(String('m', 2000)) }
                Assert.Equal(Some(String('m', 2000)), stored |> Payment.memo |> Option.map PaymentMemo.value)
            })

    [<Fact>]
    member _.``REQ-CF-6.7 a CreatePayment payload whose memo has text surrounded by spaces is stored rather than rejected as whitespace-only`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, _ = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment invoiceId line { paymentFor line 100.00M with memo = Some "  a memo  " }
                Assert.Contains("a memo", stored |> Payment.memo |> Option.map PaymentMemo.value |> Option.defaultValue "")
            })

    [<Theory>]
    [<InlineData("Income", "OtherAccount")>]
    [<InlineData("Income", "UnusedAccount")>]
    [<InlineData("Outgo", "OtherAccount")>]
    [<InlineData("Outgo", "UnusedAccount")>]
    member _.``REQ-CF-6.9 for each of Income and Outgo, and each of a line on the Payment Agreement's other account and a line on an account the agreement doesn't use, a CreatePayment payload pointing at that line is rejected with a typed error and no Payment is stored`` (direction':string, line:string) =
        withWorld (fun w ->
            result {
                let d = direction direction'
                let! made = w.make d 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let _, otherCode, unusedCode = accountsFor d
                let! wrong, _ = w.jeLine (if line = "OtherAccount" then otherCode else unusedCode) "Debit" 100.00M
                let attempt = sendCreatePayment invoiceId (paymentFor wrong 100.00M)
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.9 for each of a journal entry line and a staged line on the Payment Agreement's other account, a CreatePayment payload pointing at it is rejected with a typed error and no Payment is stored`` (pointer:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line =
                    match pointer with
                    | "Posted" -> w.jeLine "F-1280" "Debit" 100.00M |> Result.map fst
                    | _ -> w.stagedLine "F-1280" "Debit" 100.00M
                let attempt = sendCreatePayment invoiceId (paymentFor line 100.00M)
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    [<Theory>]
    [<InlineData("Income", "Debit")>]
    [<InlineData("Income", "Credit")>]
    [<InlineData("Outgo", "Debit")>]
    [<InlineData("Outgo", "Credit")>]
    member _.``REQ-CF-6.9 for each of Income and Outgo, and each of a Debit line and a Credit line on the account the direction requires, a CreatePayment payload pointing at that line is stored`` (direction':string, lineType:string) =
        withWorld (fun w ->
            result {
                let d = direction direction'
                let! made = w.make d 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let requiredCode, _, _ = accountsFor d
                let! line, _ = w.jeLine requiredCode lineType 100.00M
                let! stored = createPayment invoiceId line (paymentFor line 100.00M)
                Assert.Equal(line, stored |> Payment.transactionPointer)
            })

    [<Theory>]
    [<InlineData("WrongAccount")>]
    [<InlineData("NoSuchLine")>]
    member _.``REQ-CF-6.9 REQ-CF-6.11 for each of a line on the wrong account and a journal entry line ID that names no line, a CreateInvoice payload carrying a Payment pointing at it is rejected with a typed error and no Invoice is stored`` (bad:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 2 [ 0 ]
                let! line =
                    match bad with
                    | "WrongAccount" -> w.jeLine "F-4290" "Debit" 100.00M |> Result.map fst
                    | _ -> Ok(Posted(JournalEntryLineId.fromGuid (Guid.NewGuid())))
                let invoice = invoiceFor Outgo made.legNames[1] [ paymentFor line 100.00M ]
                let attempt =
                    ({ instanceId = made.instanceId |> InstanceId.value; invoice = invoice } : Contracts.CreateInvoiceInput)
                    |> Json.toJson
                    |> Result.bind (send "CreateInvoice")
                let! invoices = invoicesOfLeg made.legIds[1]
                let! pointing = paymentsPointingAt (pointerUuid line)
                Assert.True(attempt |> Result.isError)
                Assert.Empty(invoices)
                Assert.Empty(pointing)
            })

    [<Theory>]
    [<InlineData("WrongAccount")>]
    [<InlineData("NoSuchLine")>]
    member _.``REQ-CF-6.9 REQ-CF-6.11 for each of a line on the wrong account and a journal entry line ID that names no line, a CreateInstance payload carrying a Payment pointing at it is rejected with a typed error and nothing from the payload is stored`` (bad:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let! line =
                    match bad with
                    | "WrongAccount" -> w.jeLine "F-4290" "Debit" 100.00M |> Result.map fst
                    | _ -> Ok(Posted(JournalEntryLineId.fromGuid (Guid.NewGuid())))
                let attempt =
                    ({ masterAgreementName = made.agreementName
                       instanceDate = april1
                       invoices = [ invoiceFor Outgo made.legNames[0] [ paymentFor line 100.00M ] ] } : Contracts.CreateInstanceInput)
                    |> Json.toJson
                    |> Result.bind (send "CreateInstance")
                let! dates =
                    [ made.agreementId ] |> Instance.fetchByMasterAgreementIdList (fresh ())
                    |> Result.map (List.map Instance.instanceDate)
                let! pointing = paymentsPointingAt (pointerUuid line)
                Assert.True(attempt |> Result.isError)
                Assert.Equal<LocalDate list>([ march 1 ], dates)
                Assert.Empty(pointing)
            })

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving no posted-to-ledger date and pointing at a journal entry line reads back with a posted-to-ledger date equal to that entry's entry date`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let! line, entryDate = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment made.invoiceIds[0] line { paymentFor line 100.00M with postedToLedgerDate = None }
                Assert.Equal(Some entryDate, stored |> Payment.postedToLedgerDate |> Option.map CashFlowComponent.PostedToLedgerDate.value)
            })

    [<Fact>]
    member _.``REQ-CF-6.10 a Payment pointing at a staged line reads back with no posted-to-ledger date`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let! line = w.stagedLine "F-2230" "Debit" 100.00M
                let! stored = createPayment made.invoiceIds[0] line (paymentFor line 100.00M)
                Assert.Equal(None, stored |> Payment.postedToLedgerDate)
            })

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving its journal entry's own date as the posted-to-ledger date is stored and reads back with that date`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let! line, entryDate = w.jeLine "F-2230" "Debit" 100.00M
                let! stored = createPayment made.invoiceIds[0] line { paymentFor line 100.00M with postedToLedgerDate = Some entryDate }
                Assert.Equal(Some entryDate, stored |> Payment.postedToLedgerDate |> Option.map CashFlowComponent.PostedToLedgerDate.value)
            })

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving a posted-to-ledger date different from its journal entry's date is rejected with a typed error and no Payment is stored`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line, entryDate = w.jeLine "F-2230" "Debit" 100.00M
                let attempt = sendCreatePayment invoiceId { paymentFor line 100.00M with postedToLedgerDate = Some(entryDate.PlusDays(-1)) }
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving a posted-to-ledger date for a staged line is rejected with a typed error and no Payment is stored`` () =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let! line = w.stagedLine "F-2230" "Debit" 100.00M
                let attempt = sendCreatePayment invoiceId { paymentFor line 100.00M with postedToLedgerDate = Some w.today }
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.11 for each of a journal entry line ID and a staged line ID that names no stored line, a CreatePayment payload pointing at it is rejected with a typed error and no Payment is stored`` (pointer:string) =
        withWorld (fun w ->
            result {
                let! made = w.make Outgo 1 [ 0 ]
                let invoiceId = made.invoiceIds[0]
                let line =
                    match pointer with
                    | "Posted" -> Posted(JournalEntryLineId.fromGuid (Guid.NewGuid()))
                    | _ -> Staged(StageEntryLineId.fromGuid (Guid.NewGuid()))
                let attempt = sendCreatePayment invoiceId (paymentFor line 100.00M)
                let! payments = paymentsOf invoiceId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(payments)
            })

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-CF-6.4 for each of Income and Outgo, a CreatePayment payload pointing at a line on an account the Payment Agreement does not name is stored with that pointer`` () =
        Assert.Fail "Not yet implemented"
