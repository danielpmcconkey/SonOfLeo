module Tests.Isolated.Model.Positions.LotsAndActivity

open NodaTime
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Xunit

let private mustSucceed (r: Result<'a, IAppError>) =
    r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private expectOk (label: string) (r: Result<'a, IAppError>) =
    match r with
    | Ok _ -> ()
    | Error e -> Assert.Fail $"{label}: expected success; got {e.ToMessage()}"

let private positionsError (label: string) (r: Result<'a, IAppError>) : PositionsError =
    match r with
    | Error(AsError(e: PositionsError)) -> e
    | Error e -> failwith $"{label}: wrong error. {e.ToMessage()}"
    | Ok _ -> failwith $"{label}: expected failure; got success"

let private account = "Alex Brokerage"
let private security = "Example Total Market Index Fund"
let private snapshotDate = LocalDate(2031, 5, 16)
let private activityDate = LocalDate(2031, 5, 12)

let private quantity (d: decimal) = Quantity.fromDecimal d |> mustSucceed
let private money (d: decimal) = Money.fromDecimal d |> mustSucceed
let private price (d: decimal) = Price.fromDecimal d |> mustSucceed

let private confirmLots (lineQuantity: decimal) (lots: (LocalDate * decimal * decimal option) list) =
    lots
    |> List.map (fun (acquired, q, cost) -> acquired, quantity q, cost |> Option.map money)
    |> AccountSnapshotLot.confirmLots account snapshotDate security (quantity lineQuantity)

let private oneLot (acquired: LocalDate) (q: decimal) (cost: decimal option) = confirmLots q [ acquired, q, cost ]

let private acquired = snapshotDate.PlusDays(-100)

let private confirmShape kind namesSecurity (q: decimal option) (p: decimal option) (amount: decimal) =
    InvestmentActivity.confirmShape account activityDate kind namesSecurity (q |> Option.map quantity) (p |> Option.map price) (money amount)

let private kindNames =
    [ "Contribution"; "RolloverIn"; "TransferIn"; "Purchase"; "Reinvestment"; "AdjustmentIn"; "Withdrawal"
      "RolloverOut"; "TransferOut"; "Sale"; "Fee"; "AdjustmentOut"; "Dividend"; "Interest"; "CapitalGainDistribution" ]

[<Fact>]
let ``REQ-POS-6.11 a lot with a quantity of 0 is rejected with a typed error naming the account, the snapshot date and the security, and a lot with a quantity of 0.000001 is accepted`` () =
    match confirmLots 1M [ acquired, 0M, Some 10M; acquired, 1M, Some 10M ] |> positionsError "quantity 0" with
    | PositionsLotQuantityNotPositive(a, d, s) ->
        Assert.Equal(account, a)
        Assert.Equal(snapshotDate, d)
        Assert.Equal(security, s)
    | other -> Assert.Fail $"Wrong error: {other}"
    oneLot acquired 0.000001M (Some 10M) |> expectOk "quantity 0.000001"

[<Fact>]
let ``REQ-POS-6.11 a lot with a reported cost basis of -0.01 is rejected with a typed error naming the account, the snapshot date and the security, and lots with a cost basis of 0.00 and with no cost basis are each accepted`` () =
    match oneLot acquired 5M (Some -0.01M) |> positionsError "cost -0.01" with
    | PositionsLotCostBasisNegative(a, d, s, cost) ->
        Assert.Equal(account, a)
        Assert.Equal(snapshotDate, d)
        Assert.Equal(security, s)
        Assert.Equal(-0.01M, cost)
    | other -> Assert.Fail $"Wrong error: {other}"
    oneLot acquired 5M (Some 0.00M) |> expectOk "cost 0.00"
    oneLot acquired 5M None |> expectOk "no cost"

[<Fact>]
let ``REQ-POS-6.11 a lot acquired the day after the snapshot date is rejected with a typed error naming the account, the snapshot date, the security and the acquired date, and a lot acquired on the snapshot date is accepted`` () =
    match oneLot (snapshotDate.PlusDays 1) 5M (Some 50M) |> positionsError "acquired after" with
    | PositionsLotAcquiredAfterSnapshot(a, d, s, acquiredDate) ->
        Assert.Equal(account, a)
        Assert.Equal(snapshotDate, d)
        Assert.Equal(security, s)
        Assert.Equal(snapshotDate.PlusDays 1, acquiredDate)
    | other -> Assert.Fail $"Wrong error: {other}"
    oneLot snapshotDate 5M (Some 50M) |> expectOk "acquired on the snapshot date"

[<Fact>]
let ``REQ-POS-6.12 three lots with six-decimal quantities that sum exactly to the line's quantity are accepted`` () =
    confirmLots 50.000000M [ acquired, 19.999998M, Some 2000M; acquired, 15.000001M, Some 1500M; acquired, 15.000001M, None ]
    |> expectOk "exact sum"

[<Fact>]
let ``REQ-POS-6.12 for each of lots summing to 0.000001 more and 0.000001 less than the line's quantity, the line is rejected with a typed error naming the account, the snapshot date, the security, the exact sum of the lots and the line's quantity`` () =
    for lastLot, expectedSum in [ 15.000002M, 50.000001M; 15.000000M, 49.999999M ] do
        let lots = [ acquired, 19.999998M, Some 2000M; acquired, 15.000001M, Some 1500M; acquired, lastLot, None ]
        match confirmLots 50M lots |> positionsError $"sum {expectedSum}" with
        | PositionsLotsDontSumToLineQuantity(a, d, s, lotSum, lineQuantity) ->
            Assert.Equal(account, a)
            Assert.Equal(snapshotDate, d)
            Assert.Equal(security, s)
            Assert.Equal(expectedSum, lotSum)
            Assert.Equal(50M, lineQuantity)
        | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-6.12 lots whose quantities sum exactly to the line's quantity but whose cost bases sum to 0.03 more than the line's reported cost basis are accepted`` () =
    // The line's own reported cost basis is 3500.00; the lots' bases sum to 3500.03. Lots are checked only against
    // the line's quantity, so the line's cost basis isn't an input at all.
    confirmLots 35M [ acquired, 20M, Some 2000.01M; acquired, 15M, Some 1500.02M ] |> expectOk "cost bases off by 0.03"

[<Fact>]
let ``REQ-POS-12.2 each of the fifteen activity kinds parses from its exact name to a distinct kind that gives back the same name, and 'purchase', 'Buy' and an empty string are each rejected with a typed error carrying the text`` () =
    let parsed = kindNames |> List.map (ActivityKind.fromString >> mustSucceed)
    Assert.Equal(15, parsed |> List.distinct |> List.length)
    Assert.Equal<string list>(kindNames, parsed |> List.map ActivityKind.toString)
    for raw in [ "purchase"; "Buy"; "" ] do
        match ActivityKind.fromString raw |> positionsError raw with
        | PositionsInvalidActivityKind text -> Assert.Equal(raw, text)
        | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-12.2 for each of the fifteen activity kinds, its units move in for Contribution, RolloverIn, TransferIn, Purchase, Reinvestment and AdjustmentIn, out for Withdrawal, RolloverOut, TransferOut, Sale, Fee and AdjustmentOut, and not at all for Dividend, Interest and CapitalGainDistribution`` () =
    let expected =
        [ "Contribution", UnitsIn; "RolloverIn", UnitsIn; "TransferIn", UnitsIn; "Purchase", UnitsIn
          "Reinvestment", UnitsIn; "AdjustmentIn", UnitsIn; "Withdrawal", UnitsOut; "RolloverOut", UnitsOut
          "TransferOut", UnitsOut; "Sale", UnitsOut; "Fee", UnitsOut; "AdjustmentOut", UnitsOut
          "Dividend", NoUnits; "Interest", NoUnits; "CapitalGainDistribution", NoUnits ]
    let actual =
        kindNames |> List.map (fun name -> name, name |> ActivityKind.fromString |> mustSucceed |> ActivityKind.direction)
    Assert.Equal<(string * UnitDirection) list>(expected, actual)

[<Fact>]
let ``REQ-SYS-1.1 REQ-POS-12.2 each of the fifteen activity kinds wrapped in leading and trailing whitespace parses to the same kind as its bare name`` () =
    for name in kindNames do
        let padded = ActivityKind.fromString $"  {name}\t " |> mustSucceed
        Assert.Equal(ActivityKind.fromString name |> mustSucceed, padded)

[<Fact>]
let ``REQ-POS-12.3 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, an activity description is rejected with a typed empty-description error`` () =
    for raw in [ ""; "   \t " ] do
        match ActivityDescription.create raw |> positionsError $"'{raw}'" with
        | PositionsActivityDescriptionIsEmpty _ -> ()
        | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-12.3 an activity description of exactly 500 characters is accepted and one of 501 characters is rejected with a typed too-long error`` () =
    let exact = String.replicate 500 "d"
    Assert.Equal(exact, ActivityDescription.create exact |> mustSucceed |> ActivityDescription.value)
    match ActivityDescription.create (String.replicate 501 "d") |> positionsError "501" with
    | PositionsActivityDescriptionTooLong(_, limit) -> Assert.Equal(500, limit)
    | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-12.3 REQ-POS-12.4 REQ-SYS-1.1 an activity description and an activity source with leading and trailing whitespace each hold the trimmed text`` () =
    Assert.Equal(
        "Contribution - payroll",
        ActivityDescription.create "  Contribution - payroll \t" |> mustSucceed |> ActivityDescription.value)
    Assert.Equal("Employer match", ActivitySource.create "\t Employer match  " |> mustSucceed |> ActivitySource.value)

[<Fact>]
let ``REQ-POS-12.4 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, an activity source is rejected with a typed empty-source error`` () =
    for raw in [ ""; "  \t  " ] do
        match ActivitySource.create raw |> positionsError $"'{raw}'" with
        | PositionsActivitySourceIsEmpty _ -> ()
        | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-12.4 an activity source of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    let exact = String.replicate 100 "s"
    Assert.Equal(exact, ActivitySource.create exact |> mustSucceed |> ActivitySource.value)
    match ActivitySource.create (String.replicate 101 "s") |> positionsError "101" with
    | PositionsActivitySourceTooLong(_, limit) -> Assert.Equal(100, limit)
    | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-12.6 for each of the fifteen kinds and each combination of naming a Security or not and carrying a quantity or not, an activity is accepted exactly when Purchase, Sale, Reinvestment, AdjustmentIn and AdjustmentOut name a Security and carry a quantity, Dividend, Interest and CapitalGainDistribution carry no quantity with or without a Security, and every other kind carries a quantity exactly when it names a Security`` () =
    let needsBoth = set [ "Purchase"; "Sale"; "Reinvestment"; "AdjustmentIn"; "AdjustmentOut" ]
    let neverQuantity = set [ "Dividend"; "Interest"; "CapitalGainDistribution" ]
    let expectedAccepted name namesSecurity carriesQuantity =
        if needsBoth.Contains name then namesSecurity && carriesQuantity
        elif neverQuantity.Contains name then not carriesQuantity
        else namesSecurity = carriesQuantity
    let mismatches =
        [ for name in kindNames do
              let kind = ActivityKind.fromString name |> mustSucceed
              for namesSecurity in [ false; true ] do
                  for carriesQuantity in [ false; true ] do
                      let q = if carriesQuantity then Some 2M else None
                      let accepted = confirmShape kind namesSecurity q None 10M |> Result.isOk
                      if accepted <> expectedAccepted name namesSecurity carriesQuantity then
                          yield $"{name} security={namesSecurity} quantity={carriesQuantity} accepted={accepted}" ]
    Assert.Empty(mismatches)

[<Fact>]
let ``REQ-POS-12.6 a Purchase naming no Security, a Sale carrying no quantity, a Dividend carrying a quantity, a Contribution naming a Security with no quantity and a Fee carrying a quantity with no Security are each rejected with a typed error naming the account, the activity date, the kind and that activity's own problem`` () =
    let cases =
        [ ActivityKind.Purchase, false, Some 2M, SecurityMissing
          ActivityKind.Sale, true, None, QuantityMissing
          ActivityKind.Dividend, true, Some 2M, QuantityNotAllowed
          ActivityKind.Contribution, true, None, QuantityMissing
          ActivityKind.Fee, false, Some 2M, QuantityWithoutSecurity ]
    for kind, namesSecurity, q, problem in cases do
        let kindName = kind |> ActivityKind.toString
        match confirmShape kind namesSecurity q None 10M |> positionsError kindName with
        | PositionsActivityShapeInvalid(a, d, k, p) ->
            Assert.Equal(account, a)
            Assert.Equal(activityDate, d)
            Assert.Equal(kindName, k)
            Assert.Equal(problem, p)
        | other -> Assert.Fail $"{kindName}: wrong error: {other}"

[<Fact>]
let ``REQ-POS-12.7 an activity carrying a quantity of 0 is rejected with a typed error naming the account, the activity date and the kind, and one carrying a quantity of 0.000001 is accepted`` () =
    match confirmShape ActivityKind.Purchase true (Some 0M) None 10M |> positionsError "quantity 0" with
    | PositionsActivityQuantityNotPositive(a, d, k) ->
        Assert.Equal(account, a)
        Assert.Equal(activityDate, d)
        Assert.Equal("Purchase", k)
    | other -> Assert.Fail $"Wrong error: {other}"
    confirmShape ActivityKind.Purchase true (Some 0.000001M) None 10M |> expectOk "quantity 0.000001"

[<Fact>]
let ``REQ-POS-12.7 an activity carrying a price with no quantity is rejected with a typed error naming the account, the activity date and the kind, and the same activity with a quantity added is accepted`` () =
    match confirmShape ActivityKind.Purchase true None (Some 10M) 50M |> positionsError "price alone" with
    | PositionsActivityPriceWithoutQuantity(a, d, k) ->
        Assert.Equal(account, a)
        Assert.Equal(activityDate, d)
        Assert.Equal("Purchase", k)
    | other -> Assert.Fail $"Wrong error: {other}"
    confirmShape ActivityKind.Purchase true (Some 5M) (Some 10M) 50M |> expectOk "price with quantity"

[<Fact>]
let ``REQ-POS-12.7 an activity with an amount of -0.01 is rejected with a typed error naming the account, the activity date and the kind, and one with an amount of 0.00 is accepted`` () =
    match confirmShape ActivityKind.Interest false None None -0.01M |> positionsError "amount -0.01" with
    | PositionsActivityAmountNegative(a, d, k, amount) ->
        Assert.Equal(account, a)
        Assert.Equal(activityDate, d)
        Assert.Equal("Interest", k)
        Assert.Equal(-0.01M, amount)
    | other -> Assert.Fail $"Wrong error: {other}"
    confirmShape ActivityKind.Interest false None None 0.00M |> expectOk "amount 0.00"

[<Fact>]
let ``REQ-POS-13.1 an activity dated the day before its range's begin date, and one dated the day after its end date, are each rejected with a typed error naming the account, the range and the activity's date, and activities dated on the begin and end dates are accepted`` () =
    let beginDate = LocalDate(2031, 5, 1)
    let endDate = LocalDate(2031, 5, 31)
    for outside in [ beginDate.PlusDays -1; endDate.PlusDays 1 ] do
        match InvestmentActivity.confirmDatedInRange account beginDate endDate outside |> positionsError $"{outside}" with
        | PositionsActivityOutsideRange(a, b, e, d) ->
            Assert.Equal(account, a)
            Assert.Equal(beginDate, b)
            Assert.Equal(endDate, e)
            Assert.Equal(outside, d)
        | other -> Assert.Fail $"Wrong error: {other}"
    for inside in [ beginDate; endDate ] do
        InvestmentActivity.confirmDatedInRange account beginDate endDate inside |> expectOk $"{inside}"

[<Fact>]
let ``REQ-POS-13.1 a range whose end date is the day before its begin date is rejected with a typed error naming the account and both dates`` () =
    let beginDate = LocalDate(2031, 5, 10)
    let currentDate = LocalDate(2031, 6, 1)
    match InvestmentActivity.confirmRange account beginDate (beginDate.PlusDays -1) currentDate |> positionsError "end before begin" with
    | PositionsActivityRangeEndBeforeBegin(a, b, e) ->
        Assert.Equal(account, a)
        Assert.Equal(beginDate, b)
        Assert.Equal(beginDate.PlusDays -1, e)
    | other -> Assert.Fail $"Wrong error: {other}"
    InvestmentActivity.confirmRange account beginDate beginDate currentDate |> expectOk "one-day range"

[<Fact>]
let ``REQ-POS-13.3 two ranges for one account in one operation that share a single day are rejected with a typed error naming the account and both ranges`` () =
    let first = LocalDate(2031, 5, 1), LocalDate(2031, 5, 15)
    let second = LocalDate(2031, 5, 15), LocalDate(2031, 5, 31)
    let ranges = [ "key-a", account, fst first, snd first; "key-a", account, fst second, snd second ]
    match InvestmentActivity.confirmRangesDontOverlap ranges |> positionsError "overlap" with
    | PositionsActivityRangesOverlap(a, r1, r2) ->
        Assert.Equal(account, a)
        Assert.Equal(first, r1)
        Assert.Equal(second, r2)
    | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-POS-13.3 two ranges for one account in one operation where the second begins the day after the first ends are accepted, and two ranges for two different accounts covering the same dates are accepted`` () =
    let b, e = LocalDate(2031, 5, 1), LocalDate(2031, 5, 15)
    InvestmentActivity.confirmRangesDontOverlap [ "key-a", account, b, e; "key-a", account, e.PlusDays 1, e.PlusDays 10 ]
    |> expectOk "adjacent ranges"
    InvestmentActivity.confirmRangesDontOverlap [ "key-a", account, b, e; "key-b", "Sam 401k", b, e ]
    |> expectOk "two accounts, same dates"

[<Fact>]
let ``REQ-POS-15.2 when no fiscal period exists, a pre-ledger balance is rejected with a typed error naming the account code and the balance date`` () =
    let balanceDate = LocalDate(2025, 12, 31)
    match PreLedgerBalance.confirmBeforeLedger "F-1000" balanceDate None |> positionsError "no fiscal period" with
    | PositionsPreLedgerNoFiscalPeriod(code, d) ->
        Assert.Equal("F-1000", code)
        Assert.Equal(balanceDate, d)
    | other -> Assert.Fail $"Wrong error: {other}"

[<Fact>]
let ``REQ-RPT-8.1 when no fiscal period exists, a net worth date is rejected with a typed error naming the date rather than treated as pre-ledger`` () =
    let date = LocalDate(2025, 12, 31)
    match NetWorth.classifyDate [] date |> positionsError "no fiscal period" with
    | PositionsNetWorthDateOutsideFiscalPeriods d -> Assert.Equal(date, d)
    | other -> Assert.Fail $"Wrong error: {other}"
