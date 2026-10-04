module Tests.Isolated.Model.Positions.PositionsComponent

open App.Utility.IAppError
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Tests.Helpers.TestError
open Xunit

let private mustSucceed (r: Result<'a, IAppError>) =
    r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private isEmptyError (r: Result<'a, IAppError>) (isExpected: PositionsError -> bool) =
    match r with
    | Error (AsError (e: PositionsError)) when isExpected e -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

/// A name type's limit: the value at the limit is accepted whole and one character over is refused with the limit.
let private checkLimit
    (limit: int)
    (create: string -> Result<'a, IAppError>)
    (value: 'a -> string)
    (tooLongLimit: PositionsError -> int option)
    =
    let atLimit = String.replicate limit "x"
    Assert.Equal(atLimit, create atLimit |> mustSucceed |> value)
    match create (atLimit + "x") with
    | Error (AsError (e: PositionsError)) when (tooLongLimit e).IsSome -> Assert.Equal(Some limit, tooLongLimit e)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-2.1 each of InvestmentType, MarketCap, IndexType, Sector, Region, Objective and Benchmark parses to a distinct dimension that gives back the same text`` () =
    let texts = [ "InvestmentType"; "MarketCap"; "IndexType"; "Sector"; "Region"; "Objective"; "Benchmark" ]
    let parsed = texts |> List.map (Dimension.fromString >> mustSucceed)
    Assert.Equal(7, parsed |> List.distinct |> List.length)
    Assert.Equal<string list>(texts, parsed |> List.map Dimension.toString)

[<Theory>]
[<InlineData("region")>]
[<InlineData("AssetClass")>]
[<InlineData("")>]
let ``REQ-POS-2.1 for each of 'region', 'AssetClass' and an empty string, a dimension name outside the seven is rejected with a typed error carrying the text`` (text: string) =
    match Dimension.fromString text with
    | Error (AsError (PositionsInvalidDimension raw)) -> Assert.Equal(text, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-2.2 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Dimension Value name is rejected with a typed empty-name error`` (raw: string) =
    isEmptyError (DimensionValueName.create raw) (function PositionsDimensionValueNameIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-2.2 a Dimension Value name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    checkLimit 100 DimensionValueName.create DimensionValueName.value (function
        | PositionsDimensionValueNameTooLong (_, limit) -> Some limit
        | _ -> None)

[<Fact>]
let ``REQ-POS-2.2 REQ-SYS-1.1 a Dimension Value name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Equal("Large Cap", DimensionValueName.create "  Large Cap  " |> mustSucceed |> DimensionValueName.value)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-3.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Security name is rejected with a typed empty-name error`` (raw: string) =
    isEmptyError (SecurityName.create raw) (function PositionsSecurityNameIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-3.1 a Security name of exactly 200 characters is accepted and one of 201 characters is rejected with a typed too-long error`` () =
    checkLimit 200 SecurityName.create SecurityName.value (function
        | PositionsSecurityNameTooLong (_, limit) -> Some limit
        | _ -> None)

[<Fact>]
let ``REQ-POS-3.1 REQ-SYS-1.1 a Security name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Equal(
        "Example Total Market Index Fund",
        SecurityName.create " Example Total Market Index Fund\t" |> mustSucceed |> SecurityName.value)

[<Fact>]
let ``REQ-POS-3.3 a whitespace-only ticker is rejected with a typed empty-ticker error`` () =
    isEmptyError (Ticker.create "   ") (function PositionsTickerIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-3.3 a ticker of exactly 20 characters is accepted and one of 21 characters is rejected with a typed too-long error`` () =
    checkLimit 20 Ticker.create Ticker.value (function
        | PositionsTickerTooLong (_, limit) -> Some limit
        | _ -> None)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-4.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, an Investment Account name is rejected with a typed empty-name error`` (raw: string) =
    isEmptyError (InvestmentAccountName.create raw) (function PositionsInvestmentAccountNameIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-4.1 an Investment Account name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    checkLimit 100 InvestmentAccountName.create InvestmentAccountName.value (function
        | PositionsInvestmentAccountNameTooLong (_, limit) -> Some limit
        | _ -> None)

[<Fact>]
let ``REQ-POS-4.1 REQ-POS-4.2 REQ-POS-4.3 REQ-SYS-1.1 an Investment Account name, an institution and an account group with leading and trailing whitespace each hold the trimmed text`` () =
    Assert.Equal("Alex Brokerage", InvestmentAccountName.create "  Alex Brokerage " |> mustSucceed |> InvestmentAccountName.value)
    Assert.Equal("Example Brokerage", Institution.create "\tExample Brokerage  " |> mustSucceed |> Institution.value)
    Assert.Equal("Retirement", AccountGroup.create " Retirement\t" |> mustSucceed |> AccountGroup.value)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-4.2 for each of an empty string and a whitespace-only string, an institution is rejected with a typed empty-institution error`` (raw: string) =
    isEmptyError (Institution.create raw) (function PositionsInstitutionIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-4.2 an institution of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    checkLimit 100 Institution.create Institution.value (function
        | PositionsInstitutionTooLong (_, limit) -> Some limit
        | _ -> None)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-4.3 for each of an empty string and a whitespace-only string, an account group is rejected with a typed empty-account-group error`` (raw: string) =
    isEmptyError (AccountGroup.create raw) (function PositionsAccountGroupIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-4.3 an account group of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    checkLimit 100 AccountGroup.create AccountGroup.value (function
        | PositionsAccountGroupTooLong (_, limit) -> Some limit
        | _ -> None)

[<Fact>]
let ``REQ-POS-4.4 each of Taxable, TaxDeferred, Roth and Hsa parses to a distinct tax treatment that gives back the same text`` () =
    let texts = [ "Taxable"; "TaxDeferred"; "Roth"; "Hsa" ]
    let parsed = texts |> List.map (TaxTreatment.fromString >> mustSucceed)
    Assert.Equal(4, parsed |> List.distinct |> List.length)
    Assert.Equal<string list>(texts, parsed |> List.map TaxTreatment.toString)

[<Theory>]
[<InlineData("taxable")>]
[<InlineData("IRA")>]
[<InlineData("")>]
let ``REQ-POS-4.4 for each of 'taxable', 'IRA' and an empty string, a tax treatment outside the four is rejected with a typed error carrying the text`` (text: string) =
    match TaxTreatment.fromString text with
    | Error (AsError (PositionsInvalidTaxTreatment raw)) -> Assert.Equal(text, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-5.2 each of AverageCost and SpecificLot parses to a distinct basis method, and 'averagecost' and 'Fifo' are rejected with a typed error carrying the text`` () =
    let texts = [ "AverageCost"; "SpecificLot" ]
    let parsed = texts |> List.map (BasisMethod.fromString >> mustSucceed)
    Assert.Equal(2, parsed |> List.distinct |> List.length)
    Assert.Equal<string list>(texts, parsed |> List.map BasisMethod.toString)
    for text in [ "averagecost"; "Fifo" ] do
        match BasisMethod.fromString text with
        | Error (AsError (PositionsInvalidBasisMethod raw)) -> Assert.Equal(text, raw)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for {text}; got success"

[<Fact>]
let ``REQ-POS-6.2 each of Reported and Imported parses to a distinct provenance, and 'reported' and 'Estimated' are rejected with a typed error carrying the text`` () =
    let texts = [ "Reported"; "Imported" ]
    let parsed = texts |> List.map (Provenance.fromString >> mustSucceed)
    Assert.Equal(2, parsed |> List.distinct |> List.length)
    Assert.Equal<string list>(texts, parsed |> List.map Provenance.toString)
    for text in [ "reported"; "Estimated" ] do
        match Provenance.fromString text with
        | Error (AsError (PositionsInvalidProvenance raw)) -> Assert.Equal(text, raw)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for {text}; got success"

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-9.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Property name is rejected with a typed empty-name error`` (raw: string) =
    isEmptyError (PropertyName.create raw) (function PositionsPropertyNameIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-9.1 a Property name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    checkLimit 100 PropertyName.create PropertyName.value (function
        | PositionsPropertyNameTooLong (_, limit) -> Some limit
        | _ -> None)

[<Fact>]
let ``REQ-POS-9.1 REQ-POS-10.2 REQ-SYS-1.1 a Property name and a valuation basis with leading and trailing whitespace each hold the trimmed text`` () =
    Assert.Equal("12 Example Street", PropertyName.create "  12 Example Street " |> mustSucceed |> PropertyName.value)
    Assert.Equal("Appraisal", ValuationBasis.create "\tAppraisal  " |> mustSucceed |> ValuationBasis.value)

[<Fact>]
let ``REQ-POS-9.2 each of PrimaryResidence and Rental parses to a distinct use, and 'rental' and 'Vacation' are rejected with a typed error carrying the text`` () =
    let texts = [ "PrimaryResidence"; "Rental" ]
    let parsed = texts |> List.map (PropertyUse.fromString >> mustSucceed)
    Assert.Equal(2, parsed |> List.distinct |> List.length)
    Assert.Equal<string list>(texts, parsed |> List.map PropertyUse.toString)
    for text in [ "rental"; "Vacation" ] do
        match PropertyUse.fromString text with
        | Error (AsError (PositionsInvalidPropertyUse raw)) -> Assert.Equal(text, raw)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for {text}; got success"

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-POS-10.2 for each of an empty string and a whitespace-only string, a valuation basis is rejected with a typed empty-basis error`` (raw: string) =
    isEmptyError (ValuationBasis.create raw) (function PositionsValuationBasisIsEmpty _ -> true | _ -> false)

[<Fact>]
let ``REQ-POS-10.2 a valuation basis of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    checkLimit 100 ValuationBasis.create ValuationBasis.value (function
        | PositionsValuationBasisTooLong (_, limit) -> Some limit
        | _ -> None)
