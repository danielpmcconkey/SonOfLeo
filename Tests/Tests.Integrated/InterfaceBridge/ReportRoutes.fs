namespace Tests.Integrated.InterfaceBridge

open InterfaceBridge.InterfaceContracts.ReportsContracts
open InterfaceBridge.Json.Json
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type ReportRoutesTests(fixture: TestDataFixture) =

    let nextMonth = Calendar.today().PlusMonths(1)
    let testOutputDir = "/tmp/son-of-leo-test-output"

    [<Fact>]
    member _.``REQ-RPT-2.2 data-only mode returns boundary-type rows with expected field types``() =
        let input: TrialBalanceInput = { asOf = { asOf = nextMonth }; reportOutput = OutputSpecifier.DataOnly }
        result {
            let! payload = input |> toJson<TrialBalanceInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReturn>
            return!
                match returned with
                | TrialBalanceReturn.DataOnly rows ->
                    Assert.True(rows |> List.length > 0)
                    let row = rows |> List.head
                    Assert.False(System.String.IsNullOrWhiteSpace row.accountCode)
                    Assert.False(System.String.IsNullOrWhiteSpace row.accountName)
                    Assert.True(row.level >= 0)
                    Ok ()
                | TrialBalanceReturn.Report _ ->
                    Error (TestingError "Expected DataOnly but got Report")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.3 report mode writes an HTML file and returns the file path``() =
        let input: TrialBalanceInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = false; fileName = "rpt-2-3-test" } }
        result {
            let! payload = input |> toJson<TrialBalanceInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReturn>
            return returned
        }
        |> Result.map(fun returned ->
            match returned with
            | TrialBalanceReturn.Report pathReturn ->
                Assert.True(System.IO.File.Exists pathReturn.fullyQualifiedPath)
                Assert.Contains(".html", pathReturn.fullyQualifiedPath)
                System.IO.File.Delete pathReturn.fullyQualifiedPath
            | TrialBalanceReturn.DataOnly _ ->
                Assert.Fail "Expected Report but got DataOnly")
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-2.4 date interpolation appends yyyy-MM-dd to filename before extension``() =
        let input: TrialBalanceInput =
            { asOf = { asOf = nextMonth }
              reportOutput = OutputSpecifier.Report { baseDir = testOutputDir; interpolateAsOf = true; fileName = "rpt-2-4-test" } }
        let expectedDateStr = nextMonth |> Calendar.localDateToString "yyyy-MM-dd"
        result {
            let! payload = input |> toJson<TrialBalanceInput>
            let! returnPayload = routeReportingCommandForTesting "TrialBalance" [] payload
            let! returned = returnPayload |> fromJson<TrialBalanceReturn>
            return returned
        }
        |> Result.map(fun returned ->
            match returned with
            | TrialBalanceReturn.Report pathReturn ->
                let fileName = System.IO.Path.GetFileNameWithoutExtension pathReturn.fullyQualifiedPath
                Assert.Contains($"-{expectedDateStr}", fileName)
                System.IO.File.Delete pathReturn.fullyQualifiedPath
            | TrialBalanceReturn.DataOnly _ ->
                Assert.Fail "Expected Report but got DataOnly")
        |> railroadWrapper
