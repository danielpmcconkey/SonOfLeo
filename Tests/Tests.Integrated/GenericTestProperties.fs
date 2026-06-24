module Tests.Integrated.GenericTestProperties

open Model.Audit
open Model.Ledger.Accounts.AccountComponent
open Utilities

// audit
let genericAuditEnvelope = AuditEnvelope.create AccountCreate

// account
let genericAccountCodeString = "GenCode"
let genericAccountNameString = "Gen account name"
let genericAccountTypeString = "Revenue"
let genericAccountType = AccountType.fromString genericAccountTypeString |> Result.defaultWith failwith
let genericAccountActiveBegin = Calendar.today().PlusYears(-1)
let genericAccountActiveEnd = None
let genericAccountSubtype = None
let genericAccountSubtypeString = "Cash"
let genericAccountSubtypeNonNull = AccountSubtype.fromString genericAccountSubtypeString |> Result.defaultWith failwith
let genericAccountParentId = None
let genericAccountParentCode = None
let genericAccountReference= None

// fiscal period
let genericFiscalPeriodKey = "2026-07"


