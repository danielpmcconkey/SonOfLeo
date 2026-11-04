# Record and Same-Named Accessor Module: Qualified Access Hits the Field

**Source:** slice 3 audit refactors (`InstanceOrchestration.InvoiceCompositeUpdate`), 2026-10-11;
reproduced 2026-11-04 with .NET SDK 10.0.114 (FSharp.Core 10.0.114).

When a record type and a same-named module sit side by side inside another module, and the
module has an accessor named exactly like one of the record's fields, a fully qualified call to
that accessor resolves to the record's field, not the accessor, and fails to compile.

## What works

- Open the enclosing module and use the short form: `open Outer.Orchestration`, then
  `built |> Update.invoiceUpdates` calls the accessor.
- Or give the accessor a name that is not a field name (`invoiceUpdatesOf`); the qualified form
  then compiles. Accessors here are named for their fields by convention, so prefer the `open`.

## What doesn't

- `built |> Outer.Orchestration.Update.invoiceUpdates`. `Outer.Orchestration.Update.create`
  compiles, which makes the failure look arbitrary: no field is called `create`.
- Making the record public. That removes one of the two errors below, not the other: privacy
  is not the cause, the field and accessor sharing a name is.

## Example

`type Update = private { invoiceUpdates: int }` and `module Update = let invoiceUpdates (update:
Update) = update.invoiceUpdates`, both inside `module Outer.Orchestration`. A caller in another
file writing the qualified form gets:

```
error FS0728: Field 'invoiceUpdates' is not static
error FS1093: The union cases or fields of the type 'Update' are not accessible from this code location
```

With the record public, only FS0728. Recognise it by FS0728 on a pipe into a fully qualified
`Module.Type.name`. Entity modules (`MasterAgreement.agreementName`) don't hit it, because there
the record lives inside its own module rather than beside a same-named one. Expect it the next
time a private record and its accessor module are nested in an orchestration module, as
`InvoiceCompositeUpdate` is (tests reach that one through `Tests.Helpers.CompositeUpdates`).
