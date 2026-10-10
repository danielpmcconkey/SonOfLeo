module Business.FinancialServices.Positions.AccountSnapshotHeader

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type AccountSnapshotHeader = private {
    accountSnapshotId: AccountSnapshotId
    investmentAccountId: InvestmentAccountId
    snapshotDate: LocalDate
    provenance: Provenance
    contributionBasis: ContributionBasis option
    createdAt: Instant
    modifiedAt: Instant
}

type AccountSnapshotHeaderFieldUpdates = {
    accountSnapshotIdToUpdate: AccountSnapshotId
    provenanceUpdate: FieldUpdate<Provenance>
    contributionBasisUpdate: FieldUpdate<ContributionBasis option>
}

let accountSnapshotId h = h.accountSnapshotId
let investmentAccountId h = h.investmentAccountId
let snapshotDate h = h.snapshotDate
let provenance h = h.provenance
let contributionBasis h = h.contributionBasis
let createdAt h = h.createdAt
let modifiedAt h = h.modifiedAt

let create
    (accountSnapshotId: AccountSnapshotId)
    (investmentAccountId: InvestmentAccountId)
    (snapshotDate: LocalDate)
    (provenance: Provenance)
    (contributionBasis: ContributionBasis option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : AccountSnapshotHeader =
    { accountSnapshotId = accountSnapshotId
      investmentAccountId = investmentAccountId
      snapshotDate = snapshotDate
      provenance = provenance
      contributionBasis = contributionBasis
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private contributionBasisParameter (contributionBasis: ContributionBasis option) =
    { name = "@contribution_basis"
      value = NullableNumeric(contributionBasis |> Option.map (ContributionBasis.value >> Money.amount)) }

let persist (context: Context.Context) (header: AccountSnapshotHeader) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.account_snapshot(
            unique_id, investment_account_id, snapshot_date, provenance, contribution_basis, created_at, modified_at)
        values (
            @unique_id, @investment_account_id, @snapshot_date, @provenance, @contribution_basis, @created_at,
            @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(header.accountSnapshotId |> AccountSnapshotId.value) }
          { name = "@investment_account_id"; value = UniqueId(header.investmentAccountId |> InvestmentAccountId.value) }
          { name = "@snapshot_date"; value = DbLocalDate header.snapshotDate }
          { name = "@provenance"; value = CharString(header.provenance |> Provenance.toString) }
          contributionBasisParameter header.contributionBasis
          { name = "@created_at"; value = DbInstant header.createdAt }
          { name = "@modified_at"; value = DbInstant header.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let delete (context: Context.Context) (accountSnapshotId: AccountSnapshotId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.account_snapshot
        where unique_id = @unique_id;"""
    let parameters = [ { name = "@unique_id"; value = UniqueId(accountSnapshotId |> AccountSnapshotId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, accountId, snapshotDate, provenanceStr, contributionBasisRaw, createdAt, modifiedAt = raw
        let! provenance = provenanceStr |> Provenance.fromString
        let! contributionBasis =
            contributionBasisRaw
            |> convertOptionToDesiredTypeWithFallibleConverter (fun d ->
                d |> Money.fromDecimal |> Result.bind ContributionBasis.create)
        return
            create
                (uuid |> AccountSnapshotId.fromGuid)
                (accountId |> InvestmentAccountId.fromGuid)
                snapshotDate
                provenance
                contributionBasis
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "investment_account_id"),
    (row |> RowReader.getDate "snapshot_date"),
    (row |> RowReader.getString "provenance"),
    (row |> RowReader.getNumericOption "contribution_basis"),
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
    : Result<AccountSnapshotHeader list, IAppError> =
    let from = "positions.account_snapshot snap"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<AccountSnapshotHeader list, IAppError> =
    let select =
        """
        snap.unique_id, snap.investment_account_id, snap.snapshot_date, snap.provenance, snap.contribution_basis,
        snap.created_at, snap.modified_at"""
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (accountSnapshotId: AccountSnapshotId) : Result<AccountSnapshotHeader, IAppError> =
    let uuid = accountSnapshotId |> AccountSnapshotId.value
    fetchAny context (Some "snap.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsAccountSnapshotIdDoesntExist uuid)
    |> Result.map List.head

let private accountParameter (investmentAccountId: InvestmentAccountId) =
    { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) }

let fetchByInvestmentAccount
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    : Result<AccountSnapshotHeader list, IAppError> =
    fetchAny
        context
        (Some "snap.investment_account_id = @investment_account_id")
        [ accountParameter investmentAccountId ]
        AnyQuantityIsAcceptable
    |> Result.map (List.sortBy (fun h -> h.snapshotDate))

/// The account's snapshots any of whose lines carries a lot, in date order.
let fetchByInvestmentAccountCarryingLots
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    : Result<AccountSnapshotHeader list, IAppError> =
    let predicate =
        """
        snap.investment_account_id = @investment_account_id
        and exists (
            select 1
            from positions.account_snapshot_line snapln
            join positions.account_snapshot_lot snaplt on snaplt.account_snapshot_line_id = snapln.unique_id
            where snapln.account_snapshot_id = snap.unique_id)"""
    fetchAny context (Some predicate) [ accountParameter investmentAccountId ] AnyQuantityIsAcceptable
    |> Result.map (List.sortBy (fun h -> h.snapshotDate))

let fetchByInvestmentAccountAndDate
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (snapshotDate: LocalDate)
    : Result<AccountSnapshotHeader option, IAppError> =
    fetchAny
        context
        (Some "snap.investment_account_id = @investment_account_id and snap.snapshot_date = @snapshot_date")
        [ accountParameter investmentAccountId; { name = "@snapshot_date"; value = DbLocalDate snapshotDate } ]
        AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let fetchByInvestmentAccountBetween
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<AccountSnapshotHeader list, IAppError> =
    fetchAny
        context
        (Some
            "snap.investment_account_id = @investment_account_id and snap.snapshot_date between @begin_date and @end_date")
        [ accountParameter investmentAccountId
          { name = "@begin_date"; value = DbLocalDate beginDate }
          { name = "@end_date"; value = DbLocalDate endDate } ]
        AnyQuantityIsAcceptable
    |> Result.map (List.sortBy (fun h -> h.snapshotDate))

let update (context: Context.Context) (fieldUpdates: AccountSnapshotHeaderFieldUpdates) : Result<AccountSnapshotHeader, IAppError> =
    let uuid = fieldUpdates.accountSnapshotIdToUpdate |> AccountSnapshotId.value
    let updates =
        [
           fieldUpdates.provenanceUpdate
           |> mapNoChangeToOptionWithConversion (fun v ->
               ("provenance = @provenance", { name = "@provenance"; value = CharString(Provenance.toString v) }))
           fieldUpdates.contributionBasisUpdate
           |> mapNoChangeToOptionWithConversion (fun v ->
               ("contribution_basis = @contribution_basis", { name = "@contribution_basis"; value = NullableNumeric(v |> Option.map (ContributionBasis.value >> Money.amount)) })) ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (updates |> List.map snd)
    let queryStatement =
        $"""
        update positions.account_snapshot
        set {setClauses}, modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do! if updates |> List.isEmpty then error PositionsAccountSnapshotUpdateNoOp else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsAccountSnapshotIdDoesntExist uuid)
        return! fieldUpdates.accountSnapshotIdToUpdate |> fetchById context
    }
