module Tests.Integrated.CrossDomainOrchestration.StagingIngestionRules

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type StagingIngestionRulesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-STG-1.17 a file whose records use the property names baseStageEntryGroupId, entryDate, amount, entryType, accountCode, description, fiSource, fiReference and memo is ingested with records sharing a baseStageEntryGroupId in one staged entry and every other value stored in its field`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.17 for each of group_id, entry_date, line_type, fi_source and fi_reference, a file whose records spell that required property by its column name instead is rejected and nothing is ingested`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.17 for each text property (baseStageEntryGroupId, entryType, accountCode, description, fiSource, fiReference, memo), a value padded with spaces is treated as its trimmed value: stored trimmed, grouped, parsed or resolved as if trimmed, and accepted when only the padding takes it past its maximum length`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.25 each ingestion source created by name receives a system-generated UUID, distinct from every other source's`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.26 for each of null, empty and whitespace only, creating an ingestion source with that name is rejected with a typed error and no source is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.26 an ingestion source name of 100 characters is stored, and one of 101 is rejected with a typed error and not stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.26 an ingestion source name padded with spaces is stored trimmed, and one of 100 characters plus padding is accepted`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.26 a record whose fiSource equals an ingestion source's name is staged with that source's ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.26 batch post writes the ingestion source's name as the financial institution on the external reference of each journal entry posted from that source`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.27 REQ-STG-3.15 an ingestion source created by name reads back with that name, a system-generated ID, and created-at and modified-at both equal to the creating operation's instant`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-3.11 for each of a missing import directory, a missing processed directory and a file absent from the import directory, the ingestion request fails with a typed error and nothing is ingested`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-3.13 ingestion returns every staged entry it created and no other, each with its header, all of its lines and its status transitions`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-3.14 ingesting a file with a line an active account rule matches and a group whose source and reference match an existing staged entry leaves every new entry Ingested, the matched line with a null account, and nothing flagged duplicate`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-4.1.1 after transitions to Classified and then Reviewed, the entry's current status is Reviewed, the to-status of its latest audit record, and an audit record added later becomes its current status on the next read`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-4.1.1 a staged entry's current status is the to-status of its audit record with the latest instant, including when a record with an earlier instant is added after it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-5.11 classification returns its run ID, one outcome per line it evaluated, and every staged entry that is Ingested, Classified, NoMatch, Conflict or Reviewed after the run, and none that is Duplicate, Ignored or Posted`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.3.1 a manual update naming a line of a different staged entry is rejected with a typed error naming both the entry and the line, and neither entry changes`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.3.2 for each of an update naming no fields and an update setting every field it names to its current value (other than status alone), the manual update is rejected with a typed error and nothing is written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.3.2 a manual update that sets only the status, to the entry's current status, succeeds and writes nothing: no new status transition and no change to the entry or its lines`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.7 for each of Duplicate and Ignored, a manual update moving an entry with a line referenced by a Payment to that status is rejected with a typed error and the entry keeps its status`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.7 deduplication leaves an entry that would be a duplicate but has a line referenced by a Payment at its status, and lists it in its result as not flagged because of the Payment`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-7.5.1 deduplication returns every staged entry that is Ingested after the pass and no entry of any other status, including those it just flagged Duplicate`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-8.5 shadow post's after trial balance includes a staged line dated the day it runs and excludes one dated the next day, and its before trial balance likewise excludes a ledger entry dated the next day`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-8.5 shadow post's result states that the post was rolled back`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.10 batch post records on each staged entry the ID of the journal entry it produced`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.10 for an entry whose lines differ in account, line type or amount, each staged line records the journal entry line with the same account, line type and amount, including when the staged lines are not in the order the journal entry lines are created in`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.10 for an entry with two lines of the same account, line type and amount, each of those staged lines records a different journal entry line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.11 batch post returns before and after trial balances equal to those shadow post returns for the same entries, and states that the post was not rolled back`` () =
        Assert.Fail "not implemented"
