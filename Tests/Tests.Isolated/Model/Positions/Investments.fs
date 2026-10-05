module Tests.Isolated.Model.Positions.Investments

open NodaTime
open App.Utility.IAppError
open Business.General
open Business.General.BizGeneralError
open Business.General.PersonComponent
open Business.General.Person
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Tests.Helpers.TestError
open Xunit

let private mustSucceed (r: Result<'a, IAppError>) =
    r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private begin' = LocalDate(2031, 3, 10)
let private instant = Instant.FromUtc(2031, 1, 1, 0, 0)

let private accountWith (activeEnd: LocalDate option) =
    let period = ActivityPeriod.create begin' activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate |> mustSucceed
    InvestmentAccount.create
        (InvestmentAccountId.create ())
        (InvestmentAccountName.create "Alex Brokerage" |> mustSucceed)
        (Institution.create "Example Brokerage" |> mustSucceed)
        (AccountGroup.create "Brokerage" |> mustSucceed)
        TaxTreatment.Taxable
        (Set.singleton (PersonId.create ()))
        period
        None
        instant
        instant

let private isActiveOn date account =
    account |> InvestmentAccount.activityPeriod |> ActivityPeriod.isActive date

[<Fact>]
let ``REQ-POS-4.7 an active period whose end is the day before its begin is rejected with a typed error, and one whose end equals its begin is accepted`` () =
    match ActivityPeriod.create begin' (Some(begin'.PlusDays(-1))) ActivityPeriod.NotConsideredAvailableBeforeBeginDate with
    | Error (AsError (ActiveEndBeforeBegin (b, e))) ->
        Assert.Equal(begin', b)
        Assert.Equal(Some(begin'.PlusDays(-1)), e)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
    let sameDay = accountWith (Some begin')
    Assert.Equal(Some begin', sameDay |> InvestmentAccount.activityPeriod |> ActivityPeriod.activeEnd)

[<Fact>]
let ``REQ-POS-4.7 an Investment Account is active on its begin date and on its end date, and not on the day before begin or the day after end`` () =
    let activeEnd = begin'.PlusDays(30)
    let account = accountWith (Some activeEnd)
    Assert.True(account |> isActiveOn begin', "begin date")
    Assert.True(account |> isActiveOn activeEnd, "end date")
    Assert.False(account |> isActiveOn (begin'.PlusDays(-1)), "day before begin")
    Assert.False(account |> isActiveOn (activeEnd.PlusDays(1)), "day after end")

[<Fact>]
let ``REQ-POS-4.7 an Investment Account with no active end is active on every date from its begin onward, and not on the day before begin`` () =
    let account = accountWith None
    for date in [ begin'; begin'.PlusDays(1); begin'.PlusYears(40) ] do
        Assert.True(account |> isActiveOn date, $"{date}")
    Assert.False(account |> isActiveOn (begin'.PlusDays(-1)), "day before begin")

[<Fact>]
let ``REQ-POS-5.2 for each of the four tax treatments and each of no basis method, AverageCost and SpecificLot, the pairing is allowed exactly when the treatment is Taxable and a basis method is present, or the treatment is not Taxable and none is`` () =
    let expected =
        [ TaxTreatment.Taxable, None, false
          TaxTreatment.Taxable, Some AverageCost, true
          TaxTreatment.Taxable, Some SpecificLot, true
          TaxTreatment.TaxDeferred, None, true
          TaxTreatment.TaxDeferred, Some AverageCost, false
          TaxTreatment.TaxDeferred, Some SpecificLot, false
          TaxTreatment.Roth, None, true
          TaxTreatment.Roth, Some AverageCost, false
          TaxTreatment.Roth, Some SpecificLot, false
          TaxTreatment.Hsa, None, true
          TaxTreatment.Hsa, Some AverageCost, false
          TaxTreatment.Hsa, Some SpecificLot, false ]
    for treatment, basisMethod, allowed in expected do
        Assert.True(
            (allowed = BasisMethod.isAllowedFor treatment basisMethod),
            $"{TaxTreatment.toString treatment} with {basisMethod}: expected {allowed}")

[<Fact>]
let ``REQ-POS-6.4 a contribution basis of zero is accepted and one of -0.01 is rejected with a typed negative-contribution-basis error`` () =
    let zero = Money.fromDecimal 0M |> mustSucceed
    Assert.Equal(0M, ContributionBasis.create zero |> mustSucceed |> ContributionBasis.value |> Money.amount)
    match Money.fromDecimal -0.01M |> mustSucceed |> ContributionBasis.create with
    | Error (AsError (PositionsContributionBasisNegative amount)) -> Assert.Equal(-0.01M, amount)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

// =============================================================================
// Snapshot line figures
// =============================================================================

let private snapshotDate = LocalDate(2031, 5, 2)
let private qty d = Quantity.fromDecimal d |> mustSucceed
let private prc d = Price.fromDecimal d |> mustSucceed
let private money d = Money.fromDecimal d |> mustSucceed

let private check quantity price marketValue costBasis =
    AccountSnapshotLine.confirmFigures
        "Alex Brokerage" snapshotDate "Example Total Market Index Fund"
        (qty quantity) (prc price) (money marketValue) (costBasis |> Option.map money)

let private assertNamesLine account date security =
    Assert.Equal("Alex Brokerage", account)
    Assert.Equal(snapshotDate, date)
    Assert.Equal("Example Total Market Index Fund", security)

[<Fact>]
let ``REQ-POS-6.7 a snapshot line with a quantity of zero is rejected with a typed error naming the account, date and security`` () =
    match check 0M 10M 0M None with
    | Error (AsError (PositionsSnapshotLineQuantityNotPositive (account, date, security))) ->
        assertNamesLine account date security
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-6.7 a snapshot line with a market value of -0.01 is rejected with a typed error naming the account, date and security`` () =
    match check 1M 0M -0.01M None with
    | Error (AsError (PositionsSnapshotLineMarketValueNegative (account, date, security, amount))) ->
        assertNamesLine account date security
        Assert.Equal(-0.01M, amount)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-6.7 a snapshot line with a reported cost basis of -0.01 is rejected with a typed error naming the account, date and security, and one with no cost basis is accepted`` () =
    match check 2M 5M 10M (Some -0.01M) with
    | Error (AsError (PositionsSnapshotLineCostBasisNegative (account, date, security, amount))) ->
        assertNamesLine account date security
        Assert.Equal(-0.01M, amount)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
    match check 2M 5M 10M None with
    | Ok () -> ()
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-POS-6.8 a snapshot line whose quantity times price exceeds its market value by exactly 0.05 is accepted, and one exceeding it by 0.050001 is rejected`` () =
    // 1 x 100.05 = 100.05 against 100.00; 1 x 100.050001 = 100.050001 against 100.00.
    match check 1M 100.05M 100.00M None with
    | Ok () -> ()
    | Error e -> Assert.Fail(e.ToMessage())
    match check 1M 100.050001M 100.00M None with
    | Error (AsError (PositionsSnapshotLineOutsideTolerance _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-6.8 a snapshot line whose quantity times price falls short of its market value by exactly 0.05 is accepted, and one falling short by 0.050001 is rejected`` () =
    // 2 x 49.975 = 99.95 against 100.00; 1 x 99.949999 = 99.949999 against 100.00.
    match check 2M 49.975M 100.00M None with
    | Ok () -> ()
    | Error e -> Assert.Fail(e.ToMessage())
    match check 1M 99.949999M 100.00M None with
    | Error (AsError (PositionsSnapshotLineOutsideTolerance _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-6.8 the out-of-tolerance error names the account, the snapshot date, the security, the exact product and the market value`` () =
    // 3 x 33.333333 = 99.999999 against 100.10: 0.100001 apart.
    match check 3M 33.333333M 100.10M None with
    | Error (AsError (PositionsSnapshotLineOutsideTolerance (account, date, security, product, marketValue))) ->
        assertNamesLine account date security
        Assert.Equal(99.999999M, product)
        Assert.Equal(100.10M, marketValue)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
