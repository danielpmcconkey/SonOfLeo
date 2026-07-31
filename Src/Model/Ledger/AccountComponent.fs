namespace Model.Ledger.Accounts

open System
open NodaTime
open Utilities.AppError

module AccountComponent =

    (*
     * This module defines simple types that are used by the Account type. It really
     * only exists as a separate module because Account was getting huge
     *)

    type AccountId = private AccountId of Guid
    module AccountId =
        let create () : AccountId = AccountId(Guid.NewGuid())
        let fromGuid g = AccountId g
        let value (AccountId g) : Guid = g

    type AccountActivityPeriod =
        private
            { activeBegin: LocalDate
              activeEnd: LocalDate option }

    module AccountActivityPeriod =
        let activeBegin (a: AccountActivityPeriod) = a.activeBegin
        let activeEnd (a: AccountActivityPeriod) = a.activeEnd
        let create (rawBegin: LocalDate) (rawEnd: LocalDate option) : Result<AccountActivityPeriod, AppError> =
            match rawEnd with
            | None -> Ok { activeBegin = rawBegin; activeEnd = None }
            | Some x ->
                if x < rawBegin then
                    Error(AccountActiveEndBeforeBegin(rawBegin, rawEnd))
                else
                    Ok { activeBegin = rawBegin; activeEnd = rawEnd }
        let isActive
            (referencePoint: LocalDate)
            (aap: AccountActivityPeriod)
            : bool =
            let beginDate = activeBegin aap
            let endDate = activeEnd aap
            match endDate with
            | None when beginDate <= referencePoint -> true // no end and begin is in the past
            | Some x when beginDate <= referencePoint && x >= referencePoint -> true // begin is in the past; end is in the future
            | None when beginDate > referencePoint -> false // no end, but hasn't started yet
            | Some x when x < referencePoint -> false // end is in the past
            | Some _ when beginDate > referencePoint -> false // there's an end date, but start is in the future
            | _ -> false

    type AccountCode = private AccountCode of string

    module AccountCode =
        let maxLength = 10
        let value (AccountCode ac) = ac // required because AccountCode is a private string
        let create (raw: string) : Result<AccountCode, AppError> =
            let trimmed = raw.Trim()
            if String.IsNullOrWhiteSpace trimmed then
                Error(AccountCodeIsEmpty raw)
            elif trimmed.Length > maxLength then
                Error(AccountCodeTooLong(raw, maxLength))
            else
                Ok(AccountCode trimmed)

    type AccountName = private AccountName of string

    module AccountName =
        let maxLength = 100
        let value (AccountName an) = an // required because AccountName is a private string
        let create (raw: string) : Result<AccountName, AppError> =
            let trimmed = raw.Trim()
            if String.IsNullOrWhiteSpace trimmed then
                Error(AccountNameIsEmpty raw)
            elif trimmed.Length > maxLength then
                Error(AccountNameTooLong(raw, maxLength))
            else
                Ok(AccountName trimmed)

    type AccountTypeNormalBalance =
        | Debit
        | Credit

    type AccountType =
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense

    module AccountType =
        let fromString (accountType: string) : Result<AccountType, AppError> =
            match accountType.Trim() with
            | "Asset" -> Ok Asset
            | "Liability" -> Ok Liability
            | "Equity" -> Ok Equity
            | "Revenue" -> Ok Revenue
            | "Expense" -> Ok Expense
            | _ -> Error(AccountTypeInvalid accountType)

        let toString (``type``: AccountType) : string =
            match ``type`` with
            | Asset -> "Asset"
            | Liability -> "Liability"
            | Equity -> "Equity"
            | Revenue -> "Revenue"
            | Expense -> "Expense"

        let normalBalance (``type``: AccountType) : AccountTypeNormalBalance =
            match ``type`` with
            | Asset
            | Expense -> Debit
            | Liability
            | Equity
            | Revenue -> Credit

    type AccountSubtype =
        | Cash
        | CurrentLiability
        | FixedAsset
        | Investment
        | LongTermLiability
        | OperatingExpense
        | OperatingRevenue
        | OtherRevenue
        | OtherExpense

    module AccountSubtype =
        let toString (subtype: AccountSubtype) : string =
            match subtype with
            | Cash -> "Cash"
            | CurrentLiability -> "CurrentLiability"
            | FixedAsset -> "FixedAsset"
            | Investment -> "Investment"
            | LongTermLiability -> "LongTermLiability"
            | OperatingRevenue -> "OperatingRevenue"
            | OperatingExpense -> "OperatingExpense"
            | OtherRevenue -> "OtherRevenue"
            | OtherExpense -> "OtherExpense"
        let fromString (subtype: string) : Result<AccountSubtype, AppError> =
            match subtype.Trim() with
            | "Cash" -> Ok Cash
            | "CurrentLiability" -> Ok CurrentLiability
            | "FixedAsset" -> Ok FixedAsset
            | "Investment" -> Ok Investment
            | "LongTermLiability" -> Ok LongTermLiability
            | "OperatingRevenue" -> Ok OperatingRevenue
            | "OperatingExpense" -> Ok OperatingExpense
            | "OtherRevenue" -> Ok OtherRevenue
            | "OtherExpense" -> Ok OtherExpense
            | _ -> Error(AccountSubtypeInvalid subtype)
        let validFor (subtype: AccountSubtype) : AccountType = // confirms that subtype A, B, C can only be associated to type Y
            match subtype with
            | Cash
            | FixedAsset
            | Investment -> Asset
            | CurrentLiability
            | LongTermLiability -> Liability
            | OperatingRevenue
            | OtherRevenue -> Revenue
            | OperatingExpense
            | OtherExpense -> Expense

        let validWith (``type``: AccountType) : AccountSubtype list = // confirms that type Y can only accept subtype A, B, C
            match ``type`` with
            | Asset -> [ Cash; FixedAsset; Investment ]
            | Liability -> [ CurrentLiability; LongTermLiability ]
            | Equity -> []
            | Revenue -> [ OperatingRevenue; OtherRevenue ]
            | Expense -> [ OperatingExpense; OtherExpense ]

        let validTypeSubtypeCombination (``type``: AccountType) (subtype: AccountSubtype option) : bool =
            match subtype with
            | None -> true
            | Some x -> validWith ``type`` |> List.contains x


    type AccountExternalReference = private AccountExternalReference of string

    module AccountExternalReference =
        let maxLength = 50
        let value (AccountExternalReference reference) = reference // required due to private value
        let create (raw: string) : Result<AccountExternalReference, AppError> =
            let trimmed = raw.Trim()
            if trimmed = String.Empty then
                Error(AccountExternalReferenceIsEmpty raw)
            elif trimmed.Length > maxLength then
                Error(AccountExternalReferenceTooLong(raw, maxLength))
            else
                Ok(AccountExternalReference trimmed)
