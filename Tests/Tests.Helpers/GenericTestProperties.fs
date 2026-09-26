module Tests.Helpers.GenericTestProperties

open Business.General
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath

// account
let genericAccountCodeString = "GenCode"
let genericAccountCode =
    genericAccountCodeString
    |> AccountCode.create
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let genericAccountNameString = "Gen account name"
let genericAccountName =
    genericAccountNameString
    |> AccountName.create
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let genericAccountTypeString = "Revenue"
let genericAccountType =
    AccountType.fromString genericAccountTypeString
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let genericActiveBegin = Calendar.today().PlusYears(-1)
let genericActiveEnd = None
let genericActivityPeriod =
    ActivityPeriod.create genericActiveBegin genericActiveEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let genericAccountSubtype = None
let genericAccountSubtypeString = "Cash"
let genericAccountSubtypeNonNull =
    AccountSubtype.fromString genericAccountSubtypeString
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
let genericAccountParentId = None
let genericAccountParentCode = None
let genericAccountReference = None
// fiscal period
let genericFiscalPeriodKeyString = "2050-01"
let genericFiscalPeriodKey =
    genericFiscalPeriodKeyString
    |> FiscalPeriodKey.fromString
    |> Result.defaultWith(fun e -> failwith(e.ToMessage()))
