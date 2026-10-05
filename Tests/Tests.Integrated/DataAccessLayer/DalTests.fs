module Tests.Integrated.DataAccessLayer.DalTests

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteScalar
open App.Operation.AuditEnvelope
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open App.DataAccessLayer.DalError


let errorRowCount ()
    : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    let mapRaw _ = ("", "")
    let contructFromRaw _ = Ok ""
    // two rows where exactly one was required. Zero rows is a different fact, DalNoOp, pinned below
    match executeReaderQuery (context |> Context.getDatabaseTransaction) "select 'a' union all select 'b';" [] mapRaw contructFromRaw ExactlyOne with
    | Ok _ -> Ok ()
    | Error e -> Error e
    
[<Fact>]
let ``REQ-DAL-2.2 a read requiring exactly one row that finds two returns DalResultantRowsDidntMatchExpectation carrying the expectation and the count`` () =
    match errorRowCount() with
    | Error (AsError (App.DataAccessLayer.DalError.DalResultantRowsDidntMatchExpectation (expectation, actual))) ->
        Assert.Equal("ExactlyOne", expectation)
        Assert.Equal(2, actual)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

(* Zero rows where rows were required is one fact whether the statement read or wrote: DalNoOp. It is a backstop; the
   caller that knows what the empty result means swaps it for a domain error with whenNoRows. Any other wrong count is
   DalResultantRowsDidntMatchExpectation, above. *)
[<Fact>]
let ``REQ-DAL-2.2 a read requiring exactly one row that finds none returns DalNoOp`` () =
    let context = Context.create NoTransaction FetchOnly
    let mapRaw _ = ("", "")
    let contructFromRaw _ = Ok ""
    isCorrectErrorEmpty
        (executeReaderQuery (context |> Context.getDatabaseTransaction) "select 1 where 1 = 2;" [] mapRaw contructFromRaw ExactlyOne)
        (App.DataAccessLayer.DalError.DalNoOp ("", 0))
        None
    |> railroadWrapper

[<Fact>]
let ``REQ-DAL-2.2 an update requiring exactly one row that touches none returns DalNoOp`` () =
    let context = Context.create NoTransaction FetchOnly
    isCorrectErrorEmpty
        (executeNonQuery (context |> Context.getDatabaseTransaction) "update ledger.account set code = code where 1 = 2;" [] ExactlyOne)
        (App.DataAccessLayer.DalError.DalNoOp ("", 0))
        None
    |> railroadWrapper

[<Fact>]
let ``REQ-DAL-2.2 whenNoRows swaps DalNoOp for the caller's domain error and passes every other error through`` () =
    let specific = TestingError "the specific error"
    let noRows : Result<unit, IAppError> = App.DataAccessLayer.DalError.error (App.DataAccessLayer.DalError.DalNoOp ("ExactlyOne", 0))
    let wrongCount : Result<unit, IAppError> =
        App.DataAccessLayer.DalError.error (App.DataAccessLayer.DalError.DalResultantRowsDidntMatchExpectation ("ExactlyOne", 2))
    match noRows |> App.DataAccessLayer.DalError.whenNoRows specific with
    | Error (AsError (TestingError message)) -> Assert.Equal("the specific error", message)
    | other -> Assert.Fail $"Expected the specific error; got {other}"
    match wrongCount |> App.DataAccessLayer.DalError.whenNoRows specific with
    | Error (AsError (App.DataAccessLayer.DalError.DalResultantRowsDidntMatchExpectation (_, actual))) -> Assert.Equal(2, actual)
    | other -> Assert.Fail $"Expected the wrong-count error to pass through; got {other}"

(* REQ-DAL-2.4 is read from two places. The server says whether a transaction was left open: a session that ran a
   statement and never ended its transaction sits in pg_stat_activity as 'idle in transaction'. ConnectionPool says
   whether the connection came back to the pool. Tests run one at a time, so nothing else holds a connection while
   these read. *)

let private connectionsInUse = Tests.Integrated.ConnectionPool.connectionsInUse

