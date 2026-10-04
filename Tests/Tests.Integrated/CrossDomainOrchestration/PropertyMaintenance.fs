module Tests.Integrated.CrossDomainOrchestration.PropertyMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PropertyMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.6 creating a Property stores its name, use, owners, acquisition date, disposal date, purchase basis, asset account and mortgage accounts, and the list returns each of them with every linked account's code and name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.1 creating a Property whose name exactly matches an existing Property's is rejected with a typed error naming the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.1 a Property whose name differs from an existing one's only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.3 a Property with three owners is created, and the list shows all three`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.3 a Property given no owners is rejected with a typed error naming the Property`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.3 a Property given the same Person twice among its owners is rejected with a typed error naming the Person`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.3 REQ-PER-2.4 a Property with an owner name that matches no Person fails with a typed error naming that name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.6 a primary residence acquired on the date another primary residence is disposed of is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.6 a primary residence acquired the day before another primary residence's disposal date is rejected with a typed error naming both Properties`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.6 a primary residence acquired while another with no disposal date is owned is rejected with a typed error naming both Properties`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.6 a Rental owned on the same dates as a primary residence is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.6 an update moving a primary residence's disposal date past another primary residence's acquisition date is rejected with a typed error naming both Properties`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.6 an update changing a Rental's use to PrimaryResidence while another primary residence is owned on an overlapping date is rejected with a typed error naming both Properties`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.7 for each of an Asset account of a subtype other than FixedAsset and an account of a type other than Asset, linking it as a Property's asset account fails with a typed error naming the code and what is wrong`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.7 REQ-POS-4.9 linking a Property's asset account to a ledger account already another Property's asset account is rejected with a typed error naming the code and that other Property`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.8 a mortgage account of a type other than Liability fails with a typed error naming the code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.8 a ledger account already a mortgage account of one Property, given as a mortgage account of another, is rejected with a typed error naming the code and the Property already linked`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.8 a Property with two mortgage accounts is created, and the list shows both with their codes and names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 updating a Property addressed by its current name changes its name, use, acquisition date, disposal date, purchase basis and asset account, and the list shows every new value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 an update giving owners and mortgage accounts replaces each set whole, so each set afterwards is exactly the one given: a member left out is gone and a new member is present`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 an update that clears a Property's disposal date and asset account leaves it with neither`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.7 moving a Property's acquisition date after its first Valuation and its disposal date before its last is rejected with a typed error naming the earliest and latest offending valuation dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.7 moving a Property's acquisition date to exactly its first Valuation's date and its disposal date to exactly its last Valuation's date succeeds`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 updating a Property by a name that matches no Property fails with a typed not-found error naming Property and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 listing Properties returns every fixture Property ordered by name, with its owners' names and its asset and mortgage accounts' codes and names, and no other Property`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 REQ-POS-10.1 recording a Valuation stores its date, value and basis, and listing the Property's Valuations returns it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 recording a Valuation for a Property and date that already has one replaces its value and basis, leaving exactly one Valuation on that date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-10.3 a Valuation dated the day before the Property's acquisition date is rejected with a typed error naming the Property and the date, and one dated on the acquisition date is accepted`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-10.3 a Valuation dated on the Property's disposal date is accepted, and one dated the day after is rejected with a typed error naming the Property and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-10.3 REQ-SYS-3.4 a Valuation dated on the calendar date of the operation's initiation instant is accepted, and one dated the day after is rejected with a typed error naming the Property and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 deleting a Property's Valuation for a date removes it and leaves the Property's other Valuations`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 REQ-SYS-6.1 deleting a Valuation for a date on which the Property has none fails with a typed error naming the Property and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 listing a Property's Valuations returns every fixture Valuation of that Property in date order and none of another Property`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 recording a Valuation for a Property name that matches no Property fails with a typed not-found error naming Property and the name`` () =
        Assert.Fail "Not yet implemented"
