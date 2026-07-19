module Tests.Isolated.Model.GenericTestProperties

open Model.Audit
open Model.Ledger.Accounts.AccountComponent
open Utilities
open Utilities.AppError

// audit
let genericAuditEnvelope = AuditEnvelope.create AccountCreate
// account
let genericAccountCodeString = "GenCode"
let genericAccountCode = genericAccountCodeString |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
let genericAccountNameString = "Gen account name"
let genericAccountName = genericAccountNameString |> AccountName.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
let genericAccountTypeString = "Revenue"
let genericAccountType = AccountType.fromString genericAccountTypeString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
let genericAccountActiveBegin = Calendar.today().PlusYears(-1)
let genericAccountActiveEnd = None
let genericAccountActivityPeriod = AccountActivityPeriod.create genericAccountActiveBegin genericAccountActiveEnd |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
let genericAccountSubtype = None
let genericAccountSubtypeString = "Cash"
let genericAccountSubtypeNonNull = AccountSubtype.fromString genericAccountSubtypeString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
let genericAccountParentId = None
let genericAccountParentCode = None
let genericAccountReference= None
// fiscal period
let genericFiscalPeriodKey = "2050-01"

