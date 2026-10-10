module Business.FinancialServices.Positions.InvestmentActivity

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// An event the institution reports in an Investment Account. The ordinal keeps the order an account's activities of
/// one date were supplied in; identical activities are separate events and are never merged.
type InvestmentActivity = private {
    investmentActivityId: InvestmentActivityId
    investmentAccountId: InvestmentAccountId
    activityDate: LocalDate
    ordinal: int
    kind: ActivityKind
    description: ActivityDescription
    source: ActivitySource option
    holdingId: HoldingId option
    quantity: Quantity.Quantity option
    price: Price.Price option
    amount: Money.Money
    createdAt: Instant
    modifiedAt: Instant
}

let investmentActivityId a = a.investmentActivityId
let investmentAccountId a = a.investmentAccountId
let activityDate a = a.activityDate
let ordinal a = a.ordinal
let kind a = a.kind
let description a = a.description
let source a = a.source
let holdingId a = a.holdingId
let quantity a = a.quantity
let price a = a.price
let amount a = a.amount
let createdAt a = a.createdAt
let modifiedAt a = a.modifiedAt

let create
    (investmentActivityId: InvestmentActivityId)
    (investmentAccountId: InvestmentAccountId)
    (activityDate: LocalDate)
    (ordinal: int)
    (kind: ActivityKind)
    (description: ActivityDescription)
    (source: ActivitySource option)
    (holdingId: HoldingId option)
    (quantity: Quantity.Quantity option)
    (price: Price.Price option)
    (amount: Money.Money)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : InvestmentActivity =
    { investmentActivityId = investmentActivityId
      investmentAccountId = investmentAccountId
      activityDate = activityDate
      ordinal = ordinal
      kind = kind
      description = description
      source = source
      holdingId = holdingId
      quantity = quantity
      price = price
      amount = amount
      createdAt = createdAt
      modifiedAt = modifiedAt }

// Purchases, sales, reinvestments and adjustments move units of a named Security; dividends, interest and capital-gain
// distributions move none; every other kind moves units exactly when it names a Security.
let private shapeProblem (kind: ActivityKind) (namesSecurity: bool) (carriesQuantity: bool) : ActivityShapeProblem option =
    match kind with
    | ActivityKind.Purchase
    | ActivityKind.Sale
    | ActivityKind.Reinvestment
    | ActivityKind.AdjustmentIn
    | ActivityKind.AdjustmentOut ->
        if not namesSecurity then Some SecurityMissing
        elif not carriesQuantity then Some QuantityMissing
        else None
    | ActivityKind.Dividend
    | ActivityKind.Interest
    | ActivityKind.CapitalGainDistribution -> if carriesQuantity then Some QuantityNotAllowed else None
    | ActivityKind.Contribution
    | ActivityKind.RolloverIn
    | ActivityKind.TransferIn
    | ActivityKind.Withdrawal
    | ActivityKind.RolloverOut
    | ActivityKind.TransferOut
    | ActivityKind.Fee ->
        match namesSecurity, carriesQuantity with
        | true, false -> Some QuantityMissing
        | false, true -> Some QuantityWithoutSecurity
        | _ -> None

/// Confirms everything about one activity that needs no read. The label only names the account in the error.
let confirmShape
    (accountLabel: string)
    (activityDate: LocalDate)
    (kind: ActivityKind)
    (namesSecurity: bool)
    (quantity: Quantity.Quantity option)
    (price: Price.Price option)
    (amount: Money.Money)
    : Result<unit, IAppError> =
    let kindName = kind |> ActivityKind.toString
    match quantity, price with
    | Some q, _ when not (q |> Quantity.isPositive) ->
        error (PositionsActivityQuantityNotPositive(accountLabel, activityDate, kindName))
    | None, Some _ -> error (PositionsActivityPriceWithoutQuantity(accountLabel, activityDate, kindName))
    | _ when amount |> Money.isNegative ->
        error (PositionsActivityAmountNegative(accountLabel, activityDate, kindName, amount |> Money.amount))
    | _ ->
        match shapeProblem kind namesSecurity (quantity |> Option.isSome) with
        | Some problem -> error (PositionsActivityShapeInvalid(accountLabel, activityDate, kindName, problem))
        | None -> Ok()

/// Confirms one account's range: its end not before its begin, nor after the current date.
let confirmRange
    (accountLabel: string)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (currentDate: LocalDate)
    : Result<unit, IAppError> =
    if endDate < beginDate then error (PositionsActivityRangeEndBeforeBegin(accountLabel, beginDate, endDate))
    elif endDate > currentDate then error (PositionsActivityRangeEndsAfterCurrentDate(accountLabel, beginDate, endDate))
    else Ok()

let confirmDatedInRange
    (accountLabel: string)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (activityDate: LocalDate)
    : Result<unit, IAppError> =
    if activityDate < beginDate || activityDate > endDate then
        error (PositionsActivityOutsideRange(accountLabel, beginDate, endDate, activityDate))
    else
        Ok()

