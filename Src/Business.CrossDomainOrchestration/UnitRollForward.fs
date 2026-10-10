module Business.CrossDomainOrchestration.UnitRollForward

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// One Security's units between two snapshots. Expected and difference are exact decimals, not Quantities: a missing
/// activity can make the expected quantity negative, and a difference has a sign.
type RollForwardRow = {
    securityName: string
    startQuantity: decimal
    unitsIn: decimal
    unitsOut: decimal
    expected: decimal
    endQuantity: decimal
    difference: decimal
}

type RollForward = {
    investmentAccountName: string
    firstDate: LocalDate
    secondDate: LocalDate
    rows: RollForwardRow list
}

let private snapshotOn
    (context: Context.Context)
    (accountName: string)
    (investmentAccountId: InvestmentAccountId)
    (snapshotDate: LocalDate)
    : Result<AccountSnapshotHeader.AccountSnapshotHeader, IAppError> =
    AccountSnapshotHeader.fetchByInvestmentAccountAndDate context investmentAccountId snapshotDate
    |> Result.bind (function
        | Some header -> Ok header
        | None -> error (PositionsSnapshotDoesntExist(accountName, snapshotDate)))

let private quantitiesByHolding (lines: AccountSnapshotLine.AccountSnapshotLine list) : Map<HoldingId, decimal> =
    lines
    |> List.map (fun l -> AccountSnapshotLine.holdingId l, l |> AccountSnapshotLine.quantity |> Quantity.amount)
    |> Map.ofList

/// Rolls the account's units forward from its snapshot on the first date to its snapshot on the second. Activity dated
/// on the first date is already in the first snapshot, so the window is after the first date, up to and including the
/// second. A difference is returned as data, never raised.
let rollForward
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (firstDate: LocalDate)
    (secondDate: LocalDate)
    : Result<RollForward, IAppError> =
    result {
        let! account = investmentAccountId |> InvestmentAccount.fetchById context
        let accountName = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
        do!
            if firstDate < secondDate then Ok()
            else error (PositionsRollForwardDatesNotInOrder(accountName, firstDate, secondDate))
        let! first = snapshotOn context accountName investmentAccountId firstDate
        let! second = snapshotOn context accountName investmentAccountId secondDate
        let! firstLines = first |> AccountSnapshotHeader.accountSnapshotId |> AccountSnapshotLine.fetchByAccountSnapshot context
        let! secondLines = second |> AccountSnapshotHeader.accountSnapshotId |> AccountSnapshotLine.fetchByAccountSnapshot context
        let! activities =
            InvestmentActivity.fetchByInvestmentAccountBetween context investmentAccountId (firstDate.PlusDays 1) secondDate
        let unitMoves =
            activities
            |> List.choose (fun a ->
                match a |> InvestmentActivity.holdingId, a |> InvestmentActivity.quantity with
                | Some holdingId, Some quantity -> Some(holdingId, a |> InvestmentActivity.kind |> ActivityKind.direction, quantity)
                | _ -> None)
            |> List.filter (fun (_, direction, _) -> direction <> NoUnits)
        let startQuantities = quantitiesByHolding firstLines
        let endQuantities = quantitiesByHolding secondLines
        let holdingIds =
            [ startQuantities |> Map.keys |> List.ofSeq
              endQuantities |> Map.keys |> List.ofSeq
              unitMoves |> List.map (fun (holdingId, _, _) -> holdingId) ]
            |> List.concat
            |> List.distinct
        let! holdings = investmentAccountId |> Holding.fetchByInvestmentAccount context
        let! securities = Security.fetchAll context
        let securityNameOf holdingId =
            holdings
            |> List.find (fun h -> Holding.holdingId h = holdingId)
            |> Holding.securityId
            |> fun securityId -> securities |> List.find (fun s -> Security.securityId s = securityId)
            |> Security.securityName
            |> SecurityName.value
        let movedFor holdingId direction =
            unitMoves
            |> List.filter (fun (id, d, _) -> id = holdingId && d = direction)
            |> List.map (fun (_, _, quantity) -> quantity)
            |> Quantity.sum
        let rows =
            holdingIds
            |> List.map (fun holdingId ->
                let startQuantity = startQuantities |> Map.tryFind holdingId |> Option.defaultValue 0M
                let unitsIn = movedFor holdingId UnitsIn
                let unitsOut = movedFor holdingId UnitsOut
                let expected = startQuantity + unitsIn - unitsOut
                let endQuantity = endQuantities |> Map.tryFind holdingId |> Option.defaultValue 0M
                { securityName = securityNameOf holdingId
                  startQuantity = startQuantity
                  unitsIn = unitsIn
                  unitsOut = unitsOut
                  expected = expected
                  endQuantity = endQuantity
                  difference = endQuantity - expected })
            |> List.sortBy (fun r -> r.securityName)
        return
            { investmentAccountName = accountName
              firstDate = firstDate
              secondDate = secondDate
              rows = rows }
    }