let private poolMaximum () : int64 =
    Tests.Integrated.ConnectionPool.maximumConnections ()
    |> Option.defaultWith (fun () -> failwith "no connection pool has been built yet")

let private sessionsIdleInTransaction () : Result<int64, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    executeScalar
        (context |> Context.getDatabaseTransaction)
        """
        select count(*) from pg_stat_activity
        where datname = current_database() and state = 'idle in transaction' and pid <> pg_backend_pid()
        """
        []
        longUnboxing

// A fiscal period at a sentinel month stands in for any write an operation makes before it fails. It exists afterwards
// only if the transaction committed.
let private probeKey = "2071-04"

let private probeRecordExists () : Result<bool, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match FiscalPeriod.fetchIdByKey context probeKey with
    | Ok _ -> Ok true
    | Error (AsError (LedgerError.FiscalPeriodNoPeriodMatchingKey _)) -> Ok false
    | Error e -> Error e

let private writeProbeRecord (context: Context.Context) : Result<unit, IAppError> =
    probeKey
    |> FiscalPeriodComponent.FiscalPeriodKey.fromString
    |> Result.bind (FiscalPeriodCreation.constructNewAndPersist context)
    |> Result.map ignore

let private failWithTypedError (context: Context.Context) : Result<unit, IAppError> =
    result {
        do! writeProbeRecord context
        return! Error (TestingError "typed failure mid-transaction")
    }

let private failByThrowing (context: Context.Context) : Result<unit, IAppError> =
    result {
        do! writeProbeRecord context
        return failwith "thrown mid-transaction"
    }

let private confirmReleased (inUseBefore: int64) : Result<unit, IAppError> =
    result {
        let! idle = sessionsIdleInTransaction()
        Assert.Equal(0L, idle)
        Assert.Equal(inUseBefore, connectionsInUse())
        let! probeExists = probeRecordExists()
        Assert.False(probeExists, "the probe fiscal period exists, so the transaction committed instead of rolling back")
    }

