module Tests.Isolated.Model.CashFlow.TrimmedText

(* REQ-SYS-1.1: the text value types of the cash-flow and ingestion models hold exactly the text inside any leading
   and trailing whitespace, keep whitespace inside the text, and measure their length limit after trimming. *)

open System
open App.Utility.IAppError
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Xunit

let private padded (text: string) = $" \t  {text} \t "

/// The value the constructor holds for `raw`, failing the test with the error if it refuses it.
let private held (create: string -> Result<'t, IAppError>) (value: 't -> string) (raw: string) =
    match create raw with
    | Ok v -> value v
    | Error e -> raise (Xunit.Sdk.XunitException $"Refused. {e.DomainName}.{e.CaseName}: {e.ToMessage()}")

[<Fact>]
let ``REQ-SYS-1.1 REQ-CF-5.15 an invoice memo holds exactly the text inside its leading and trailing whitespace, and a memo of 2000 characters inside whitespace is held as those 2000 characters`` () =
    let atLimit = String('m', 2000)
    Assert.Equal("an  invoice\tmemo", padded "an  invoice\tmemo" |> held InvoiceMemo.create InvoiceMemo.value)
    Assert.Equal(atLimit, padded atLimit |> held InvoiceMemo.create InvoiceMemo.value)

[<Fact>]
let ``REQ-SYS-1.1 REQ-CF-5.14 a blocker note holds exactly the text inside its leading and trailing whitespace, and a note of 500 characters inside whitespace is held as those 500 characters`` () =
    let atLimit = String('n', 500)
    Assert.Equal("which  account\tto use", padded "which  account\tto use" |> held BlockerNote.create BlockerNote.value)
    Assert.Equal(atLimit, padded atLimit |> held BlockerNote.create BlockerNote.value)

[<Fact>]
let ``REQ-SYS-1.1 an external invoice ID holds exactly the text inside its leading and trailing whitespace, and an ID of 100 characters inside whitespace is held as those 100 characters`` () =
    let atLimit = String('x', 100)
    Assert.Equal("INV  0042", padded "INV  0042" |> held ExternalInvoiceId.create ExternalInvoiceId.value)
    Assert.Equal(atLimit, padded atLimit |> held ExternalInvoiceId.create ExternalInvoiceId.value)

[<Fact>]
let ``REQ-SYS-1.1 REQ-CF-2.22 a Master Agreement memo holds exactly the text inside its leading and trailing whitespace, and a memo of 2000 characters inside whitespace is held as those 2000 characters`` () =
    let atLimit = String('m', 2000)
    Assert.Equal("an  agreement\tmemo", padded "an  agreement\tmemo" |> held AgreementMemo.create AgreementMemo.value)
    Assert.Equal(atLimit, padded atLimit |> held AgreementMemo.create AgreementMemo.value)

[<Fact>]
let ``REQ-SYS-1.1 REQ-CF-6.7 a Payment memo holds exactly the text inside its leading and trailing whitespace, and a memo of 2000 characters inside whitespace is held as those 2000 characters`` () =
    let atLimit = String('m', 2000)
    Assert.Equal("a  payment\tmemo", padded "a  payment\tmemo" |> held PaymentMemo.create PaymentMemo.value)
    Assert.Equal(atLimit, padded atLimit |> held PaymentMemo.create PaymentMemo.value)

[<Fact>]
let ``REQ-SYS-1.1 a staged entry's source file holds exactly the text inside its leading and trailing whitespace, and a source file of 150 characters inside whitespace is held as those 150 characters`` () =
    let atLimit = String('f', 150)
    Assert.Equal("statement  2049-03.csv", padded "statement  2049-03.csv" |> held SourceFile.create SourceFile.value)
    Assert.Equal(atLimit, padded atLimit |> held SourceFile.create SourceFile.value)
