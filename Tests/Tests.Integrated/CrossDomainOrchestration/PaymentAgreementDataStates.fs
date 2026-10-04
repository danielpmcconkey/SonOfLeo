module Tests.Integrated.CrossDomainOrchestration.PaymentAgreementDataStates

open System
open System.Text.Json.Nodes
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
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
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Payment Agreements are created with their Master Agreement. Most tests here send a CreateAgreement payload through
   the route, the way the operator does, and read back from a fresh context: the route commits when it succeeds, and a
   refused create only shows that it stored nothing once its transaction is gone. Every agreement whose name a test uses
   is deleted in a finally. The tests that go below the route say why.

   Each agreement is Daily, Outgo, started 30 days ago. Its Payment Agreements sit on F-2230 and F-1280 unless a test
   says otherwise. Names carry a Guid so a leftover from a failed run never collides with the next one. *)

let private unique (label: string) = $"{label} {Guid.NewGuid():N}"

let private fresh () = Context.create NoTransaction FetchOnly

let private noFilter : AgreementFilter =
    { agreementIds = None; activeAgreementsOnly = false }

let private storedNamed (context: Context.Context) (name: string) =
    noFilter
    |> AgreementOrchestration.fetchFiltered context AnyQuantityIsAcceptable
    |> Result.map (List.filter (fun a ->
        a |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementName |> AgreementName.value = name))

/// The Payment Agreements of the one stored agreement with the name.
let private storedLegsOf (name: string) =
    storedNamed (fresh ()) name
    |> Result.map (List.exactlyOne >> AgreementOrchestration.paymentAgreements)

let private leg (name: string) : Contracts.CreatePaymentAgreementFieldsInput =
    { paymentAgreementName = name
      debitAccountCode = "F-2230"
      creditAccountCode = "F-1280"
      expectedAmount = Some 100.00M
      daysDueAfterInvoiceDate = Some 0
      memo = None }

let private createInput (name: string) (legs: Contracts.CreatePaymentAgreementFieldsInput list) : Contracts.CreateAgreementInput =
    { agreementName = name
      direction = "Outgo"
      cadence = { cadenceType = Contracts.Daily; nextInstance = Calendar.today () }
      counterparty = "Payment agreement data state test counterparty"
      activeBegin = Calendar.today().PlusDays(-30)
      activeEnd = None
      memo = None
      paymentAgreements = legs }

let private createRoute (payload: string) = routeUiCommandForTesting "CashFlow" "CreateAgreement" [] payload

/// Sends a CreateAgreement payload with the given legs. Returns the route's result, parsed.
let private create (name: string) (legs: Contracts.CreatePaymentAgreementFieldsInput list) =
    createInput name legs
    |> Json.toJson
    |> Result.bind createRoute
    |> Result.bind Json.fromJson<Contracts.AgreementReturn>

/// Runs the test, then deletes every stored agreement carrying one of the names, from a fresh context.
let private cleaningUp (names: string list) (test: unit -> Result<unit, IAppError>) =
    let cleanUpFailures = ResizeArray<string>()
    try
        test () |> railroadWrapper
    finally
        for name in names do
            match storedNamed (fresh ()) name with
            | Ok stored ->
                for a in stored do
                    let id = a |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID |> MasterAgreementId.value
                    match Cleanup.cleanUpMasterAgreementTree (Some id) with
                    | Ok () -> ()
                    | Error e -> cleanUpFailures.Add(e.ToMessage())
            | Error e -> cleanUpFailures.Add(e.ToMessage())
    Assert.Empty(cleanUpFailures)

