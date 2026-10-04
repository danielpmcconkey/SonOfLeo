module Tests.Isolated.Model.QuantityAndPrice

open Xunit

[<Fact>]
let ``REQ-QP-1.1 REQ-QP-3.1 Quantity.fromDecimal accepts zero and gives back zero`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-1.1 REQ-QP-3.1 Quantity.fromDecimal rejects -0.000001 with a typed negative-value error carrying the value`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-1.2 REQ-QP-3.1 Quantity.fromDecimal accepts the maximum 9,999,999,999.999999 and gives it back unchanged`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-1.2 REQ-QP-3.1 Quantity.fromDecimal rejects 10,000,000,000, the smallest six-place value above the maximum, with a typed exceeds-maximum error carrying the value`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-1.3 REQ-QP-3.1 Quantity.fromDecimal accepts a value with exactly six decimal places and gives back every digit`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-1.3 REQ-QP-3.1 for each of 0.0000001 and 1.2345675, Quantity.fromDecimal rejects a value with a seventh decimal place with a typed precision error carrying the unrounded value`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-2.1 REQ-QP-3.2 Price.fromDecimal accepts zero and gives back zero`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-2.1 REQ-QP-3.2 Price.fromDecimal rejects -0.000001 with a typed negative-value error carrying the value`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-2.2 REQ-QP-3.2 Price.fromDecimal accepts the maximum 9,999,999,999.999999 and gives it back unchanged`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-2.2 REQ-QP-3.2 Price.fromDecimal rejects 10,000,000,000, the smallest six-place value above the maximum, with a typed exceeds-maximum error carrying the value`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-2.3 REQ-QP-3.2 Price.fromDecimal accepts a value with exactly six decimal places and gives back every digit`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-2.3 REQ-QP-3.2 for each of 0.0000001 and 1.2345675, Price.fromDecimal rejects a value with a seventh decimal place with a typed precision error carrying the unrounded value`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-3.3 for each of zero, the maximum and a six-place value, converting to a Quantity and back gives the identical decimal including its scale, and the same for a Price`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-3.4 for every ordered pair of two different Quantities and of one Quantity with itself, each of equal, less than, greater than, at most and at least gives the verdict decimal comparison gives`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-3.4 for every ordered pair of two different Prices and of one Price with itself, each of equal, less than, greater than, at most and at least gives the verdict decimal comparison gives`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-3.5 a Quantity of 1.234567 times a Price of 2.5 is exactly 3.0864175, keeping the digits beyond two decimal places that Money could not hold`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-QP-3.5 a Quantity times a Price is the exact decimal product for each of several pairs, including a zero Price and the maximum Quantity at a six-place Price`` () =
    Assert.Fail "Not yet implemented"
