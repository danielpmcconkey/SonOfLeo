module Tests.Isolated.Model.CashFlow.CashFlowComponent

open Xunit

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(366)>]
let ``REQ-CF-7.1 for each of 0, -1 and 366, a sweep horizon of that many days is rejected with a typed error`` (days: int) =
    Assert.Fail "not implemented"
