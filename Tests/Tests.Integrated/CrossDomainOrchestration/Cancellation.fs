module Tests.Integrated.CrossDomainOrchestration.Cancellation

open System
open App.DataAccessLayer.DbTransaction
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
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.TestError
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Every test builds its own agreement on the fixture's cash flow accounts: debit F-2230 (a liability), credit F-1280
   (cash) for Outgo; debit F-1280, credit F-4290 (revenue) for Income. Monthly on the 1st, every leg 100.00 and due on
   its invoice date. A Payment points at a line of a journal entry the test posts.

   The operator cancels through the CashFlow routes, which commit. Those tests make their setup in a committed
   transaction, call the route, read back from a fresh context, and delete the agreement (with everything under it) and
   the journal entries in a finally. The projection, sweep and matching tests run in one transaction that is rolled
   back: what they check is what those operations read, and the sweep and matching routes would commit work on the
   fixture's own agreements too. *)

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today()
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cash = accountIdOf "F-1280"
    let revenueId = accountIdOf "F-4290"
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
    let headerIdList = ResizeArray<JournalEntryHeaderId>()
    let directions = Collections.Generic.Dictionary<MasterAgreementId, FlowDirection>()

    member _.Context = context
    member _.firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
    member this.firstOfMonthsAgo (months: int) = this.firstOfThisMonth.PlusMonths(-months)
    member _.cashId = cash
    member _.journalEntryHeaderIds = headerIdList |> List.ofSeq

    /// An agreement with the given number of legs. Returns its id, its legs' ids and its legs' names, in leg order.
    member this.agreement (name: string) (direction: FlowDirection) (legCount: int) =
        result {
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Cancellation test counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (this.firstOfMonthsAgo 4) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let debit, credit =
                match direction with
                | Outgo -> loanId, cash
                | Income -> cash, revenueId
            let legNames = [ 1 .. legCount ] |> List.map (fun i -> $"{name} leg {i}")
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
                    context agreementName direction (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = this.firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None legs
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            directions[agreementId] <- direction
            let legIds =
                legNames
                |> List.map (fun legName ->
                    agreement
                    |> AgreementOrchestration.paymentAgreements
                    |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                    |> PaymentAgreement.paymentAgreementId)
            return agreementId, legIds, legNames
        }

    /// A journal entry for the amount (Debit F-2230, Credit F-1280); returns its Debit line.
    member this.ledgerLine (amount: decimal) =
        result {
            let! entry, headerId =
                createTestJournalEntryFromPrimitives
                    context $"Cancellation test payment {Guid.NewGuid()}" None this.firstOfThisMonth
                    [ (loanId, amount, "Debit", None); (cash, amount, "Credit", None) ] [] []
            headerIdList.Add headerId
            return
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = loanId)
                |> JournalEntryLine.journalEntryLineId
        }

    /// An Instance on the date with an Invoice for each (leg, amount, Posted Payment amounts) given, invoiced and due
    /// on the Instance date, InvoiceReceived for Outgo and InvoiceSent for Income. Returns the Instance's id and its Invoices' ids in the order given.
    member this.instance agreementId (date: LocalDate) (invoices: (PaymentAgreementId * decimal * decimal list) list) =
        result {
            let state = if directions[agreementId] = Income then InvoiceSent else InvoiceReceived
            let! invoiceFields =
                invoices
                |> List.map (fun (legId, amount, paid) ->
                    result {
                        let! money = Money.fromDecimal amount
                        let! payments =
                            paid
                            |> List.map (fun p ->
                                this.ledgerLine p |> Result.map (fun line -> (TransactionPointer.Posted(line, None), None, None, None)))
                            |> convertListOfResultsToResultsList
                        return
                            (legId, None, (InvoiceDate.create date), (DueDate.create date), (InvoiceAmount.create money),
                             state, None, None, payments)
                    })
                |> convertListOfResultsToResultsList
            let! created = InstanceOrchestration.constructNewAndPersist context agreementId date invoiceFields
            let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
            let composites = created |> InstanceOrchestration.invoiceComposites
            let invoiceIds =
                invoices
                |> List.map (fun (legId, _, _) ->
                    composites
                    |> List.find (fun c -> c |> InstanceOrchestration.invoice |> Invoice.paymentAgreementId = legId)
                    |> InstanceOrchestration.invoice
                    |> Invoice.invoiceId)
            return instanceId, invoiceIds
        }

    /// A Classified staged entry (Debit F-2230, Credit F-1280) of 100.00 whose Debit line is linked to the leg.
    member _.linkedLine legId (entryDate: LocalDate) =
        result {
            let start = Clock.now()
            let! staged =
                createStageEntryForTest context "/tmp/cancellation-test.dat" $"Cancellation test {Guid.NewGuid()}"
                    (Guid.NewGuid().ToString()) testBank entryDate
                    [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                    [ (None, "Ingested", start, "StageIngestion")
                      (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier") ]
            let lineId =
                staged
                |> StageEntryOrchestration.seLines
                |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit)
                |> StageEntryLine.stageEntryLineId
            let! _ = CashFlowOps.constructNewAndPersist context legId lineId
            return lineId
        }

    member _.cancelInstance (note: string) (instanceId: InstanceId) =
        result {
            let! reason = note |> CancellationReasonNote.create
            return! instanceId |> InstanceOrchestration.cancelInstance context reason
        }

    member _.cancelInvoice (note: string) (invoiceId: InvoiceId) =
        result {
            let! reason = note |> CancellationReasonNote.create
            return! invoiceId |> InstanceOrchestration.cancelInvoice context reason
        }

    member _.projection (days: int) =
        result {
            let! horizon = days |> ProjectionHorizonInDays.create
            return! horizon |> CashFlowOps.projectCashFlowNDaysForward context
        }

let private fresh () = Context.create NoTransaction FetchOnly

let private routeWith (verb: string) (input: 'T) =
    result {
        let! json = Json.toJson input
        return! routeUiCommandForTesting "CashFlow" verb [] json
    }

let private cancelInstanceRoute (note: string) (instanceId: InstanceId) =
    routeWith "CancelInstance" ({ instanceId = instanceId |> InstanceId.value; cancellationReasonNote = note } : Contracts.CancelInstanceInput)

let private cancelInvoiceRoute (note: string) (invoiceId: InvoiceId) =
    routeWith "CancelInvoice" ({ invoiceId = invoiceId |> InvoiceId.value; cancellationReasonNote = note } : Contracts.CancelInvoiceInput)

/// A Posted Payment on the line, for the Invoice, through the CreatePayment route.
let private createPaymentRoute (invoiceId: InvoiceId) (line: JournalEntryLineId) =
    routeWith "CreatePayment"
        ({ invoiceId = invoiceId |> InvoiceId.value
           payment =
             { transactionPointer = Contracts.TransactionPointerContract.Posted(line |> JournalEntryLineId.value)
               postedToFiDate = None
               postedToLedgerDate = None
               memo = None } } : Contracts.CreatePaymentInput)

/// A 100.00 Invoice for the named leg, invoiced and due on the date, through the CreateInvoice route.
let private createInvoiceRoute (instanceId: InstanceId) (legName: string) (date: LocalDate) =
    routeWith "CreateInvoice"
        ({ instanceId = instanceId |> InstanceId.value
           invoice =
             { paymentAgreementName = legName
               externalInvoiceId = None
               invoiceDate = date
               dueDate = date
               amount = 100.00M
               invoiceState = "InvoiceReceived"
               blocker = None
               memo = None
               payments = [] } } : Contracts.CreateInvoiceInput)

let private noteOfInstance (instanceId: InstanceId) =
    instanceId |> Instance.fetchById (fresh ()) |> Result.map (Instance.cancellationReasonNote >> Option.map CancellationReasonNote.value)

let private noteOfInvoice (invoiceId: InvoiceId) =
    invoiceId |> Invoice.fetchById (fresh ()) |> Result.map (Invoice.cancellationReasonNote >> Option.map CancellationReasonNote.value)

let private invoiceIdsOfInstance (instanceId: InstanceId) =
    [ instanceId ] |> Invoice.fetchByInstanceIdList (fresh ()) |> Result.map (List.map Invoice.invoiceId >> Set.ofList)

let private instanceIdsOf (composites: InstanceOrchestration.InstanceComposite list) =
    composites |> List.map (InstanceOrchestration.instance >> Instance.instanceId) |> Set.ofList

let private wrongError (e: IAppError) : Result<unit, IAppError> = Error (TestingError $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
let private unexpectedOk () : Result<unit, IAppError> = Error (TestingError "Expected failure; got success")

[<Collection("SharedTestData")>]
type CancellationTests(fixture: TestDataFixture) =

    let rolledBack (body: Scenario -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context -> body (Scenario(fixture, context)))
        |> railroadWrapper

    (* Runs setup in a committed transaction, then the test, then deletes the agreement (with everything under it) and
       any journal entries the setup made, children first. *)
    let committed (setup: Scenario -> Result<MasterAgreementId * 'a, IAppError>) (test: 'a -> Result<unit, IAppError>) =
        let mutable agreementId: Guid option = None
        let mutable headerIds: JournalEntryHeaderId list = []
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! made =
                    runCommandRouteAndAutoCompleteTransaction CashFlowCreateInstance (fun context ->
                        let s = Scenario(fixture, context)
                        let made = setup s
                        headerIds <- s.journalEntryHeaderIds
                        made |> Result.map (fun (id, m) -> agreementId <- Some(id |> MasterAgreementId.value); m))
                return! test made
            }
            |> railroadWrapper
        finally
            [ yield Cleanup.cleanUpMasterAgreementTree agreementId
              for id in headerIds do yield Cleanup.cleanUpJournalEntryId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

    /// An Outgo agreement with two legs and one Instance this month with a 100.00 Invoice on each leg, the first
    /// carrying Posted Payments of `paidOnFirst`. Returns the Instance's id and its two Invoices' ids.
    let twoInvoiceInstance (label: string) (paidOnFirst: decimal list) (s: Scenario) =
        result {
            let! agreementId, legIds, _ = s.agreement $"{label} {Guid.NewGuid()}" Outgo 2
            let! instanceId, invoiceIds =
                s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, paidOnFirst); (legIds[1], 100.00M, []) ]
            return agreementId, (instanceId, invoiceIds)
        }

    // =========================================================================
    // REQ-CF-4.11 – 4.13 — cancelling an Instance
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-4.11 cancelling an Instance with a reason note stores the Instance as cancelled carrying exactly that note, read back from a fresh context`` () =
        let note = "Created under the wrong cadence"
        committed (twoInvoiceInstance "CF-4.11 cancel" [])
            (fun (instanceId, _) ->
                result {
                    let! _ = cancelInstanceRoute note instanceId
                    let! stored = noteOfInstance instanceId
                    Assert.Equal(Some note, stored)
                })

    [<Fact>]
    member _.``REQ-CF-4.11 REQ-CF-5.17 an Instance and an Invoice that have never been cancelled read back with no cancellation reason note`` () =
        (* The fixture's agreement A has never had anything cancelled. Read through the operator's summary route. *)
        let cashFlow = fixture.Data.cashFlow
        result {
            let! master = cashFlow.agreementAId |> MasterAgreement.fetchById (fresh ())
            let! returned =
                routeWith "FetchAgreementSummary"
                    ({ agreementName = master |> MasterAgreement.agreementName |> AgreementName.value } : Contracts.FetchAgreementSummaryInput)
            let! summary = Json.fromJson<Contracts.AgreementReturn> returned
            let instance = summary.instances |> List.find (fun i -> i.instanceId = (cashFlow.openInstanceAId |> InstanceId.value))
            let invoice = summary.invoices |> List.find (fun i -> i.invoiceId = (cashFlow.openInvoiceAId |> InvoiceId.value))
            Assert.Equal(None, instance.cancellationReasonNote)
            Assert.Equal(None, invoice.cancellationReasonNote)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-4.11 a request to cancel an Instance with a whitespace-only reason note is rejected with a typed error and the Instance and its Invoices read back uncancelled`` () =
        let blank = "   \t   "
        committed (twoInvoiceInstance "CF-4.11 blank note" [])
            (fun (instanceId, invoiceIds) ->
                result {
                    do!
                        match cancelInstanceRoute blank instanceId with
                        | Error (AsError (CashFlowError.CashflowCancellationReasonNoteIsEmpty raw)) ->
                            Assert.Equal(blank, raw)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! instanceNote = noteOfInstance instanceId
                    let! invoiceNotes = invoiceIds |> List.map noteOfInvoice |> convertListOfResultsToResultsList
                    Assert.Equal(None, instanceNote)
                    Assert.Equal<string option list>([ None; None ], invoiceNotes)
                })

    [<Fact>]
    member _.``REQ-CF-4.12 cancelling an Instance cancels every Invoice it holds, each carrying the Instance's reason note`` () =
        let note = "Lease ended before this period"
        committed (twoInvoiceInstance "CF-4.12 cascade" [])
            (fun (instanceId, invoiceIds) ->
                result {
                    let! _ = cancelInstanceRoute note instanceId
                    let! storedIds = invoiceIdsOfInstance instanceId
                    let! invoiceNotes = invoiceIds |> List.map noteOfInvoice |> convertListOfResultsToResultsList
                    Assert.Equal<Set<InvoiceId>>(invoiceIds |> Set.ofList, storedIds)
                    Assert.Equal<string option list>([ Some note; Some note ], invoiceNotes)
                })

    [<Fact>]
    member _.``REQ-CF-4.12 cancelling an Instance one of whose Invoices has a Payment is rejected with a typed error naming that Invoice, and neither the Instance nor any of its Invoices is cancelled`` () =
        committed (twoInvoiceInstance "CF-4.12 paid" [ 40.00M ])
            (fun (instanceId, invoiceIds) ->
                result {
                    do!
                        match cancelInstanceRoute "Never going to be paid" instanceId with
                        | Error (AsError (CashFlowError.CashflowInstanceCancellationBlockedByPayments(instanceUuid, invoiceUuids))) ->
                            Assert.Equal(instanceId |> InstanceId.value, instanceUuid)
                            Assert.Equal<Guid list>([ invoiceIds[0] |> InvoiceId.value ], invoiceUuids)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! instanceNote = noteOfInstance instanceId
                    let! invoiceNotes = invoiceIds |> List.map noteOfInvoice |> convertListOfResultsToResultsList
                    Assert.Equal(None, instanceNote)
                    Assert.Equal<string option list>([ None; None ], invoiceNotes)
                })

    [<Fact>]
    member _.``REQ-CF-4.13 a second cancellation of a cancelled Instance, with a different reason note, is rejected with a typed error and the Instance keeps its original note`` () =
        let original = "Spawned by mistake"
        committed (twoInvoiceInstance "CF-4.13 twice" [])
            (fun (instanceId, _) ->
                result {
                    let! _ = cancelInstanceRoute original instanceId
                    do!
                        match cancelInstanceRoute "A different reason" instanceId with
                        | Error (AsError (CashFlowError.CashflowInstanceCancelled uuid)) ->
                            Assert.Equal(instanceId |> InstanceId.value, uuid)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! stored = noteOfInstance instanceId
                    Assert.Equal(Some original, stored)
                })

    [<Fact>]
    member _.``REQ-CF-4.13 adding an Invoice to a cancelled Instance is rejected with a typed error and no Invoice is stored`` () =
        (* The Instance has an Invoice on its first leg only, so its second leg is free for a new one. *)
        committed
            (fun s ->
                result {
                    let! agreementId, legIds, legNames = s.agreement $"CF-4.13 add invoice {Guid.NewGuid()}" Outgo 2
                    let! instanceId, _ = s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, []) ]
                    return agreementId, (instanceId, legNames[1], s.firstOfThisMonth)
                })
            (fun (instanceId, freeLegName, date) ->
                result {
                    let! _ = cancelInstanceRoute "Nothing will be billed" instanceId
                    let! before = invoiceIdsOfInstance instanceId
                    do!
                        match createInvoiceRoute instanceId freeLegName date with
                        | Error (AsError (CashFlowError.CashflowInstanceCancelled uuid)) ->
                            Assert.Equal(instanceId |> InstanceId.value, uuid)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! after = invoiceIdsOfInstance instanceId
                    Assert.Equal<Set<InvoiceId>>(before, after)
                    Assert.Equal(1, after.Count)
                })

    // =========================================================================
    // REQ-CF-5.17 – 5.19 — cancelling an Invoice
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-5.17 cancelling one Invoice of an Instance stores that Invoice as cancelled with its reason note and leaves the Instance and its other Invoices uncancelled`` () =
        let note = "Forgiven tenant charge"
        committed (twoInvoiceInstance "CF-5.17 one invoice" [])
            (fun (instanceId, invoiceIds) ->
                result {
                    let! _ = cancelInvoiceRoute note invoiceIds[0]
                    let! cancelledNote = noteOfInvoice invoiceIds[0]
                    let! otherNote = noteOfInvoice invoiceIds[1]
                    let! instanceNote = noteOfInstance instanceId
                    Assert.Equal(Some note, cancelledNote)
                    Assert.Equal(None, otherNote)
                    Assert.Equal(None, instanceNote)
                })

    [<Fact>]
    member _.``REQ-CF-5.18 cancelling an Invoice that has a Payment is rejected with a typed error naming the Invoice, and the Invoice reads back uncancelled`` () =
        committed (twoInvoiceInstance "CF-5.18 paid" [ 40.00M ])
            (fun (_, invoiceIds) ->
                result {
                    do!
                        match cancelInvoiceRoute "Settled outside the imported accounts" invoiceIds[0] with
                        | Error (AsError (CashFlowError.CashflowInvoiceCancellationBlockedByPayments uuid)) ->
                            Assert.Equal(invoiceIds[0] |> InvoiceId.value, uuid)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! stored = noteOfInvoice invoiceIds[0]
                    Assert.Equal(None, stored)
                })

    [<Fact>]
    member _.``REQ-CF-5.19 a second cancellation of a cancelled Invoice, with a different reason note, is rejected with a typed error and the Invoice keeps its original note`` () =
        let original = "Billed in error"
        committed (twoInvoiceInstance "CF-5.19 twice" [])
            (fun (_, invoiceIds) ->
                result {
                    let! _ = cancelInvoiceRoute original invoiceIds[0]
                    do!
                        match cancelInvoiceRoute "A different reason" invoiceIds[0] with
                        | Error (AsError (CashFlowError.CashflowInvoiceCancelled uuid)) ->
                            Assert.Equal(invoiceIds[0] |> InvoiceId.value, uuid)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! stored = noteOfInvoice invoiceIds[0]
                    Assert.Equal(Some original, stored)
                })

    [<Fact>]
    member _.``REQ-CF-5.19 updating a cancelled Invoice is rejected with a typed error and every field of the Invoice reads back unchanged`` () =
        committed (twoInvoiceInstance "CF-5.19 update" [])
            (fun (_, invoiceIds) ->
                result {
                    let invoiceId = invoiceIds[0]
                    let! _ = cancelInvoiceRoute "Billed in error" invoiceId
                    let! before = invoiceId |> Invoice.fetchById (fresh ())
                    let input : Contracts.UpdateInvoiceInput =
                        { invoiceId = invoiceId |> InvoiceId.value
                          externalInvoiceIdUpdate = SetTo(Some "CF-5.19 external")
                          invoiceDateUpdate = NoChange
                          dueDateUpdate = NoChange
                          amountUpdate = SetTo 250.00M
                          invoiceStateUpdate = NoChange
                          blockerUpdate = NoChange
                          memoUpdate = SetTo(Some "CF-5.19 memo") }
                    do!
                        match routeWith "UpdateInvoice" input with
                        | Error (AsError (CashFlowError.CashflowInvoiceCancelled uuid)) ->
                            Assert.Equal(invoiceId |> InvoiceId.value, uuid)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! after = invoiceId |> Invoice.fetchById (fresh ())
                    Assert.Equal(before, after)
                })

    [<Fact>]
    member _.``REQ-CF-5.19 adding a Payment to a cancelled Invoice is rejected with a typed error and no Payment is stored`` () =
        committed
            (fun s ->
                result {
                    let! agreementId, (_, invoiceIds) = twoInvoiceInstance "CF-5.19 payment" [] s
                    let! line = s.ledgerLine 100.00M
                    return agreementId, (invoiceIds[0], line)
                })
            (fun (invoiceId, line) ->
                result {
                    let! _ = cancelInvoiceRoute "Billed in error" invoiceId
                    do!
                        match createPaymentRoute invoiceId line with
                        | Error (AsError (CashFlowError.CashflowInvoiceCancelled uuid)) ->
                            Assert.Equal(invoiceId |> InvoiceId.value, uuid)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! payments = [ invoiceId ] |> Payment.fetchByInvoiceIdList (fresh ())
                    Assert.Empty(payments)
                })

    [<Fact>]
    member _.``REQ-CF-5.19 REQ-CF-5.16 adding an Invoice for a Payment Agreement whose Invoice on the same Instance is cancelled is rejected with a typed error and no second Invoice is stored`` () =
        committed
            (fun s ->
                result {
                    let! agreementId, legIds, legNames = s.agreement $"CF-5.19 slot {Guid.NewGuid()}" Outgo 1
                    let! instanceId, invoiceIds = s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, []) ]
                    return agreementId, (instanceId, invoiceIds[0], legIds[0], legNames[0], s.firstOfThisMonth)
                })
            (fun (instanceId, invoiceId, legId, legName, date) ->
                result {
                    let! _ = cancelInvoiceRoute "Billed in error" invoiceId
                    do!
                        match createInvoiceRoute instanceId legName date with
                        | Error (AsError (CashFlowError.CashflowInstanceManyInvoicesForPaymentAgreement(instanceUuid, legUuid, count))) ->
                            Assert.Equal(instanceId |> InstanceId.value, instanceUuid)
                            Assert.Equal(legId |> PaymentAgreementId.value, legUuid)
                            Assert.Equal(2, count)
                            Ok ()
                        | Error e -> wrongError e
                        | Ok _ -> unexpectedOk ()
                    let! stored = invoiceIdsOfInstance instanceId
                    Assert.Equal<Set<InvoiceId>>(Set.singleton invoiceId, stored)
                })

    // =========================================================================
    // REQ-CF-4.9, REQ-CF-9.10 — fulfilment with cancelled Invoices
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-4.9 REQ-CF-9.10 an Instance with one Invoice cancelled becomes fulfilled when a Payment brings its other Invoice to FullyPaid`` () =
        committed
            (fun s ->
                result {
                    let! agreementId, (instanceId, invoiceIds) = twoInvoiceInstance "CF-4.9 one cancelled" [] s
                    let! line = s.ledgerLine 100.00M
                    return agreementId, (instanceId, invoiceIds, line)
                })
            (fun (instanceId, invoiceIds, line) ->
                result {
                    let! _ = cancelInvoiceRoute "Variable leg, nothing billed" invoiceIds[1]
                    let! beforePayment = instanceId |> Instance.fetchById (fresh ())
                    Assert.False(beforePayment |> Instance.isFulfilled)
                    let! _ = createPaymentRoute invoiceIds[0] line
                    let! paid = invoiceIds[0] |> Invoice.fetchById (fresh ())
                    let! afterPayment = instanceId |> Instance.fetchById (fresh ())
                    Assert.Equal(FullyPaid, paid |> Invoice.invoiceLifeCycleState |> InvoiceLifeCycleState.paymentState)
                    Assert.True(afterPayment |> Instance.isFulfilled)
                })

    [<Fact>]
    member _.``REQ-CF-4.9 REQ-CF-9.10 an Instance whose every Invoice is cancelled, with none FullyPaid, reads back not fulfilled`` () =
        committed (twoInvoiceInstance "CF-4.9 all cancelled" [])
            (fun (instanceId, invoiceIds) ->
                result {
                    let! _ = cancelInvoiceRoute "Billed in error" invoiceIds[0]
                    let! _ = cancelInvoiceRoute "Billed in error" invoiceIds[1]
                    let! invoiceNotes = invoiceIds |> List.map noteOfInvoice |> convertListOfResultsToResultsList
                    let! stored = instanceId |> Instance.fetchById (fresh ())
                    Assert.Equal<string option list>([ Some "Billed in error"; Some "Billed in error" ], invoiceNotes)
                    Assert.False(stored |> Instance.isFulfilled)
                })

    // =========================================================================
    // REQ-CF-4.14 — what an open Instance is, in every list of open Instances
    // =========================================================================

    (* One agreement with three Instances made before the operation runs: last-but-one month's is fulfilled (its one
       Invoice paid in full), last month's is cancelled, this month's is neither. *)
    member private _.threeInstances (s: Scenario) (label: string) =
        result {
            let! agreementId, legIds, _ = s.agreement $"{label} {Guid.NewGuid()}" Outgo 1
            let! fulfilledId, _ = s.instance agreementId (s.firstOfMonthsAgo 2) [ (legIds[0], 100.00M, [ 100.00M ]) ]
            let! cancelledId, _ = s.instance agreementId (s.firstOfMonthsAgo 1) [ (legIds[0], 100.00M, []) ]
            let! openId, _ = s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, []) ]
            let! _ = s.cancelInstance "Never billed" cancelledId
            let! fulfilled = fulfilledId |> Instance.fetchById s.Context
            Assert.True(fulfilled |> Instance.isFulfilled)
            return fulfilledId, cancelledId, openId
        }

    [<Fact>]
    member this.``REQ-CF-4.14 REQ-CF-7.14 REQ-CF-14.10 the projection sweep's open Instances include an uncancelled, unfulfilled Instance created before the sweep ran, and exclude a cancelled one and a fulfilled one`` () =
        rolledBack (fun s ->
            result {
                let! fulfilledId, cancelledId, openId = this.threeInstances s "CF-7.14 sweep"
                let! horizon = 1 |> ProjectionHorizonInDays.create
                let! returned = horizon |> CashFlowOps.createUpcomingInstances s.Context
                let listed = returned |> instanceIdsOf
                Assert.Contains(openId, listed)
                Assert.DoesNotContain(cancelledId, listed)
                Assert.DoesNotContain(fulfilledId, listed)
            })

    [<Fact>]
    member this.``REQ-CF-4.14 REQ-CF-13.9 REQ-CF-14.10 the open Instances returned by linkage and matching include an uncancelled, unfulfilled Instance and exclude a cancelled one and a fulfilled one`` () =
        rolledBack (fun s ->
            result {
                let! fulfilledId, cancelledId, openId = this.threeInstances s "CF-13.9 matching"
                let! run = ClassificationOrchestration.classifyPaymentAgreements s.Context
                let listed = run.openInstances |> instanceIdsOf
                Assert.Contains(openId, listed)
                Assert.DoesNotContain(cancelledId, listed)
                Assert.DoesNotContain(fulfilledId, listed)
            })

    // =========================================================================
    // REQ-CF-8.2 – 8.4, REQ-CF-14.10 — the projection
    // =========================================================================

    (* The difference between a projection taken before the agreement exists and one taken after its Instance is made
       and one of its Invoices cancelled is what the agreement contributes. The cancelled Invoice is 250.00 and the
       uncancelled one 100.00, so the difference says which of them counted. *)
    member private _.projectedContribution (s: Scenario) (label: string) (direction: FlowDirection) =
        result {
            let! before = s.projection 30
            let! agreementId, legIds, _ = s.agreement $"{label} {Guid.NewGuid()}" direction 2
            let! _, invoiceIds =
                s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, []); (legIds[1], 250.00M, []) ]
            let! _ = s.cancelInvoice "Forgiven" invoiceIds[1]
            let! after = s.projection 30
            let cashOf (projection: CashFlowProjection) =
                projection.accounts |> List.find (fun a -> a.accountId = s.cashId)
            return cashOf before, cashOf after, invoiceIds
        }

    [<Fact>]
    member this.``REQ-CF-8.2 REQ-CF-14.10 a cancelled Income Invoice adds nothing to its debit account's known inflows, while an uncancelled Invoice on the same Instance adds its outstanding amount`` () =
        rolledBack (fun s ->
            result {
                let! before, after, invoiceIds = this.projectedContribution s "CF-8.2 cancelled" Income
                let! added = Money.subtractVal1FromVal2 before.knownInflows after.knownInflows
                let! hundred = Money.fromDecimal 100.00M
                Assert.Equal(hundred, added)
                let listed = after.invoices |> List.map (fun i -> i.invoiceId)
                Assert.Contains(invoiceIds[0], listed)
                Assert.DoesNotContain(invoiceIds[1], listed)
            })

    [<Fact>]
    member this.``REQ-CF-8.3 REQ-CF-14.10 a cancelled Outgo Invoice adds nothing to its credit account's known outflows, while an uncancelled Invoice on the same Instance adds its outstanding amount`` () =
        rolledBack (fun s ->
            result {
                let! before, after, invoiceIds = this.projectedContribution s "CF-8.3 cancelled" Outgo
                let! added = Money.subtractVal1FromVal2 before.knownOutflows after.knownOutflows
                let! hundred = Money.fromDecimal 100.00M
                Assert.Equal(hundred, added)
                let listed = after.invoices |> List.map (fun i -> i.invoiceId)
                Assert.Contains(invoiceIds[0], listed)
                Assert.DoesNotContain(invoiceIds[1], listed)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 REQ-CF-14.10 on an open Instance, a Payment Agreement whose Invoice is cancelled is not a bill to chase while a sibling Payment Agreement with no Invoice is`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, legNames = s.agreement $"CF-8.4 cancelled invoice {Guid.NewGuid()}" Outgo 2
                let! instanceId, invoiceIds = s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, []) ]
                let! _ = s.cancelInvoice "Billed in error" invoiceIds[0]
                let! projection = s.projection 30
                let bills =
                    projection.billsToChase
                    |> List.filter (fun b -> b.instanceId = instanceId)
                    |> List.map (fun b -> b.paymentAgreementName |> PaymentAgreementName.value)
                Assert.Equal<string list>([ legNames[1] ], bills)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 REQ-CF-14.10 a Payment Agreement with no Invoice is reported as a bill to chase on an open Instance and not on a cancelled Instance of the same agreement`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, _, legNames = s.agreement $"CF-8.4 cancelled instance {Guid.NewGuid()}" Outgo 1
                let! cancelledId, _ = s.instance agreementId (s.firstOfMonthsAgo 1) []
                let! openId, _ = s.instance agreementId s.firstOfThisMonth []
                let! _ = s.cancelInstance "Spawned by mistake" cancelledId
                let! projection = s.projection 30
                let bills =
                    projection.billsToChase
                    |> List.filter (fun b -> b.instanceId = cancelledId || b.instanceId = openId)
                    |> List.map (fun b -> b.instanceId, (b.paymentAgreementName |> PaymentAgreementName.value))
                Assert.Equal<(InstanceId * string) list>([ (openId, legNames[0]) ], bills)
            })

    // =========================================================================
    // REQ-CF-13.1, REQ-CF-14.10 — invoice matching
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.1 REQ-CF-14.10 a linked line whose date only a cancelled Invoice covers gets no Payment and fails the run as an orphan with the no-open-Invoice reason`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement $"CF-13.1 cancelled {Guid.NewGuid()}" Outgo 1
                let! _, invoiceIds = s.instance agreementId s.firstOfThisMonth [ (legIds[0], 100.00M, []) ]
                let! _ = s.cancelInvoice "Settled outside the imported accounts" invoiceIds[0]
                let! lineId = s.linkedLine legIds[0] s.firstOfThisMonth
                do!
                    match ClassificationOrchestration.classifyPaymentAgreements s.Context with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementLinksOrphaned orphans)) ->
                        Assert.Equal<(Guid * Guid * CashFlowError.OrphanedLineReason) list>(
                            [ (lineId |> StageEntryLineId.value, legIds[0] |> PaymentAgreementId.value, CashFlowError.NoOpenInvoiceCoversDate) ],
                            orphans)
                        Ok ()
                    | Error e -> wrongError e
                    | Ok _ -> unexpectedOk ()
                let! payments = [ lineId ] |> Payment.fetchByStageEntryLineIdList s.Context
                let! onInvoice = invoiceIds |> Payment.fetchByInvoiceIdList s.Context
                Assert.Empty(payments)
                Assert.Empty(onInvoice)
            })

    // Placeholders committed before the Src was read (audit 2026-10-04a remediation)

    [<Fact>]
    member _.``REQ-CF-4.12 cancelling an Instance one of whose Invoices is already cancelled leaves that Invoice's own reason note, while every other Invoice becomes cancelled carrying the Instance's note`` () =
        Assert.Fail "Not yet implemented"
