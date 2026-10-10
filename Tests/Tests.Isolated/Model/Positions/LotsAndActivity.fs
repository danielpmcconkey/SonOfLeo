module Tests.Isolated.Model.Positions.LotsAndActivity

open Xunit

[<Fact>]
let ``REQ-POS-6.11 a lot with a quantity of 0 is rejected with a typed error naming the account, the snapshot date and the security, and a lot with a quantity of 0.000001 is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.11 a lot with a reported cost basis of -0.01 is rejected with a typed error naming the account, the snapshot date and the security, and lots with a cost basis of 0.00 and with no cost basis are each accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.11 a lot acquired the day after the snapshot date is rejected with a typed error naming the account, the snapshot date, the security and the acquired date, and a lot acquired on the snapshot date is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.12 three lots with six-decimal quantities that sum exactly to the line's quantity are accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.12 for each of lots summing to 0.000001 more and 0.000001 less than the line's quantity, the line is rejected with a typed error naming the account, the snapshot date, the security, the exact sum of the lots and the line's quantity`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-6.12 lots whose quantities sum exactly to the line's quantity but whose cost bases sum to 0.03 more than the line's reported cost basis are accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.2 each of the fifteen activity kinds parses from its exact name to a distinct kind that gives back the same name, and 'purchase', 'Buy' and an empty string are each rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.2 for each of the fifteen activity kinds, its units move in for Contribution, RolloverIn, TransferIn, Purchase, Reinvestment and AdjustmentIn, out for Withdrawal, RolloverOut, TransferOut, Sale, Fee and AdjustmentOut, and not at all for Dividend, Interest and CapitalGainDistribution`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-SYS-1.1 REQ-POS-12.2 each of the fifteen activity kinds wrapped in leading and trailing whitespace parses to the same kind as its bare name`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.3 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, an activity description is rejected with a typed empty-description error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.3 an activity description of exactly 500 characters is accepted and one of 501 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.3 REQ-POS-12.4 REQ-SYS-1.1 an activity description and an activity source with leading and trailing whitespace each hold the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.4 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, an activity source is rejected with a typed empty-source error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.4 an activity source of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.6 for each of the fifteen kinds and each combination of naming a Security or not and carrying a quantity or not, an activity is accepted exactly when Purchase, Sale, Reinvestment, AdjustmentIn and AdjustmentOut name a Security and carry a quantity, Dividend, Interest and CapitalGainDistribution carry no quantity with or without a Security, and every other kind carries a quantity exactly when it names a Security`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.6 a Purchase naming no Security, a Sale carrying no quantity, a Dividend carrying a quantity, a Contribution naming a Security with no quantity and a Fee carrying a quantity with no Security are each rejected with a typed error naming the account, the activity date, the kind and that activity's own problem`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.7 an activity carrying a quantity of 0 is rejected with a typed error naming the account, the activity date and the kind, and one carrying a quantity of 0.000001 is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.7 an activity carrying a price with no quantity is rejected with a typed error naming the account, the activity date and the kind, and the same activity with a quantity added is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-12.7 an activity with an amount of -0.01 is rejected with a typed error naming the account, the activity date and the kind, and one with an amount of 0.00 is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-13.1 an activity dated the day before its range's begin date, and one dated the day after its end date, are each rejected with a typed error naming the account, the range and the activity's date, and activities dated on the begin and end dates are accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-13.1 a range whose end date is the day before its begin date is rejected with a typed error naming the account and both dates`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-13.3 two ranges for one account in one operation that share a single day are rejected with a typed error naming the account and both ranges`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-13.3 two ranges for one account in one operation where the second begins the day after the first ends are accepted, and two ranges for two different accounts covering the same dates are accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-15.2 when no fiscal period exists, a pre-ledger balance is rejected with a typed error naming the account code and the balance date`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-RPT-8.1 when no fiscal period exists, a net worth date is rejected with a typed error naming the date rather than treated as pre-ledger`` () =
    Assert.Fail "Not yet implemented"
