module Tests.Integrated.CrossDomainOrchestration.OperationInstantAndAtomicity

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type OperationInstantAndAtomicityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-SYS-3.3 for each update (amending a comment's text, re-pointing a comment's secondary journal entry, voiding a journal entry, re-pointing a payment agreement link), under a clock that advances on every read, the record's modified-at is set to the operation's initiation instant and its created-at is unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-3.3 a rejected update leaves the record's modified-at unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-3.4 posting a journal entry with several lines, an external reference and a comment, under a clock that advances on every read, gives every row it writes a created-at and modified-at equal to the operation's single initiation instant`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-3.4 a batch post of several staged entries, under a clock that advances on every read, writes every journal entry's created-at and modified-at and every status transition's timestamp as the one initiation instant of the operation`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-3.4 an operation initiated just before local midnight derives its current date from the initiation instant even when the clock passes midnight during the operation`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-3.4 every operation run through the interface's command runner carries an auditable action identifying that operation, and two different operations carry different actions`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-8.1 for each of posting a journal entry whose last comment fails only at database write and creating a master agreement whose last leg names an unknown account, the request fails after earlier writes were issued and none of its writes are in the database`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-8.1 an operation run through the interface's command runner that writes and then raises an exception leaves none of its writes in the database`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-8.1 a multi-write operation run through the interface that succeeds leaves every one of its writes in the database`` () =
        Assert.Fail "not implemented"
