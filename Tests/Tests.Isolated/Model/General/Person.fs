module Tests.Isolated.Model.General.Person

open Xunit

[<Fact>]
let ``REQ-PER-1.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Person name is rejected with a typed empty-name error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-PER-1.1 REQ-SYS-1.1 a Person name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-PER-1.2 a Person name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error carrying the limit`` () =
    Assert.Fail "Not yet implemented"
