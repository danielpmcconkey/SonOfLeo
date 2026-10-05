module Tests.Isolated.Model.General.Person

open App.Utility.IAppError
open Business.General.PersonComponent
open Business.General.Person
open Business.General.BizGeneralError
open Tests.Helpers.TestError
open Xunit

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``REQ-PER-1.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Person name is rejected with a typed empty-name error`` (raw: string) =
    match PersonName.create raw with
    | Error (AsError (PersonNameIsEmpty _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-PER-1.1 REQ-SYS-1.1 a Person name with leading and trailing whitespace holds the trimmed text`` () =
    match PersonName.create "  Alex Example \t" with
    | Ok name -> Assert.Equal("Alex Example", name |> PersonName.value)
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-PER-1.2 a Person name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error carrying the limit`` () =
    let atLimit = String.replicate 100 "a"
    match PersonName.create atLimit with
    | Ok name -> Assert.Equal(atLimit, name |> PersonName.value)
    | Error e -> Assert.Fail(e.ToMessage())
    match PersonName.create (atLimit + "a") with
    | Error (AsError (PersonNameTooLong (_, limit))) -> Assert.Equal(100, limit)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
