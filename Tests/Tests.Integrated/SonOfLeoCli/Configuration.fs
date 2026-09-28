module Tests.Integrated.SonOfLeoCli.Configuration

open NodaTime
open App.Utility
open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type ConfigurationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-SYS-7.1 an Instant late in the evening in the configured time zone, already the next day in UTC, converts to the configured zone's date`` () =
        // the test run is configured for America/New_York; 02:30 UTC on 10 March 2050 is 21:30 on 9 March there
        let instant = Instant.FromUtc(2050, 3, 10, 2, 30)
        Assert.Equal(LocalDate(2050, 3, 10), instant.InUtc().Date)
        Assert.Equal(LocalDate(2050, 3, 9), instant |> Calendar.dateFromInstant)
