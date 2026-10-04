module Business.FinancialServices.Positions.PositionsError

open System
open NodaTime
open App.Utility.IAppError

type PositionsError =
    | PositionsAccountGroupIsEmpty of string
    | PositionsAccountGroupTooLong of string * int
    | PositionsActivePeriodExcludesSnapshots of string * LocalDate * LocalDate
    | PositionsBasisMethodNotAllowed of string * string * string
    | PositionsContributionBasisNegative of decimal
    | PositionsContributionBasisNotAllowed of string * string
    | PositionsDimensionValueAlreadyExists of string * string
    | PositionsDimensionValueIdDoesntExist of Guid
    | PositionsDimensionValueNameDoesntMatch of string * string
    | PositionsDimensionValueNameIsEmpty of string
    | PositionsDimensionValueNameTooLong of string * int
    | PositionsDisposalDateBeforeAcquisitionDate of LocalDate * LocalDate
    | PositionsHoldingAlreadyExists of string * string
    | PositionsHoldingDoesntExist of string * string
    | PositionsHoldingIdDoesntExist of Guid
    | PositionsInstitutionIsEmpty of string
    | PositionsInstitutionTooLong of string * int
    | PositionsInvalidBasisMethod of string
    | PositionsInvalidDimension of string
    | PositionsInvalidProvenance of string
    | PositionsInvalidPropertyUse of string
    | PositionsInvalidTaxTreatment of string
    | PositionsInvalidWealthGrouping of string
    | PositionsInvestmentAccountHasNoOwners of string
    | PositionsInvestmentAccountIdDoesntExist of Guid
    | PositionsInvestmentAccountNameAlreadyExists of string
    | PositionsInvestmentAccountNameDoesntMatch of string
    | PositionsInvestmentAccountNameIsEmpty of string
    | PositionsInvestmentAccountNameTooLong of string * int
    | PositionsInvestmentAccountOwnerRepeated of string * string
    | PositionsInvestmentAccountOwnersNotAllowed of string * string * int
    | PositionsInvestmentAccountUpdateNoOp
    | PositionsInvestmentLedgerAccountNotAssetInvestment of string * string * string option
    | PositionsLedgerAccountAlreadyLinked of string * string * string
    | PositionsMortgageAccountAlreadyLinked of string * string
    | PositionsMortgageAccountNotLiability of string * string
    | PositionsNetWorthDateOutsideFiscalPeriods of LocalDate
    | PositionsOwnershipExcludesValuations of string * LocalDate * LocalDate
    | PositionsPrimaryResidencesOverlap of string * string
    | PositionsPropertyHasNoOwners of string
    | PositionsPropertyIdDoesntExist of Guid
    | PositionsPropertyLedgerAccountNotAssetFixedAsset of string * string * string option
    | PositionsPropertyNameAlreadyExists of string
    | PositionsPropertyNameDoesntMatch of string
    | PositionsPropertyNameIsEmpty of string
    | PositionsPropertyNameTooLong of string * int
    | PositionsPropertyOwnerRepeated of string * string
    | PositionsPropertyUpdateNoOp
    | PositionsPurchaseBasisNotPositive of decimal
    | PositionsSecurityDimensionGivenTwice of string
    | PositionsSecurityIdDoesntExist of Guid
    | PositionsSecurityNameAlreadyExists of string
    | PositionsSecurityNameDoesntMatch of string
    | PositionsSecurityNameIsEmpty of string
    | PositionsSecurityNameTooLong of string * int
    | PositionsSecurityUpdateNoOp
    | PositionsSnapshotDateLaterThanCurrentDate of string * LocalDate
    | PositionsSnapshotDateOutsideActivePeriod of string * LocalDate
    | PositionsSnapshotDoesntExist of string * LocalDate
    | PositionsSnapshotLineCostBasisNegative of string * LocalDate * string * decimal
    | PositionsSnapshotLineMarketValueNegative of string * LocalDate * string * decimal
    | PositionsSnapshotLineOutsideTolerance of string * LocalDate * string * decimal * decimal
    | PositionsSnapshotLineQuantityNotPositive of string * LocalDate * string
    | PositionsSnapshotRepeatedInRequest of string * LocalDate
    | PositionsSnapshotSecurityNotHeld of string * string
    | PositionsSnapshotSecurityRepeated of string * LocalDate * string
    | PositionsTaxTreatmentChangeBreaksHoldings of string * string * string list
    | PositionsTickerAlreadyExists of string
    | PositionsTickerIsEmpty of string
    | PositionsTickerTooLong of string * int
    | PositionsValuationBasisIsEmpty of string
    | PositionsValuationBasisTooLong of string * int
    | PositionsValuationDateLaterThanCurrentDate of string * LocalDate
    | PositionsValuationDateOutsideOwnership of string * LocalDate
    | PositionsValuationDoesntExist of string * LocalDate
    | PositionsValuationValueNotPositive of decimal
    | PositionsWealthHistoryEndBeforeBegin of LocalDate * LocalDate

    interface IAppError with
        member this.DomainName = nameof PositionsError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | PositionsAccountGroupIsEmpty raw -> $"Account group cannot be empty. Provided account group is \"{raw}\"."
            | PositionsAccountGroupTooLong(raw, max) -> $"Account group cannot exceed {max} characters. Provided account group is \"{raw}\"."
            | PositionsActivePeriodExcludesSnapshots(account, earliest, latest) -> $"Investment Account \"{account}\" would no longer be active on the dates of some of its snapshots, the earliest {earliest} and the latest {latest}."
            | PositionsBasisMethodNotAllowed(account, security, taxTreatment) -> $"The Holding of \"{security}\" in Investment Account \"{account}\" breaks the basis method rule for a {taxTreatment} account: a Taxable account's Holding has a basis method, and any other account's Holding has none."
            | PositionsContributionBasisNegative raw -> $"A contribution basis cannot be negative. Provided contribution basis is {raw}."
            | PositionsContributionBasisNotAllowed(account, taxTreatment) -> $"Investment Account \"{account}\" is {taxTreatment}; only a Roth account's snapshot carries a contribution basis."
            | PositionsDimensionValueAlreadyExists(dimension, name) -> $"A Dimension Value named \"{name}\" already exists in dimension {dimension}."
            | PositionsDimensionValueIdDoesntExist uuid -> $"No Dimension Value exists with ID {uuid}."
            | PositionsDimensionValueNameDoesntMatch(dimension, name) -> $"No Dimension Value is named \"{name}\" in dimension {dimension}."
            | PositionsDimensionValueNameIsEmpty raw -> $"Dimension Value name cannot be empty. Provided name is \"{raw}\"."
            | PositionsDimensionValueNameTooLong(raw, max) -> $"Dimension Value name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsDisposalDateBeforeAcquisitionDate(acquisition, disposal) -> $"A Property's disposal date ({disposal}) cannot be before its acquisition date ({acquisition})."
            | PositionsHoldingAlreadyExists(account, security) -> $"Investment Account \"{account}\" already has a Holding of \"{security}\"."
            | PositionsHoldingDoesntExist(account, security) -> $"Investment Account \"{account}\" has no Holding of \"{security}\"."
            | PositionsHoldingIdDoesntExist uuid -> $"No Holding exists with ID {uuid}."
            | PositionsInstitutionIsEmpty raw -> $"Institution cannot be empty. Provided institution is \"{raw}\"."
            | PositionsInstitutionTooLong(raw, max) -> $"Institution cannot exceed {max} characters. Provided institution is \"{raw}\"."
            | PositionsInvalidBasisMethod raw -> $"Invalid basis method of \"{raw}\"."
            | PositionsInvalidDimension raw -> $"Invalid dimension of \"{raw}\"."
            | PositionsInvalidProvenance raw -> $"Invalid provenance of \"{raw}\"."
            | PositionsInvalidPropertyUse raw -> $"Invalid property use of \"{raw}\"."
            | PositionsInvalidTaxTreatment raw -> $"Invalid tax treatment of \"{raw}\"."
            | PositionsInvalidWealthGrouping raw -> $"Invalid investment wealth grouping of \"{raw}\"."
            | PositionsInvestmentAccountHasNoOwners account -> $"Investment Account \"{account}\" must have at least one owner."
            | PositionsInvestmentAccountIdDoesntExist uuid -> $"No Investment Account exists with ID {uuid}."
            | PositionsInvestmentAccountNameAlreadyExists name -> $"An Investment Account named \"{name}\" already exists."
            | PositionsInvestmentAccountNameDoesntMatch name -> $"No Investment Account is named \"{name}\"."
            | PositionsInvestmentAccountNameIsEmpty raw -> $"Investment Account name cannot be empty. Provided name is \"{raw}\"."
            | PositionsInvestmentAccountNameTooLong(raw, max) -> $"Investment Account name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsInvestmentAccountOwnerRepeated(account, person) -> $"Investment Account \"{account}\" names Person \"{person}\" as an owner more than once."
            | PositionsInvestmentAccountOwnersNotAllowed(account, taxTreatment, count) -> $"Investment Account \"{account}\" is {taxTreatment} and so must have exactly one owner; it was given {count}."
            | PositionsInvestmentAccountUpdateNoOp -> "Updating the Investment Account record failed because at least one updatable parameter must be set."
            | PositionsInvestmentLedgerAccountNotAssetInvestment(code, accountType, subtype) ->
                let shownSubtype = subtype |> Option.defaultValue "none"
                $"Ledger account {code} cannot stand for an Investment Account: it must be an Asset account of subtype Investment, and it is {accountType} with subtype {shownSubtype}."
            | PositionsLedgerAccountAlreadyLinked(code, kind, name) -> $"Ledger account {code} already stands for {kind} \"{name}\"."
            | PositionsMortgageAccountAlreadyLinked(code, property) -> $"Ledger account {code} is already a mortgage account of Property \"{property}\"."
            | PositionsMortgageAccountNotLiability(code, accountType) -> $"Ledger account {code} cannot be a mortgage account: it must be a Liability account, and it is {accountType}."
            | PositionsNetWorthDateOutsideFiscalPeriods date -> $"Net worth cannot be computed as of {date}: the date falls in no fiscal period."
            | PositionsOwnershipExcludesValuations(property, earliest, latest) -> $"Property \"{property}\" would have Valuations outside its ownership, the earliest {earliest} and the latest {latest}."
            | PositionsPrimaryResidencesOverlap(first, second) -> $"Properties \"{first}\" and \"{second}\" would both be a primary residence owned on the same date."
            | PositionsPropertyHasNoOwners property -> $"Property \"{property}\" must have at least one owner."
            | PositionsPropertyIdDoesntExist uuid -> $"No Property exists with ID {uuid}."
            | PositionsPropertyLedgerAccountNotAssetFixedAsset(code, accountType, subtype) ->
                let shownSubtype = subtype |> Option.defaultValue "none"
                $"Ledger account {code} cannot stand for a Property: it must be an Asset account of subtype FixedAsset, and it is {accountType} with subtype {shownSubtype}."
            | PositionsPropertyNameAlreadyExists name -> $"A Property named \"{name}\" already exists."
            | PositionsPropertyNameDoesntMatch name -> $"No Property is named \"{name}\"."
            | PositionsPropertyNameIsEmpty raw -> $"Property name cannot be empty. Provided name is \"{raw}\"."
            | PositionsPropertyNameTooLong(raw, max) -> $"Property name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsPropertyOwnerRepeated(property, person) -> $"Property \"{property}\" names Person \"{person}\" as an owner more than once."
            | PositionsPropertyUpdateNoOp -> "Updating the Property record failed because at least one updatable parameter must be set."
            | PositionsPurchaseBasisNotPositive raw -> $"A purchase basis must be greater than zero. Provided purchase basis is {raw}."
            | PositionsSecurityDimensionGivenTwice dimension -> $"A Security can hold at most one value in dimension {dimension}, and was given more than one."
            | PositionsSecurityIdDoesntExist uuid -> $"No Security exists with ID {uuid}."
            | PositionsSecurityNameAlreadyExists name -> $"A Security named \"{name}\" already exists."
            | PositionsSecurityNameDoesntMatch name -> $"No Security is named \"{name}\"."
            | PositionsSecurityNameIsEmpty raw -> $"Security name cannot be empty. Provided name is \"{raw}\"."
            | PositionsSecurityNameTooLong(raw, max) -> $"Security name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsSecurityUpdateNoOp -> "Updating the Security record failed because at least one updatable parameter must be set."
            | PositionsSnapshotDateLaterThanCurrentDate(account, date) -> $"A snapshot of Investment Account \"{account}\" cannot be dated {date}, later than the current date."
            | PositionsSnapshotDateOutsideActivePeriod(account, date) -> $"Investment Account \"{account}\" is not active on {date}, so it cannot have a snapshot that day."
            | PositionsSnapshotDoesntExist(account, date) -> $"Investment Account \"{account}\" has no snapshot dated {date}."
            | PositionsSnapshotLineCostBasisNegative(account, date, security, raw) -> $"The {date} snapshot of Investment Account \"{account}\" reports a negative cost basis ({raw}) for \"{security}\"."
            | PositionsSnapshotLineMarketValueNegative(account, date, security, raw) -> $"The {date} snapshot of Investment Account \"{account}\" reports a negative market value ({raw}) for \"{security}\"."
            | PositionsSnapshotLineOutsideTolerance(account, date, security, product, marketValue) -> $"The {date} snapshot of Investment Account \"{account}\" reports \"{security}\" at a market value of {marketValue}, but its quantity times its price is {product}, more than 0.05 away."
            | PositionsSnapshotLineQuantityNotPositive(account, date, security) -> $"The {date} snapshot of Investment Account \"{account}\" lists \"{security}\" with a quantity that is not greater than zero; a security not held is omitted."
            | PositionsSnapshotRepeatedInRequest(account, date) -> $"Investment Account \"{account}\" and date {date} appear more than once in one request."
            | PositionsSnapshotSecurityNotHeld(account, security) -> $"Investment Account \"{account}\" has no Holding of \"{security}\". Create the Holding first."
            | PositionsSnapshotSecurityRepeated(account, date, security) -> $"The {date} snapshot of Investment Account \"{account}\" lists \"{security}\" on more than one line."
            | PositionsTaxTreatmentChangeBreaksHoldings(account, taxTreatment, securities) -> $"""Investment Account "{account}" cannot become {taxTreatment}: its Holdings of {securities |> String.concat ", "} would break the basis method rule."""
            | PositionsTickerAlreadyExists ticker -> $"A Security with ticker \"{ticker}\" already exists."
            | PositionsTickerIsEmpty raw -> $"Ticker cannot be empty. Provided ticker is \"{raw}\"."
            | PositionsTickerTooLong(raw, max) -> $"Ticker cannot exceed {max} characters. Provided ticker is \"{raw}\"."
            | PositionsValuationBasisIsEmpty raw -> $"Valuation basis cannot be empty. Provided basis is \"{raw}\"."
            | PositionsValuationBasisTooLong(raw, max) -> $"Valuation basis cannot exceed {max} characters. Provided basis is \"{raw}\"."
            | PositionsValuationDateLaterThanCurrentDate(property, date) -> $"A Valuation of Property \"{property}\" cannot be dated {date}, later than the current date."
            | PositionsValuationDateOutsideOwnership(property, date) -> $"A Valuation of Property \"{property}\" cannot be dated {date}, outside the dates it was owned."
            | PositionsValuationDoesntExist(property, date) -> $"Property \"{property}\" has no Valuation dated {date}."
            | PositionsValuationValueNotPositive raw -> $"A valuation must be greater than zero. Provided value is {raw}."
            | PositionsWealthHistoryEndBeforeBegin(beginDate, endDate) -> $"The end date ({endDate}) cannot be earlier than the begin date ({beginDate})."

let toMessage (e: PositionsError) = (e :> IAppError).ToMessage()
let toAppError (e: PositionsError) : IAppError = e :> IAppError
let error (e: PositionsError) : Result<'T, IAppError> = Error (e :> IAppError)
