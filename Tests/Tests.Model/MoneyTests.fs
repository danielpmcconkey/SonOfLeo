module Tests.Model.MoneyTests

open Xunit
open Model.Money

// =============================================================================
// fromDecimal
// =============================================================================

[<Fact>]
let ``fromDecimal accepts valid 2dp amount`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``fromDecimal rejects amount with more than 2dp precision`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``fromDecimal rejects amount exceeding max (9999999999.99)`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``fromDecimal rejects amount below min (-9999999999.99)`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``fromDecimal accepts negative amounts`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``fromDecimal accepts zero`` () =
    Assert.Fail "not implemented"

// =============================================================================
// splitByN
// =============================================================================

[<Fact>]
let ``splitByN produces parts that sum exactly to original`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``splitByN handles uneven split with residual in first element`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``splitByN rejects n of zero`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``splitByN rejects n of one`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``splitByN rejects negative n`` () =
    Assert.Fail "not implemented"

// =============================================================================
// add / subtract
// =============================================================================

[<Fact>]
let ``add returns correct sum`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``add returns Error when sum exceeds max`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``subtract returns correct difference`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``subtract returns Error when difference falls below min`` () =
    Assert.Fail "not implemented"
