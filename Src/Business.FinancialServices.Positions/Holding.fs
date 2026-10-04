module Business.FinancialServices.Positions.Holding

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type Holding = private {
    holdingId: HoldingId
    investmentAccountId: InvestmentAccountId
    securityId: SecurityId
    basisMethod: BasisMethod option
    createdAt: Instant
    modifiedAt: Instant
}

let holdingId h = h.holdingId
let investmentAccountId h = h.investmentAccountId
let securityId h = h.securityId
let basisMethod h = h.basisMethod
let createdAt h = h.createdAt
let modifiedAt h = h.modifiedAt

let create
    (holdingId: HoldingId)
    (investmentAccountId: InvestmentAccountId)
    (securityId: SecurityId)
    (basisMethod: BasisMethod option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Holding =
    { holdingId = holdingId
      investmentAccountId = investmentAccountId
      securityId = securityId
      basisMethod = basisMethod
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (holding: Holding) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.holding(unique_id, investment_account_id, security_id, basis_method, created_at, modified_at)
        values (@unique_id, @investment_account_id, @security_id, @basis_method, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(holding.holdingId |> HoldingId.value) }
          { name = "@investment_account_id"; value = UniqueId(holding.investmentAccountId |> InvestmentAccountId.value) }
          { name = "@security_id"; value = UniqueId(holding.securityId |> SecurityId.value) }
          { name = "@basis_method"; value = NullableCharString(holding.basisMethod |> Option.map BasisMethod.toString) }
          { name = "@created_at"; value = DbInstant holding.createdAt }
          { name = "@modified_at"; value = DbInstant holding.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, accountId, securityId, basisMethodStr, createdAt, modifiedAt = raw
        let! basisMethod = basisMethodStr |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
        return
            create
                (uuid |> HoldingId.fromGuid)
                (accountId |> InvestmentAccountId.fromGuid)
                (securityId |> SecurityId.fromGuid)
                basisMethod
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "investment_account_id"),
    (row |> RowReader.getUuid "security_id"),
    (row |> RowReader.getStringOption "basis_method"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let query
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Holding list, IAppError> =
    let from = "positions.holding hol"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Holding list, IAppError> =
    let select =
        "hol.unique_id, hol.investment_account_id, hol.security_id, hol.basis_method, hol.created_at, hol.modified_at"
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (holdingId: HoldingId) : Result<Holding, IAppError> =
    let uuid = holdingId |> HoldingId.value
    fetchAny context (Some "hol.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsHoldingIdDoesntExist uuid)
    |> Result.map List.head

let fetchAll (context: Context.Context) : Result<Holding list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByInvestmentAccount
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    : Result<Holding list, IAppError> =
    let parameters =
        [ { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) } ]
    fetchAny context (Some "hol.investment_account_id = @investment_account_id") parameters AnyQuantityIsAcceptable

let fetchByInvestmentAccountAndSecurity
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (securityId: SecurityId)
    : Result<Holding option, IAppError> =
    let parameters =
        [ { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) }
          { name = "@security_id"; value = UniqueId(securityId |> SecurityId.value) } ]
    fetchAny
        context
        (Some "hol.investment_account_id = @investment_account_id and hol.security_id = @security_id")
        parameters
        AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let updateBasisMethod
    (context: Context.Context)
    (basisMethod: BasisMethod option)
    (holdingId: HoldingId)
    : Result<Holding, IAppError> =
    let uuid = holdingId |> HoldingId.value
    let queryStatement =
        """
        update positions.holding
        set basis_method = @basis_method, modified_at = @modified
        where unique_id = @unique_id;"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@basis_method"; value = NullableCharString(basisMethod |> Option.map BasisMethod.toString) }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
    result {
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsHoldingIdDoesntExist uuid)
        return! holdingId |> fetchById context
    }
