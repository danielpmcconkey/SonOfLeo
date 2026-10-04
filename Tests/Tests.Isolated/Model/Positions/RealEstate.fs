module Tests.Isolated.Model.Positions.RealEstate

open NodaTime
open App.Utility.IAppError
open Business.General.Person
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Tests.Helpers.TestError
open Xunit

let private mustSucceed (r: Result<'a, IAppError>) =
    r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private money d = Money.fromDecimal d |> mustSucceed
let private acquired = LocalDate(2030, 6, 15)
let private instant = Instant.FromUtc(2030, 1, 1, 0, 0)

let private propertyWith (purchaseBasis: decimal) =
    Property.create
        (PropertyId.create ())
        (PropertyName.create "12 Example Street" |> mustSucceed)
        PrimaryResidence
        (OwnedPeriod.create acquired None |> mustSucceed)
        (PurchaseBasis.create (money purchaseBasis) |> mustSucceed)
        None
        [ PersonId.create () ]
        []
        instant
        instant

let private valuationOf property (date: LocalDate) (value: decimal) =
    Valuation.create
        (ValuationId.create ())
        (property |> Property.propertyId)
        date
        (ValuationValue.create (money value) |> mustSucceed)
        (ValuationBasis.create "Appraisal" |> mustSucceed)
        instant
        instant

[<Fact>]
let ``REQ-POS-9.4 a Property whose disposal date is the day before its acquisition date is rejected with a typed error, and one whose disposal date equals its acquisition date is accepted`` () =
    match OwnedPeriod.create acquired (Some(acquired.PlusDays(-1))) with
    | Error (AsError (PositionsDisposalDateBeforeAcquisitionDate (a, d))) ->
        Assert.Equal(acquired, a)
        Assert.Equal(acquired.PlusDays(-1), d)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
    let sameDay = OwnedPeriod.create acquired (Some acquired) |> mustSucceed
    Assert.Equal(Some acquired, sameDay |> OwnedPeriod.disposalDate)

[<Fact>]
let ``REQ-POS-9.4 a Property is owned on its acquisition date and on the day before its disposal date, and not on its disposal date or the day before acquisition`` () =
    let disposal = acquired.PlusMonths(8)
    let period = OwnedPeriod.create acquired (Some disposal) |> mustSucceed
    Assert.True(period |> OwnedPeriod.isOwnedOn acquired, "acquisition date")
    Assert.True(period |> OwnedPeriod.isOwnedOn (disposal.PlusDays(-1)), "day before disposal")
    Assert.False(period |> OwnedPeriod.isOwnedOn disposal, "disposal date")
    Assert.False(period |> OwnedPeriod.isOwnedOn (acquired.PlusDays(-1)), "day before acquisition")

[<Fact>]
let ``REQ-POS-9.4 a Property with no disposal date is owned on every date from its acquisition onward, and not on the day before acquisition`` () =
    let period = OwnedPeriod.create acquired None |> mustSucceed
    for date in [ acquired; acquired.PlusDays(1); acquired.PlusYears(50) ] do
        Assert.True(period |> OwnedPeriod.isOwnedOn date, $"{date}")
    Assert.False(period |> OwnedPeriod.isOwnedOn (acquired.PlusDays(-1)), "day before acquisition")

[<Fact>]
let ``REQ-POS-9.5 for each of zero and -0.01, a purchase basis is rejected with a typed error, and one of 0.01 is accepted`` () =
    for amount in [ 0M; -0.01M ] do
        match PurchaseBasis.create (money amount) with
        | Error (AsError (PositionsPurchaseBasisNotPositive raw)) -> Assert.Equal(amount, raw)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for {amount}; got success"
    Assert.Equal(0.01M, PurchaseBasis.create (money 0.01M) |> mustSucceed |> PurchaseBasis.value |> Money.amount)

[<Fact>]
let ``REQ-POS-10.1 for each of zero and -0.01, a valuation value is rejected with a typed error, and one of 0.01 is accepted`` () =
    for amount in [ 0M; -0.01M ] do
        match ValuationValue.create (money amount) with
        | Error (AsError (PositionsValuationValueNotPositive raw)) -> Assert.Equal(amount, raw)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for {amount}; got success"
    Assert.Equal(0.01M, ValuationValue.create (money 0.01M) |> mustSucceed |> ValuationValue.value |> Money.amount)

[<Fact>]
let ``REQ-POS-10.4 a Property's value on a date is the value of its latest Valuation dated on or before that date, ignoring an earlier one and one dated after`` () =
    let property = propertyWith 300000.00M
    let valuations =
        [ valuationOf property (acquired.PlusMonths(1)) 310000.00M
          valuationOf property (acquired.PlusMonths(5)) 330000.00M
          valuationOf property (acquired.PlusMonths(3)) 320000.00M ]
    let value = Valuation.valueOn (acquired.PlusMonths(4)) property valuations
    Assert.Equal(320000.00M, value |> Money.amount)

[<Fact>]
let ``REQ-POS-10.4 a Valuation dated on the date itself is the one that gives the Property's value on that date`` () =
    let property = propertyWith 300000.00M
    let date = acquired.PlusMonths(3)
    let valuations =
        [ valuationOf property (date.PlusDays(-1)) 315000.00M
          valuationOf property date 322500.00M ]
    Assert.Equal(322500.00M, Valuation.valueOn date property valuations |> Money.amount)

[<Fact>]
let ``REQ-POS-10.4 a Property whose only Valuations are dated after the date is worth its purchase basis on that date`` () =
    let property = propertyWith 300000.00M
    let valuations = [ valuationOf property (acquired.PlusMonths(6)) 340000.00M ]
    Assert.Equal(300000.00M, Valuation.valueOn (acquired.PlusMonths(5)) property valuations |> Money.amount)

[<Fact>]
let ``REQ-POS-10.4 a Property with no Valuations is worth its purchase basis on any date`` () =
    let property = propertyWith 287500.00M
    for date in [ acquired; acquired.PlusYears(3) ] do
        Assert.Equal(287500.00M, Valuation.valueOn date property [] |> Money.amount)