/// Confirms no two ranges of one account, among those given in one operation, share a date. Each range is the
/// account's key, the label naming it in the error, and its begin and end dates.
let confirmRangesDontOverlap (ranges: ('k * string * LocalDate * LocalDate) list) : Result<unit, IAppError> =
    let indexed = ranges |> List.indexed
    indexed
    |> List.tryPick (fun (i, (key, label, firstBegin, firstEnd)) ->
        indexed
        |> List.tryFind (fun (j, (otherKey, _, secondBegin, secondEnd)) ->
            j > i && otherKey = key && firstBegin <= secondEnd && secondBegin <= firstEnd)
        |> Option.map (fun (_, (_, _, secondBegin, secondEnd)) -> label, (firstBegin, firstEnd), (secondBegin, secondEnd)))
    |> function
        | Some(label, first, second) -> error (PositionsActivityRangesOverlap(label, first, second))
        | None -> Ok()

let persist (context: Context.Context) (activity: InvestmentActivity) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.investment_activity(
            unique_id, investment_account_id, activity_date, ordinal, activity_kind, description, source, holding_id,
            quantity, price, amount, created_at, modified_at)
        values (
            @unique_id, @investment_account_id, @activity_date, @ordinal, @activity_kind, @description, @source,
            @holding_id, @quantity, @price, @amount, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(activity.investmentActivityId |> InvestmentActivityId.value) }
          { name = "@investment_account_id"; value = UniqueId(activity.investmentAccountId |> InvestmentAccountId.value) }
          { name = "@activity_date"; value = DbLocalDate activity.activityDate }
          { name = "@ordinal"; value = Integer activity.ordinal }
          { name = "@activity_kind"; value = CharString(activity.kind |> ActivityKind.toString) }
          { name = "@description"; value = CharString(activity.description |> ActivityDescription.value) }
          { name = "@source"; value = NullableCharString(activity.source |> Option.map ActivitySource.value) }
          { name = "@holding_id"; value = NullableUniqueId(activity.holdingId |> Option.map HoldingId.value) }
          { name = "@quantity"; value = NullableNumeric(activity.quantity |> Option.map Quantity.amount) }
          { name = "@price"; value = NullableNumeric(activity.price |> Option.map Price.amount) }
          { name = "@amount"; value = Numeric(activity.amount |> Money.amount) }
          { name = "@created_at"; value = DbInstant activity.createdAt }
          { name = "@modified_at"; value = DbInstant activity.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private accountParameter (investmentAccountId: InvestmentAccountId) =
    { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) }

let private rangeParameters (beginDate: LocalDate) (endDate: LocalDate) =
    [ { name = "@begin_date"; value = DbLocalDate beginDate }; { name = "@end_date"; value = DbLocalDate endDate } ]

/// Deletes every activity of the account dated in the range, both ends included.
let deleteByInvestmentAccountBetween
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.investment_activity
        where investment_account_id = @investment_account_id
            and activity_date between @begin_date and @end_date;"""
    let parameters = accountParameter investmentAccountId :: rangeParameters beginDate endDate
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters AnyQuantityIsAcceptable

let private reconstitute raw =
    result {
        let uuid, accountId, activityDate, ordinal, kindStr, descriptionStr, sourceStr, holdingId, quantityRaw, priceRaw,
            amountRaw, createdAt, modifiedAt = raw
        let! kind = kindStr |> ActivityKind.fromString
        let! description = descriptionStr |> ActivityDescription.create
        let! source = sourceStr |> convertOptionToDesiredTypeWithFallibleConverter ActivitySource.create
        let! quantity = quantityRaw |> convertOptionToDesiredTypeWithFallibleConverter Quantity.fromDecimal
        let! price = priceRaw |> convertOptionToDesiredTypeWithFallibleConverter Price.fromDecimal
        let! amount = amountRaw |> Money.fromDecimal
        do! confirmShape $"{accountId}" activityDate kind (holdingId |> Option.isSome) quantity price amount
        return
            create
                (uuid |> InvestmentActivityId.fromGuid)
                (accountId |> InvestmentAccountId.fromGuid)
                activityDate
                ordinal
                kind
                description
                source
                (holdingId |> Option.map HoldingId.fromGuid)
                quantity
                price
                amount
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "investment_account_id"),
    (row |> RowReader.getDate "activity_date"),
    (row |> RowReader.getInt "ordinal"),
    (row |> RowReader.getString "activity_kind"),
    (row |> RowReader.getString "description"),
    (row |> RowReader.getStringOption "source"),
    (row |> RowReader.getUuidOption "holding_id"),
    (row |> RowReader.getNumericOption "quantity"),
    (row |> RowReader.getNumericOption "price"),
    (row |> RowReader.getNumeric "amount"),
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
    : Result<InvestmentActivity list, IAppError> =
    let from = "positions.investment_activity invact"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    : Result<InvestmentActivity list, IAppError> =
    let select =
        """
        invact.unique_id, invact.investment_account_id, invact.activity_date, invact.ordinal, invact.activity_kind,
        invact.description, invact.source, invact.holding_id, invact.quantity, invact.price, invact.amount,
        invact.created_at, invact.modified_at"""
    query context None select None predicate limit None None parameters AnyQuantityIsAcceptable
    |> Result.map (List.sortBy (fun a -> a.activityDate, a.ordinal))

/// The account's activities in date order and, within a date, in the order supplied.
let fetchByInvestmentAccount
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    : Result<InvestmentActivity list, IAppError> =
    fetchAny context (Some "invact.investment_account_id = @investment_account_id") None [ accountParameter investmentAccountId ]

/// The account's activities dated in the range, both ends included, in date order and, within a date, in the order
/// supplied.
let fetchByInvestmentAccountBetween
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<InvestmentActivity list, IAppError> =
    fetchAny
        context
        (Some "invact.investment_account_id = @investment_account_id and invact.activity_date between @begin_date and @end_date")
        None
        (accountParameter investmentAccountId :: rangeParameters beginDate endDate)

/// Whether any activity names the Holding.
let existsForHolding (context: Context.Context) (holdingId: HoldingId) : Result<bool, IAppError> =
    let parameters = [ { name = "@holding_id"; value = UniqueId(holdingId |> HoldingId.value) } ]
    fetchAny context (Some "invact.holding_id = @holding_id") (Some 1) parameters
    |> Result.map (List.isEmpty >> not)