/// Fails unless the attempt was refused with an error `isExpected` accepts, naming whatever came back instead.
let private expectRefusal (isExpected: IAppError -> bool) (attempt: Result<'a, IAppError>) =
    match attempt with
    | Error e when isExpected e -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

/// The database constraint violation, of the given SQL state, that the data access layer surfaces carrying the
/// constraint's name. Duplicate Payment Agreement names and an orphaned Payment Agreement have no typed error: only
/// the constraint enforces them, and the violation is loud.
let private constraintViolation (sqlState: string) (constraintName: string) (e: IAppError) =
    match e with
    | AsError (App.DataAccessLayer.DalError.DalErrorDuringNonQueryExecution(:? Npgsql.PostgresException as pg)) ->
        pg.SqlState = sqlState && pg.ConstraintName = constraintName
    | _ -> false

let private duplicateLegName = constraintViolation "23505" "payment_agreement_payment_agreement_name_key"

[<Collection("SharedTestData")>]
type PaymentAgreementDataStatesTests(fixture: TestDataFixture) =

    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    /// Sends the payload, failing unless it is refused with an error `isExpected` accepts. Returns what is stored
    /// under the name after.
    let refusedAndStored (isExpected: IAppError -> bool) (name: string) (legs: Contracts.CreatePaymentAgreementFieldsInput list) =
        result {
            let attempt = create name legs
            let! stored = storedNamed (fresh ()) name
            attempt |> expectRefusal isExpected
            return stored
        }

    // =========================================================================
    // REQ-CF-3.3, 3.4, 3.5, 3.11 — what a Payment Agreement references
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.3 a Payment Agreement created with its agreement is stored referencing that agreement's ID`` () =
        let name = unique "CF-3.3 references"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ leg (unique "CF-3.3 leg") ]
                let! stored = storedNamed (fresh ()) name
                let agreement = stored |> List.exactlyOne
                let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
                let! legs = [ agreementId ] |> PaymentAgreement.fetchByMasterAgreementIdList (fresh ())
                Assert.Equal<MasterAgreementId list>(
                    [ agreementId ], legs |> List.map PaymentAgreement.masterAgreementID)
            })

    (* Below the route, because no route creates a Payment Agreement apart from its agreement. The write runs in a
       committing transaction of its own, which it rolls back when it fails, so what it left can be read afterwards. *)
    [<Fact>]
    member _.``REQ-CF-3.3 writing a Payment Agreement at the model level whose Master Agreement ID names no stored agreement is refused by the master agreement foreign key and no Payment Agreement is stored`` () =
        let paymentAgreementId = PaymentAgreementId.create ()
        result {
            let attempt =
                runCommandRouteAndAutoCompleteTransaction CashFlowCreateAgreement (fun context ->
                    result {
                        let! name = unique "CF-3.3 orphan" |> PaymentAgreementName.create
                        let now = context |> Context.getInitiationInstant
                        return!
                            PaymentAgreement.create paymentAgreementId (MasterAgreementId.create ()) name
                                (DebitAccount.create(accountIdOf "F-2230")) (CreditAccount.create(accountIdOf "F-1280")) None None None now now
                            |> PaymentAgreement.persist context
                    })
            let! stored = PaymentAgreement.fetchByPaymentAgreementIdList (fresh ()) [ paymentAgreementId ]
            attempt |> expectRefusal (constraintViolation "23503" "payment_agreement_master_agreement_id_fkey")
            Assert.Empty(stored)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-3.4 REQ-CF-3.5 a Payment Agreement created with existing debit and credit accounts is stored referencing exactly those accounts on their respective sides`` () =
        let name = unique "CF-3.4 accounts"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.4 leg") with debitAccountCode = "F-1280"; creditAccountCode = "F-4290" } ]
                let! legs = storedLegsOf name
                let stored = legs |> List.exactlyOne
                Assert.Equal(DebitAccount.create(accountIdOf "F-1280"), stored |> PaymentAgreement.debitAccount)
                Assert.Equal(CreditAccount.create(accountIdOf "F-4290"), stored |> PaymentAgreement.creditAccount)
            })

    (* Below the route, because the route takes account codes and a code that names no account never becomes an ID.
       The refusal comes before anything is written, so the rolled-back transaction can still be read. *)
    [<Theory>]
    [<InlineData("Debit")>]
    [<InlineData("Credit")>]
    member _.``REQ-CF-3.4 REQ-CF-3.5 REQ-CF-3.11 for each of the debit side and the credit side, creating an agreement whose Payment Agreement names an account that does not exist on that side is rejected with a typed error naming that side, and no agreement is stored`` (side: string) =
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let name = unique $"CF-3.11 {side}"
                let missing = AccountId.fromGuid (Guid.NewGuid())
                let debit, credit =
                    if side = "Debit" then missing, accountIdOf "F-1280" else accountIdOf "F-2230", missing
                let! agreementName = name |> AgreementName.create
                let! counterparty = "Payment agreement data state test counterparty" |> Counterparty.create
                let! legName = unique "CF-3.11 leg" |> PaymentAgreementName.create
                let today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
                let! period = ActivityPeriod.create (today.PlusDays(-30)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
                let attempt =
                    AgreementOrchestration.constructNewAndPersist
                        context agreementName Outgo Cadence.Daily { nextInstance = today } counterparty period None
                        [ (legName, (DebitAccount.create debit), (CreditAccount.create credit), None, None, None) ]
                let missingUuid = missing |> AccountId.value
                let namesTheSide =
                    match side, attempt with
                    | "Debit", Error (AsError (CashFlowError.CashflowPaymentAgreementDebitAccountInvalid uuid)) -> uuid = missingUuid
                    | "Credit", Error (AsError (CashFlowError.CashflowPaymentAgreementCreditAccountInvalid uuid)) -> uuid = missingUuid
                    | _ -> false
                let! stored = storedNamed context name
                Assert.True(namesTheSide)
                Assert.Empty(stored)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-3.7 — expected amount
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.7 an agreement whose Payment Agreement has no expected amount is created and its Payment Agreement is stored with no expected amount`` () =
        let name = unique "CF-3.7 no amount"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.7 leg") with expectedAmount = None } ]
                let! legs = storedLegsOf name
                Assert.Equal(None, legs |> List.exactlyOne |> PaymentAgreement.expectedAmount)
            })

    [<Theory>]
    [<InlineData("0.00")>]
    [<InlineData("-0.01")>]
    member _.``REQ-CF-3.7 for each of 0.00 and -0.01, creating an agreement whose Payment Agreement has that expected amount is rejected with a typed error and no agreement is stored`` (amount: string) =
        let name = unique "CF-3.7 non-positive"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        (function
                         | AsError (CashFlowError.CashflowPaymentAgreementNonPositiveExpectedAmount(_, refused)) ->
                            refused = Decimal.Parse amount
                         | _ -> false)
                        name [ { leg (unique "CF-3.7 leg") with expectedAmount = Some(Decimal.Parse amount) } ]
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-3.7 an agreement whose Payment Agreement has an expected amount of 0.01 is stored with 0.01`` () =
        let name = unique "CF-3.7 one cent"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.7 leg") with expectedAmount = Some 0.01M } ]
                let! legs = storedLegsOf name
                Assert.Equal(Some 0.01M, legs |> List.exactlyOne |> PaymentAgreement.expectedAmount |> Option.map Money.amount)
            })

    [<Fact>]
    member _.``REQ-CF-3.7 creating an agreement whose Payment Agreement expected amount has a fraction of a cent is rejected with a typed error and no agreement is stored`` () =
        let name = unique "CF-3.7 fraction"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        (function
                         | AsError (BizFinServError.MoneyFailedToConvertImproperPrecision raw) -> raw = 100.005M
                         | _ -> false)
                        name [ { leg (unique "CF-3.7 leg") with expectedAmount = Some 100.005M } ]
                Assert.Empty(stored)
            })

    // =========================================================================
    // REQ-CF-3.8 — memo
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.8 a Payment Agreement with no memo is stored with no memo`` () =
        let name = unique "CF-3.8 no memo"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ leg (unique "CF-3.8 leg") ]
                let! legs = storedLegsOf name
                Assert.Equal(None, legs |> List.exactlyOne |> PaymentAgreement.memo)
            })

    [<Fact>]
    member _.``REQ-CF-3.8 a Payment Agreement memo of exactly 2000 characters is stored unchanged`` () =
        let name = unique "CF-3.8 long memo"
        let memo = String('m', 2000)
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.8 leg") with memo = Some memo } ]
                let! legs = storedLegsOf name
                Assert.Equal(Some memo, legs |> List.exactlyOne |> PaymentAgreement.memo |> Option.map PaymentAgreementMemo.value)
            })

    [<Fact>]
    member _.``REQ-CF-3.8 creating an agreement whose Payment Agreement memo is 2001 characters is rejected with a typed error and no agreement is stored`` () =
        let name = unique "CF-3.8 memo too long"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        (function
                         | AsError (CashFlowError.CashflowPaymentAgreementMemoTooLong(_, limit)) -> limit = 2000
                         | _ -> false)
                        name [ { leg (unique "CF-3.8 leg") with memo = Some(String('m', 2001)) } ]
                Assert.Empty(stored)
            })

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-3.8 for each of the empty string, a single space and a string of spaces and tabs, creating an agreement whose Payment Agreement memo is that string is rejected with a typed error and no agreement is stored`` (text: string) =
        let name = unique "CF-3.8 blank memo"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        (function
                         | AsError (CashFlowError.CashflowPaymentAgreementMemoIsEmpty _) -> true
                         | _ -> false)
                        name [ { leg (unique "CF-3.8 leg") with memo = Some text } ]
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-3.8 REQ-SYS-1.1 a Payment Agreement memo with leading and trailing spaces is stored and returned with them removed and its interior unchanged`` () =
        let name = unique "CF-3.8 trimmed memo"
        cleaningUp [ name ] (fun () ->
            result {
                let! returned = create name [ { leg (unique "CF-3.8 leg") with memo = Some " \t a  memo\twith  gaps  " } ]
                let! legs = storedLegsOf name
                Assert.Equal(Some "a  memo\twith  gaps", legs |> List.exactlyOne |> PaymentAgreement.memo |> Option.map PaymentAgreementMemo.value)
                Assert.Equal(Some "a  memo\twith  gaps", (returned.paymentAgreements |> List.exactlyOne).memo)
            })

    [<Fact>]
    member _.``REQ-CF-3.8 REQ-SYS-1.1 a Payment Agreement memo of 2000 characters padded with leading and trailing spaces is stored as the 2000 characters`` () =
        let name = unique "CF-3.8 padded memo"
        let memo = String('m', 2000)
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.8 leg") with memo = Some $"   {memo}   " } ]
                let! legs = storedLegsOf name
                Assert.Equal(Some memo, legs |> List.exactlyOne |> PaymentAgreement.memo |> Option.map PaymentAgreementMemo.value)
            })

    // =========================================================================
    // REQ-CF-3.9 — name
    // =========================================================================

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-3.9 for each of null, the empty string, a single space and a string of spaces and tabs, creating an agreement whose Payment Agreement name is that value is refused, the null as the payload is read and the others with a typed error, and no agreement is stored`` (text: string) =
        let name = unique "CF-3.9 blank name"
        cleaningUp [ name ] (fun () ->
            result {
                let! json = createInput name [ leg "placeholder" ] |> Json.toJson
                let node = JsonNode.Parse(json)
                node["paymentAgreements"].[0].["paymentAgreementName"] <- (if isNull text then null else JsonValue.Create(text) :> JsonNode)
                let attempt = createRoute (node.ToJsonString())
                let! stored = storedNamed (fresh ()) name
                attempt
                |> expectRefusal (fun e ->
                    match isNull text, e with
                    | true, AsError (UtilityError.JsonDeserializationFailed(typeName, _, _)) ->
                        typeName = typeof<Contracts.CreateAgreementInput>.ToString()
                    | false, AsError (CashFlowError.CashflowPaymentAgreementNameIsEmpty _) -> true
                    | _ -> false)
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 a Payment Agreement name of exactly 250 characters is stored with that name`` () =
        let name = unique "CF-3.9 long name"
        let legName = (unique "CF-3.9 leg").PadRight(250, 'x')
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ leg legName ]
                let! legs = storedLegsOf name
                Assert.Equal<string>(legName, legs |> List.exactlyOne |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 creating an agreement whose Payment Agreement name is 251 characters is rejected with a typed error and no agreement is stored`` () =
        let name = unique "CF-3.9 name too long"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        (function
                         | AsError (CashFlowError.CashflowPaymentAgreementNameTooLong(_, limit)) -> limit = 250
                         | _ -> false)
                        name [ leg ((unique "CF-3.9 leg").PadRight(251, 'x')) ]
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 REQ-SYS-1.1 a Payment Agreement name with leading and trailing spaces is stored and returned with them removed and its interior unchanged`` () =
        let name = unique "CF-3.9 trimmed name"
        let legName = unique "CF-3.9  leg\twith gaps"
        cleaningUp [ name ] (fun () ->
            result {
                let! returned = create name [ leg $" \t {legName}  " ]
                let! legs = storedLegsOf name
                Assert.Equal<string>(legName, legs |> List.exactlyOne |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value)
                Assert.Equal<string>(legName, (returned.paymentAgreements |> List.exactlyOne).paymentAgreementName)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 REQ-SYS-1.1 a Payment Agreement name of 250 characters padded with leading and trailing spaces is stored as the 250 characters`` () =
        let name = unique "CF-3.9 padded name"
        let legName = (unique "CF-3.9 leg").PadRight(250, 'x')
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ leg $"   {legName}   " ]
                let! legs = storedLegsOf name
                Assert.Equal<string>(legName, legs |> List.exactlyOne |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 creating an agreement whose Payment Agreement has the name of a Payment Agreement of another agreement is refused by the payment agreement name unique constraint, no second agreement is stored and the existing Payment Agreement is unchanged`` () =
        let first = unique "CF-3.9 first"
        let second = unique "CF-3.9 second"
        let legName = unique "CF-3.9 shared leg"
        cleaningUp [ first; second ] (fun () ->
            result {
                let! _ = create first [ leg legName ]
                let! before = storedLegsOf first
                let! stored = refusedAndStored duplicateLegName second [ { leg legName with expectedAmount = Some 5.00M } ]
                let! after = storedLegsOf first
                Assert.Empty(stored)
                Assert.Equal<PaymentAgreement.PaymentAgreement list>(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 REQ-SYS-1.1 creating an agreement whose Payment Agreement name differs from an existing Payment Agreement's name only by leading and trailing spaces is refused by the payment agreement name unique constraint and no agreement is stored`` () =
        let first = unique "CF-3.9 padded first"
        let second = unique "CF-3.9 padded second"
        let legName = unique "CF-3.9 padded shared leg"
        cleaningUp [ first; second ] (fun () ->
            result {
                let! _ = create first [ leg legName ]
                let! stored = refusedAndStored duplicateLegName second [ leg $"  {legName} " ]
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-3.9 creating an agreement with two Payment Agreements of the same name is refused by the payment agreement name unique constraint and no agreement is stored`` () =
        let name = unique "CF-3.9 twins"
        let legName = unique "CF-3.9 twin leg"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        duplicateLegName name
                        [ leg legName; { leg legName with debitAccountCode = "F-1280"; creditAccountCode = "F-4290" } ]
                Assert.Empty(stored)
            })

    // =========================================================================
    // REQ-CF-3.10 — days due
    // =========================================================================

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(365)>]
    member _.``REQ-CF-3.10 for each of 0 and 365, an agreement whose Payment Agreement has that days-due value is created and the Payment Agreement is stored with that value`` (days: int) =
        let name = unique "CF-3.10 in range"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.10 leg") with daysDueAfterInvoiceDate = Some days } ]
                let! legs = storedLegsOf name
                Assert.Equal(Some days, legs |> List.exactlyOne |> PaymentAgreement.daysDueAfterInvoiceDate |> Option.map DaysDueAfterInvoiceDate.value)
            })

    [<Theory>]
    [<InlineData(-1)>]
    [<InlineData(366)>]
    member _.``REQ-CF-3.10 for each of -1 and 366, creating an agreement whose Payment Agreement has that days-due value is rejected with a typed error and no agreement is stored`` (days: int) =
        let name = unique "CF-3.10 out of range"
        cleaningUp [ name ] (fun () ->
            result {
                let! stored =
                    refusedAndStored
                        (function
                         | AsError (CashFlowError.CashflowDaysDueAfterInvoiceDateBelowMin(refused, 0)) -> days = -1 && refused = days
                         | AsError (CashFlowError.CashflowDaysDueAfterInvoiceDateExceededMax(refused, 365)) -> days = 366 && refused = days
                         | _ -> false)
                        name [ { leg (unique "CF-3.10 leg") with daysDueAfterInvoiceDate = Some days } ]
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-3.10 an agreement whose Payment Agreement has no days-due value is created and its Payment Agreement is stored with none`` () =
        let name = unique "CF-3.10 none"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = create name [ { leg (unique "CF-3.10 leg") with daysDueAfterInvoiceDate = None } ]
                let! legs = storedLegsOf name
                Assert.Equal(None, legs |> List.exactlyOne |> PaymentAgreement.daysDueAfterInvoiceDate)
            })
