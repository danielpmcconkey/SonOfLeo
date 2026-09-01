module Tests.Helpers.GenericTestProperties

open Model
open Model.Ledger.AccountComponent
open Model.Ledger.FiscalPeriodComponent
open Utilities
open Utilities.AppError

// account
let genericAccountCodeString = "GenCode"
let genericAccountCode =
    genericAccountCodeString
    |> AccountCode.create
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountNameString = "Gen account name"
let genericAccountName =
    genericAccountNameString
    |> AccountName.create
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountTypeString = "Revenue"
let genericAccountType =
    AccountType.fromString genericAccountTypeString
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericActiveBegin = Calendar.today().PlusYears(-1)
let genericActiveEnd = None
let genericActivityPeriod =
    ActivityPeriod.create genericActiveBegin genericActiveEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountSubtype = None
let genericAccountSubtypeString = "Cash"
let genericAccountSubtypeNonNull =
    AccountSubtype.fromString genericAccountSubtypeString
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
let genericAccountParentId = None
let genericAccountParentCode = None
let genericAccountReference = None
// fiscal period
let genericFiscalPeriodKeyString = "2050-01"
let genericFiscalPeriodKey =
    genericFiscalPeriodKeyString
    |> FiscalPeriodKey.fromString
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