(* The caches are process-wide and never invalidated, so these run inside the shared fixture's collection: a cache
   loaded before the fixture truncates and restages the tables would hand later tests the previous run's IDs. *)
[<Collection("SharedTestData")>]
type ConnectionReleaseTests(fixture: Tests.Helpers.TestDataFixture) =

    [<Fact>]
    member _.``REQ-DAL-2.4 fetching through every lookup cache leaves no session idle in a transaction and the pool's in-use count where it started`` () =
        let inUseBefore = connectionsInUse()
        // each cache loads in full on its first fetch; the keys need not exist
        let noTransaction = Context.create NoTransaction FetchOnly |> Context.getDatabaseTransaction
        let missingName = "REQ-DAL-2.4 no such key"
        let missingId = Guid.NewGuid()
        let fetches : Result<unit, IAppError> list =
            [ Business.FinancialServices.Ledger.Account.codeToId.fetch noTransaction missingName |> Result.map ignore
              Business.FinancialServices.Ledger.Account.idToCode.fetch noTransaction missingId |> Result.map ignore
              Business.FinancialServices.Ledger.Account.idToName.fetch noTransaction missingId |> Result.map ignore
              Business.FinancialServices.Ledger.FiscalPeriod.keyToId.fetch noTransaction missingName |> Result.map ignore
              Business.FinancialServices.Ledger.FiscalPeriod.idToKey.fetch noTransaction missingId |> Result.map ignore
              Business.FinancialServices.CashFlow.MasterAgreement.nameToId.fetch noTransaction missingName |> Result.map ignore
              Business.FinancialServices.CashFlow.MasterAgreement.idToName.fetch noTransaction missingId |> Result.map ignore
              Business.FinancialServices.CashFlow.PaymentAgreement.nameToId.fetch noTransaction missingName |> Result.map ignore
              Business.FinancialServices.CashFlow.PaymentAgreement.idToName.fetch noTransaction missingId |> Result.map ignore ]
        // a missing key is DalNoOp from the single-row read that follows the load; anything else means the load failed
        for fetched in fetches do
            match fetched with
            | Error (AsError (DalNoOp _)) -> ()
            | other -> Assert.Fail $"Expected the cache to load and then miss; got {other}"
        result {
            let! idle = sessionsIdleInTransaction()
            Assert.Equal(0L, idle)
            Assert.Equal(inUseBefore, connectionsInUse())
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-DAL-2.4 an operation that succeeds commits its write, leaves no session idle in a transaction, and returns its connection to the pool`` () =
        let inUseBefore = connectionsInUse()
        try
            result {
                let! existedBefore = probeRecordExists()
                Assert.False(existedBefore, "the probe fiscal period exists before the operation ran")
                do! Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoCompleteTransaction FiscalPeriodCreate writeProbeRecord
                let! idle = sessionsIdleInTransaction()
                Assert.Equal(0L, idle)
                Assert.Equal(inUseBefore, connectionsInUse())
                // probeRecordExists reads through a fresh context, outside the operation's closed transaction
                let! probeExists = probeRecordExists()
                Assert.True(probeExists, "the probe fiscal period is missing, so the operation's transaction did not commit")
            }
            |> railroadWrapper
        finally
            // no row to delete means the operation never wrote it; the assertions above have already said so
            match Tests.Helpers.Cleanup.cleanUpFiscalPeriodKey (Some probeKey) with
            | Ok ()
            | Error (AsError (DalNoOp _)) -> ()
            | Error e -> failwith (e.ToMessage())

    [<Fact>]
    member _.``REQ-DAL-2.4 an operation run under the rollback runner that throws mid-transaction rolls back, leaves no session idle in a transaction, and returns its connection to the pool`` () =
        let inUseBefore = connectionsInUse()
        // this runner re-raises after rolling back, rather than wrapping the exception as a typed error
        let thrown =
            try
                Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoRollback FiscalPeriodCreate failByThrowing |> ignore
                None
            with ex -> Some ex.Message
        Assert.Equal(Some "thrown mid-transaction", thrown)
        confirmReleased inUseBefore |> railroadWrapper

    [<Fact>]
    member _.``REQ-DAL-2.4 an operation that ends in a typed error mid-transaction rolls back, leaves no session idle in a transaction, and returns its connection to the pool`` () =
        let inUseBefore = connectionsInUse()
        match Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoCompleteTransaction FiscalPeriodCreate failWithTypedError with
        | Error (AsError (TestingError message)) -> Assert.Equal("typed failure mid-transaction", message)
        | other -> Assert.Fail $"Expected the operation's own typed error; got {other}"
        confirmReleased inUseBefore |> railroadWrapper

    [<Fact>]
    member _.``REQ-DAL-2.4 an operation that throws mid-transaction rolls back, leaves no session idle in a transaction, and returns its connection to the pool`` () =
        let inUseBefore = connectionsInUse()
        match Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoCompleteTransaction FiscalPeriodCreate failByThrowing with
        | Error (AsError (DalErrorDuringAutoCompleteTransactionRun ex)) -> Assert.Equal("thrown mid-transaction", ex.Message)
        | other -> Assert.Fail $"Expected the thrown exception wrapped as a typed error; got {other}"
        confirmReleased inUseBefore |> railroadWrapper

    [<Fact>]
    member _.``REQ-DAL-2.4 running more failing operations than the pool holds connections never exhausts the pool`` () =
        let inUseBefore = connectionsInUse()
        let operations = int (poolMaximum()) + 5
        for i in 1 .. operations do
            let operation = if i % 2 = 0 then failWithTypedError else failByThrowing
            match Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoCompleteTransaction FiscalPeriodCreate operation with
            | Error (AsError (TestingError _))
            | Error (AsError (DalErrorDuringAutoCompleteTransactionRun _)) -> ()
            | other -> Assert.Fail $"Operation {i} of {operations} should have failed with its own error; got {other}"
        confirmReleased inUseBefore |> railroadWrapper
