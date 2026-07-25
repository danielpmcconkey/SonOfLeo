// Canonical shape for FieldUpdate SET-clause builders (PATTERNS.md P2.5, settled via #126a).
// Items are inline pipeline expressions via FieldUpdate.mapNoChangeToOptionWithConversion —
// the same idiom as P4.7 dynamic filter lists. One blank line between multiline items
// (Fantomas preserves it). A `match` never appears as a direct list item.

let updates =
    [
        nameUpdate |> FieldUpdate.mapNoChangeToOptionWithConversion (fun n ->
            (", account_name = @account_name", { name = "@account_name"; value = CharString (AccountName.value n) }))

        referenceUpdate |> FieldUpdate.mapNoChangeToOptionWithConversion (fun r ->
            let value = r |> Option.map AccountExternalReference.value
            (", external_ref = @external_ref", { name = "@external_ref"; value = NullableCharString value }))
    ] |> List.choose id
