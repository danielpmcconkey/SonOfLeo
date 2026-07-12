namespace Tests.Integrated.ModelOrchestrator

open System
open Model.Ledger.Accounts.AccountComponent
open Xunit
open Tests.Integrated
open Tests.Integrated._Cleanup
open Model.Audit
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open ModelOrchestrator.JournalEntryFetching
open NodaTime
open Utilities
open Utilities.ResultCE

[<Collection("SharedTestData")>]
type JournalEntryCreationTests(fixture: TestDataFixture) =

    let validPrimitives description =
        { header =
            { description = description
              source = Some "IntegrationTest"
              entryDate = Calendar.today()
              voidedAt = None }
          lines =
            [ { accountId = fixture.Data.mortgage2210Id |> AccountId.value
                amount = 100.00M
                lineType = "Debit"
                memo = Some "test debit" }
              { accountId = fixture.Data.food5350Id |> AccountId.value
                amount = 100.00M
                lineType = "Credit"
                memo = Some "test credit" } ]
          externalReferences = []
          comments = [] }

    // =============================================================================
    // Orchestrated creation — happy path
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.11 orchestrateCreation posts a valid journal entry and returns it`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims = validPrimitives "Happy path basic creation"
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                let h = je |> header
                Assert.Equal("Happy path basic creation", h |> JournalEntryHeader.description |> JournalEntryDescription.value)
                Assert.Equal(2, je |> lines |> List.length)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-2.1 orchestrateCreation generates a unique UUID for the header`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims = validPrimitives "UUID for header"
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                Assert.NotEqual(Guid.Empty, je |> header |> JournalEntryHeader.uniqueId)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-2.2 REQ-JE-1.21 orchestrateCreation generates unique UUIDs for each line`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims = validPrimitives "UUID for lines"
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                let ids = je |> lines |> List.map JournalEntryLine.uniqueId
                Assert.Equal(2, ids |> List.distinct |> List.length)
                ids |> List.iter (fun id -> Assert.NotEqual(Guid.Empty, id))
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-2.9 REQ-JE-1.40 orchestrateCreation generates unique UUIDs for each external reference`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "UUID for ext refs" with
                externalReferences =
                    [ { financialInstitution = "BankAlpha"; referenceText = "REF-A01" }
                      { financialInstitution = "BankBeta"; referenceText = "REF-B01" } ] }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                let ids = je |> externalReferences |> List.map JournalEntryExternalReference.uniqueId
                Assert.Equal(2, ids |> List.distinct |> List.length)
                ids |> List.iter (fun id -> Assert.NotEqual(Guid.Empty, id))
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-SYS-3.2 orchestrateCreation sets created_at and modified_at from AuditEnvelope`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let expectedInstant = AuditEnvelope.instant envelope
        let prims = validPrimitives "Audit timestamps"
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                let h = je |> header
                Assert.Equal(expectedInstant, h |> JournalEntryHeader.createdAt)
                Assert.Equal(expectedInstant, h |> JournalEntryHeader.modifiedAt)
                je |> lines |> List.iter (fun line ->
                    Assert.Equal(expectedInstant, line |> JournalEntryLine.createdAt)
                    Assert.Equal(expectedInstant, line |> JournalEntryLine.modifiedAt))
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.46 orchestrateCreation accepts an entry with zero external references`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims = validPrimitives "Zero ext refs"
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                Assert.Equal(0, je |> externalReferences |> List.length)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.46 orchestrateCreation accepts an entry with multiple external references`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "Multiple ext refs" with
                externalReferences =
                    [ { financialInstitution = "BankX"; referenceText = "TXN-100" }
                      { financialInstitution = "BankY"; referenceText = "TXN-200" }
                      { financialInstitution = "BankZ"; referenceText = "TXN-300" } ] }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                Assert.Equal(3, je |> externalReferences |> List.length)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.55 orchestrateCreation accepts an entry with zero comments`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims = validPrimitives "Zero comments"
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                Assert.Equal(0, je |> comments |> List.length)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.55 orchestrateCreation accepts an entry with multiple comments`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "Multiple comments" with
                comments =
                    [ { secondaryJournalEntryId = None; commentText = "First comment" }
                      { secondaryJournalEntryId = None; commentText = "Second comment" } ] }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                Assert.Equal(2, je |> comments |> List.length)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.6 orchestrateCreation accepts an entry with null source`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let base' = validPrimitives "Null source"
        let prims = { base' with header = { base'.header with source = None } }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                Assert.True(je |> header |> JournalEntryHeader.source |> Option.isNone)
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.26 orchestrateCreation accepts lines with null memos`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "Null memos" with
                lines =
                    [ { accountId = fixture.Data.mortgage2210Id |> AccountId.value; amount = 50.00M; lineType = "Debit"; memo = None }
                      { accountId = fixture.Data.food5350Id |> AccountId.value; amount = 50.00M; lineType = "Credit"; memo = None } ] }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Ok je ->
                idToCleanUp <- Some (je |> header |> JournalEntryHeader.uniqueId)
                je |> lines |> List.iter (fun line ->
                    Assert.True(line |> JournalEntryLine.memo |> Option.isNone))
            | Error e -> Assert.Fail e
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-1.48 orchestrateCreation accepts duplicate source_fi/reference pairs across entries`` () =
        let mutable idToCleanUp_1 = None
        let mutable idToCleanUp_2 = None
        try
            let envelope1 = AuditEnvelope.create JournalEntryPostNew
            let prims1 =
                { validPrimitives "Dup ref entry 1" with
                    externalReferences = [ { financialInstitution = "DupBank"; referenceText = "DUP-REF-001" } ] }
            let create1 = prims1 |> orchestrateCreation envelope1
            create1 |> Result.iter (fun je ->
                idToCleanUp_1 <- Some (je |> header |> JournalEntryHeader.uniqueId))

            let envelope2 = AuditEnvelope.create JournalEntryPostNew
            let prims2 =
                { validPrimitives "Dup ref entry 2" with
                    externalReferences = [ { financialInstitution = "DupBank"; referenceText = "DUP-REF-001" } ] }
            let create2 = prims2 |> orchestrateCreation envelope2
            create2 |> Result.iter (fun je ->
                idToCleanUp_2 <- Some (je |> header |> JournalEntryHeader.uniqueId))

            match create1, create2 with
            | Ok _, Ok _ -> ()
            | Error e, _ -> Assert.Fail $"First creation failed: {e}"
            | _, Error e -> Assert.Fail $"Second creation failed: {e}"
        finally
            match cleanUpJournalEntryList [idToCleanUp_1; idToCleanUp_2] with
            | Ok () -> ()
            | Error e -> failwith e

    // =============================================================================
    // Orchestrated creation — validation rejections
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-2.12 orchestrateCreation persists nothing when validation fails`` () =
        let farPeriodId = fixture.Data.fiscalPeriodIds |> List.last
        let railroad = result {
            let! beforeHeaders = JournalEntryHeader.fetchByPeriod None farPeriodId
            let beforeCount = beforeHeaders |> List.length

            let envelope = AuditEnvelope.create JournalEntryPostNew
            let entryDate = Calendar.today().PlusMonths(4).PlusDays(14)
            let prims =
                { header =
                    { description = "Should not persist"
                      source = None
                      entryDate = entryDate
                      voidedAt = None }
                  lines =
                    [ { accountId = fixture.Data.mortgage2210Id |> AccountId.value; amount = 100.00M; lineType = "Debit"; memo = None }
                      { accountId = fixture.Data.food5350Id |> AccountId.value; amount = 75.00M; lineType = "Credit"; memo = None } ]
                  externalReferences = []
                  comments = [] }
            let createResult = prims |> orchestrateCreation envelope
            Assert.True(Result.isError createResult)

            let! afterHeaders = JournalEntryHeader.fetchByPeriod None farPeriodId
            let afterCount = afterHeaders |> List.length
            Assert.Equal(beforeCount, afterCount)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-1.12 orchestrateCreation rejects entry with fewer than 2 lines`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "Single line" with
                lines =
                    [ { accountId = fixture.Data.mortgage2210Id |> AccountId.value; amount = 100.00M; lineType = "Debit"; memo = None } ] }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-1.13 orchestrateCreation rejects unbalanced entry — debits != credits`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "Unbalanced" with
                lines =
                    [ { accountId = fixture.Data.mortgage2210Id |> AccountId.value; amount = 100.00M; lineType = "Debit"; memo = None }
                      { accountId = fixture.Data.food5350Id |> AccountId.value; amount = 75.00M; lineType = "Credit"; memo = None } ] }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-1.22 orchestrateCreation rejects line with nonexistent account code`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let bogusId = Guid.NewGuid()
        let prims =
            { validPrimitives "Bogus account" with
                lines =
                    [ { accountId = bogusId; amount = 100.00M; lineType = "Debit"; memo = None }
                      { accountId = fixture.Data.food5350Id |> AccountId.value; amount = 100.00M; lineType = "Credit"; memo = None } ] }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-2.5 REQ-JE-2.6 orchestrateCreation rejects entry date with no matching fiscal period`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let base' = validPrimitives "No period"
        let prims = { base' with header = { base'.header with entryDate = LocalDate(2099, 1, 15) } }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-2.7 orchestrateCreation rejects entry date in a closed fiscal period`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let closedPeriodDate = Calendar.today().PlusMonths(-5).PlusDays(14)
        let base' = validPrimitives "Closed period"
        let prims = { base' with header = { base'.header with entryDate = closedPeriodDate } }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-2.8 orchestrateCreation rejects line referencing an inactive account as of entry date`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { validPrimitives "Inactive account" with
                lines =
                    [ { accountId = fixture.Data.closedBank1290Id |> AccountId.value; amount = 100.00M; lineType = "Debit"; memo = None }
                      { accountId = fixture.Data.food5350Id |> AccountId.value; amount = 100.00M; lineType = "Credit"; memo = None } ] }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-1.11 orchestrateCreation rejects entry date outside fiscal period bounds`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let base' = validPrimitives "Outside bounds"
        let prims = { base' with header = { base'.header with entryDate = LocalDate(2099, 6, 15) } }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    [<Fact>]
    member _.``REQ-JE-1.14 orchestrateCreation rejects creation of an already-voided entry`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let base' = validPrimitives "Pre-voided"
        let prims = { base' with header = { base'.header with voidedAt = Some (Clock.now()) } }
        let createResult = prims |> orchestrateCreation envelope
        Assert.True(Result.isError createResult)

    // =============================================================================
    // Persistence fidelity
    // =============================================================================

    [<Fact>]
    member _.``REQ-SYS-5.1 posted entry round-trips through persistence with all fields intact`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { header =
                { description = "Round-trip fidelity"
                  source = Some "Fidelity"
                  entryDate = Calendar.today()
                  voidedAt = None }
              lines =
                [ { accountId = fixture.Data.rothIra1250Id |> AccountId.value; amount = 200.00M; lineType = "Debit"; memo = Some "debit memo" }
                  { accountId = fixture.Data.personalRevenue4290Id |> AccountId.value; amount = 200.00M; lineType = "Credit"; memo = Some "credit memo" } ]
              externalReferences = [ { financialInstitution = "FidelityBank"; referenceText = "FID-001" } ]
              comments = [ { secondaryJournalEntryId = None; commentText = "Round-trip comment" } ] }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Error e -> Assert.Fail e
            | Ok created ->
                let jeId = created |> header |> JournalEntryHeader.uniqueId
                idToCleanUp <- Some jeId
                let fetchResult = jeId |> fetchById
                match fetchResult with
                | Error e -> Assert.Fail $"Fetch after creation failed: {e}"
                | Ok fetched ->
                    let ch = created |> header
                    let fh = fetched |> header
                    Assert.Equal(ch |> JournalEntryHeader.uniqueId, fh |> JournalEntryHeader.uniqueId)
                    Assert.Equal(
                        ch |> JournalEntryHeader.description |> JournalEntryDescription.value,
                        fh |> JournalEntryHeader.description |> JournalEntryDescription.value)
                    Assert.Equal(
                        ch |> JournalEntryHeader.source |> Option.map JournalEntrySource.value,
                        fh |> JournalEntryHeader.source |> Option.map JournalEntrySource.value)
                    Assert.Equal(
                        ch |> JournalEntryHeader.entryDate |> EntryDate.entryDate,
                        fh |> JournalEntryHeader.entryDate |> EntryDate.entryDate)
                    Assert.Equal(ch |> JournalEntryHeader.voidedAt, fh |> JournalEntryHeader.voidedAt)
                    Assert.Equal(ch |> JournalEntryHeader.createdAt, fh |> JournalEntryHeader.createdAt)
                    Assert.Equal(ch |> JournalEntryHeader.modifiedAt, fh |> JournalEntryHeader.modifiedAt)

                    let createdLines = created |> lines
                    let fetchedLines = fetched |> lines
                    Assert.Equal(createdLines |> List.length, fetchedLines |> List.length)
                    createdLines |> List.iter (fun cl ->
                        let fl =
                            fetchedLines |> List.find (fun fl ->
                                JournalEntryLine.uniqueId fl = JournalEntryLine.uniqueId cl)
                        Assert.Equal(cl |> JournalEntryLine.journalEntryId, fl |> JournalEntryLine.journalEntryId)
                        Assert.Equal(cl |> JournalEntryLine.accountId, fl |> JournalEntryLine.accountId)
                        Assert.Equal(cl |> JournalEntryLine.amount, fl |> JournalEntryLine.amount)
                        Assert.Equal(cl |> JournalEntryLine.lineType, fl |> JournalEntryLine.lineType)
                        Assert.Equal(
                            cl |> JournalEntryLine.memo |> Option.map LineMemo.value,
                            fl |> JournalEntryLine.memo |> Option.map LineMemo.value)
                        Assert.Equal(cl |> JournalEntryLine.createdAt, fl |> JournalEntryLine.createdAt)
                        Assert.Equal(cl |> JournalEntryLine.modifiedAt, fl |> JournalEntryLine.modifiedAt))

                    let createdRefs = created |> externalReferences
                    let fetchedRefs = fetched |> externalReferences
                    Assert.Equal(createdRefs |> List.length, fetchedRefs |> List.length)
                    createdRefs |> List.iter (fun cr ->
                        let fr =
                            fetchedRefs |> List.find (fun fr ->
                                JournalEntryExternalReference.uniqueId fr = JournalEntryExternalReference.uniqueId cr)
                        Assert.Equal(
                            cr |> JournalEntryExternalReference.journalEntryId,
                            fr |> JournalEntryExternalReference.journalEntryId)
                        Assert.Equal(
                            cr |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value,
                            fr |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
                        Assert.Equal(
                            cr |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value,
                            fr |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
                        Assert.Equal(cr |> JournalEntryExternalReference.createdAt, fr |> JournalEntryExternalReference.createdAt)
                        Assert.Equal(cr |> JournalEntryExternalReference.modifiedAt, fr |> JournalEntryExternalReference.modifiedAt))

                    let createdComments = created |> comments
                    let fetchedComments = fetched |> comments
                    Assert.Equal(createdComments |> List.length, fetchedComments |> List.length)
                    createdComments |> List.iter (fun cc ->
                        let fc =
                            fetchedComments |> List.find (fun fc ->
                                JournalEntryComment.uniqueId fc = JournalEntryComment.uniqueId cc)
                        Assert.Equal(
                            cc |> JournalEntryComment.primaryJournalEntryId,
                            fc |> JournalEntryComment.primaryJournalEntryId)
                        Assert.Equal(
                            cc |> JournalEntryComment.secondaryJournalEntryId,
                            fc |> JournalEntryComment.secondaryJournalEntryId)
                        Assert.Equal(
                            cc |> JournalEntryComment.commentText |> CommentText.value,
                            fc |> JournalEntryComment.commentText |> CommentText.value)
                        Assert.Equal(cc |> JournalEntryComment.createdAt, fc |> JournalEntryComment.createdAt)
                        Assert.Equal(cc |> JournalEntryComment.modifiedAt, fc |> JournalEntryComment.modifiedAt))
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-JE-3.1 fetched entry includes header, lines, external references, and comments`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims =
            { header =
                { description = "Full fetch test"
                  source = Some "FetchTest"
                  entryDate = Calendar.today()
                  voidedAt = None }
              lines =
                [ { accountId = fixture.Data.entertainment5650Id |> AccountId.value; amount = 30.00M; lineType = "Debit"; memo = None }
                  { accountId = fixture.Data.creditCard2220Id |> AccountId.value; amount = 30.00M; lineType = "Credit"; memo = None } ]
              externalReferences = [ { financialInstitution = "FetchBank"; referenceText = "FETCH-001" } ]
              comments = [ { secondaryJournalEntryId = None; commentText = "Fetch comment" } ] }
        let mutable idToCleanUp = None
        try
            let createResult = prims |> orchestrateCreation envelope
            match createResult with
            | Error e -> Assert.Fail e
            | Ok created ->
                let jeId = created |> header |> JournalEntryHeader.uniqueId
                idToCleanUp <- Some jeId
                let fetchResult = jeId |> fetchById
                match fetchResult with
                | Error e -> Assert.Fail $"Fetch failed: {e}"
                | Ok fetched ->
                    Assert.Equal(
                        "Full fetch test",
                        fetched |> header |> JournalEntryHeader.description |> JournalEntryDescription.value)
                    Assert.Equal(2, fetched |> lines |> List.length)
                    Assert.Equal(1, fetched |> externalReferences |> List.length)
                    Assert.Equal(1, fetched |> comments |> List.length)
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e
