module Tests.Isolated.Model.Positions.PositionsComponent

open Xunit

[<Fact>]
let ``REQ-POS-2.1 each of InvestmentType, MarketCap, IndexType, Sector, Region, Objective and Benchmark parses to a distinct dimension that gives back the same text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-2.1 for each of 'region', 'AssetClass' and an empty string, a dimension name outside the seven is rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-2.2 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Dimension Value name is rejected with a typed empty-name error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-2.2 a Dimension Value name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-2.2 REQ-SYS-1.1 a Dimension Value name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-3.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Security name is rejected with a typed empty-name error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-3.1 a Security name of exactly 200 characters is accepted and one of 201 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-3.1 REQ-SYS-1.1 a Security name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-3.3 a whitespace-only ticker is rejected with a typed empty-ticker error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-3.3 a ticker of exactly 20 characters is accepted and one of 21 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, an Investment Account name is rejected with a typed empty-name error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.1 an Investment Account name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.1 REQ-POS-4.2 REQ-POS-4.3 REQ-SYS-1.1 an Investment Account name, an institution and an account group with leading and trailing whitespace each hold the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.2 for each of an empty string and a whitespace-only string, an institution is rejected with a typed empty-institution error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.2 an institution of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.3 for each of an empty string and a whitespace-only string, an account group is rejected with a typed empty-account-group error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.3 an account group of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.4 each of Taxable, TaxDeferred, Roth and Hsa parses to a distinct tax treatment that gives back the same text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.4 for each of 'taxable', 'IRA' and an empty string, a tax treatment outside the four is rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-5.2 each of AverageCost and SpecificLot parses to a distinct basis method, and 'averagecost' and 'Fifo' are rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.2 each of Reported and Imported parses to a distinct provenance, and 'reported' and 'Estimated' are rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Property name is rejected with a typed empty-name error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.1 a Property name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.1 REQ-POS-10.2 REQ-SYS-1.1 a Property name and a valuation basis with leading and trailing whitespace each hold the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.2 each of PrimaryResidence and Rental parses to a distinct use, and 'rental' and 'Vacation' are rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.2 for each of an empty string and a whitespace-only string, a valuation basis is rejected with a typed empty-basis error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.2 a valuation basis of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"
