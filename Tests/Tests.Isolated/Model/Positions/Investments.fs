module Tests.Isolated.Model.Positions.Investments

open Xunit

[<Fact>]
let ``REQ-POS-4.7 an active period whose end is the day before its begin is rejected with a typed error, and one whose end equals its begin is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.7 an Investment Account is active on its begin date and on its end date, and not on the day before begin or the day after end`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-4.7 an Investment Account with no active end is active on every date from its begin onward, and not on the day before begin`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-5.2 for each of the four tax treatments and each of no basis method, AverageCost and SpecificLot, the pairing is allowed exactly when the treatment is Taxable and a basis method is present, or the treatment is not Taxable and none is`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.4 a contribution basis of zero is accepted and one of -0.01 is rejected with a typed negative-contribution-basis error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.7 a snapshot line with a quantity of zero is rejected with a typed error naming the account, date and security`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.7 a snapshot line with a market value of -0.01 is rejected with a typed error naming the account, date and security`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.7 a snapshot line with a reported cost basis of -0.01 is rejected with a typed error naming the account, date and security, and one with no cost basis is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.8 a snapshot line whose quantity times price exceeds its market value by exactly 0.05 is accepted, and one exceeding it by 0.050001 is rejected`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.8 a snapshot line whose quantity times price falls short of its market value by exactly 0.05 is accepted, and one falling short by 0.050001 is rejected`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.8 the out-of-tolerance error names the account, the snapshot date, the security, the exact product and the market value`` () =
    Assert.Fail "Not yet implemented"
