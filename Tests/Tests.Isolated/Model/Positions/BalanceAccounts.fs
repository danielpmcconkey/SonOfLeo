module Tests.Isolated.Model.Positions.BalanceAccounts

open NodaTime
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Tests.Helpers.TestError
open Xunit

let private mustSucceed (r: Result<'a, IAppError>) =
    r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private money d = Money.fromDecimal d |> mustSucceed
let private instant = Instant.FromUtc(2030, 1, 1, 0, 0)
let private day = LocalDate(2030, 6, 15)

let private balanceOf (balanceAccountId: BalanceAccountId) (date: LocalDate) (amount: decimal) =
    BalanceAccountBalance.create (BalanceAccountBalanceId.create ()) balanceAccountId date (money amount) instant instant

let private amountOn date balances =
    balances |> BalanceAccountBalance.balanceOn date |> Option.map (BalanceAccountBalance.amount >> Money.amount)

[<Fact>]
let ``REQ-POS-16.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Balance Account name is rejected with a typed empty-name error`` () =
    for raw in [ ""; "   " ] do
        match BalanceAccountName.create raw with
        | Error (AsError (PositionsBalanceAccountNameIsEmpty provided)) -> Assert.Equal(raw, provided)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for \"{raw}\"; got success"

[<Fact>]
let ``REQ-POS-16.1 a Balance Account name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    let hundred = String.replicate 100 "n"
    Assert.Equal(hundred, BalanceAccountName.create hundred |> mustSucceed |> BalanceAccountName.value)
    match BalanceAccountName.create (hundred + "n") with
    | Error (AsError (PositionsBalanceAccountNameTooLong (_, limit))) -> Assert.Equal(100, limit)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-POS-16.1 REQ-SYS-1.1 a Balance Account name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Equal("Example Checking", BalanceAccountName.create "  Example Checking \t" |> mustSucceed |> BalanceAccountName.value)

[<Fact>]
let ``REQ-POS-16.2 each of Cash and Debt parses to a distinct kind that writes back as the same text, and 'cash' and 'Loan' are rejected with a typed error carrying the text`` () =
    let cash = BalanceAccountKind.fromString "Cash" |> mustSucceed
    let debt = BalanceAccountKind.fromString "Debt" |> mustSucceed
    Assert.NotEqual(cash, debt)
    Assert.Equal<string list>([ "Cash"; "Debt" ], [ cash; debt ] |> List.map BalanceAccountKind.toString)
    for raw in [ "cash"; "Loan" ] do
        match BalanceAccountKind.fromString raw with
        | Error (AsError (PositionsInvalidBalanceAccountKind provided)) -> Assert.Equal(raw, provided)
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for \"{raw}\"; got success"

[<Fact>]
let ``REQ-POS-16.3 a Balance Account institution of an empty string or whitespace only is rejected with a typed empty error, one of 101 characters with a typed too-long error, and one of 100 characters is accepted`` () =
    for raw in [ ""; "  " ] do
        match Institution.create raw with
        | Error (AsError (PositionsInstitutionIsEmpty _)) -> ()
        | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for \"{raw}\"; got success"
    let hundred = String.replicate 100 "i"
    match Institution.create (hundred + "i") with
    | Error (AsError (PositionsInstitutionTooLong (_, limit))) -> Assert.Equal(100, limit)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
    Assert.Equal(hundred, Institution.create hundred |> mustSucceed |> Institution.value)

[<Fact>]
let ``REQ-POS-16.6 a Balance Account's balance on a date is the amount of its latest Balance dated on or before that date, and a Balance dated after it is ignored`` () =
    let account = BalanceAccountId.create ()
    let balances = [ balanceOf account (day.PlusDays(-30)) 100.00M; balanceOf account (day.PlusDays(-1)) 250.00M; balanceOf account (day.PlusDays(1)) 999.00M ]
    Assert.Equal(Some 250.00M, balances |> amountOn day)
    Assert.Equal(Some 250.00M, balances |> amountOn (day.PlusDays(-1)))
    Assert.Equal(Some 100.00M, balances |> amountOn (day.PlusDays(-2)))

[<Fact>]
let ``REQ-POS-16.6 a Balance Account whose only Balance is dated after the date has no balance on it, and one whose latest Balance is 0.00 has a balance of 0.00`` () =
    let later = BalanceAccountId.create ()
    Assert.Equal(None, [ balanceOf later (day.PlusDays(1)) 500.00M ] |> amountOn day)
    let closed = BalanceAccountId.create ()
    Assert.Equal(Some 0.00M, [ balanceOf closed (day.PlusDays(-10)) 500.00M; balanceOf closed day 0.00M ] |> amountOn day)

[<Fact>]
let ``REQ-RPT-8.1 when no fiscal period exists, net worth accepts any date`` () =
    for date in [ LocalDate(1990, 1, 1); LocalDate(2025, 12, 31); LocalDate(2099, 12, 31) ] do
        match NetWorth.isWithinFiscalPeriod [] date with
        | Ok withinOne -> Assert.False(withinOne, $"{date} is within no fiscal period")
        | Error e -> Assert.Fail $"{date} was rejected: {e.ToMessage()}"
