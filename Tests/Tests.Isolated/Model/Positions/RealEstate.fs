module Tests.Isolated.Model.Positions.RealEstate

open Xunit

[<Fact>]
let ``REQ-POS-9.4 a Property whose disposal date is the day before its acquisition date is rejected with a typed error, and one whose disposal date equals its acquisition date is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.4 a Property is owned on its acquisition date and on the day before its disposal date, and not on its disposal date or the day before acquisition`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.4 a Property with no disposal date is owned on every date from its acquisition onward, and not on the day before acquisition`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-9.5 for each of zero and -0.01, a purchase basis is rejected with a typed error, and one of 0.01 is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.1 for each of zero and -0.01, a valuation value is rejected with a typed error, and one of 0.01 is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.4 a Property's value on a date is the value of its latest Valuation dated on or before that date, ignoring an earlier one and one dated after`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.4 a Valuation dated on the date itself is the one that gives the Property's value on that date`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.4 a Property whose only Valuations are dated after the date is worth its purchase basis on that date`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-10.4 a Property with no Valuations is worth its purchase basis on any date`` () =
    Assert.Fail "Not yet implemented"
