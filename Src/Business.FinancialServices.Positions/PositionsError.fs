module Business.FinancialServices.Positions.PositionsError

open System
open NodaTime
open App.Utility.IAppError

/// What is wrong with an Activity whose kind fixes whether it names a Security and carries a quantity.
type ActivityShapeProblem =
    | SecurityMissing
    | QuantityMissing
    | QuantityNotAllowed
    | QuantityWithoutSecurity

type PositionsError =
    | PositionsAccountGroupIsEmpty of string
    | PositionsAccountGroupTooLong of string * int
    | PositionsAccountSnapshotIdDoesntExist of Guid
    | PositionsAccountSnapshotUpdateNoOp
    | PositionsActivePeriodExcludesRecords of string * (LocalDate * LocalDate) option * (LocalDate * LocalDate) option
    | PositionsActivityAmountNegative of string * LocalDate * string * decimal
    | PositionsActivityDateOutsideActivePeriod of string * LocalDate
    | PositionsActivityDescriptionIsEmpty of string
    | PositionsActivityDescriptionTooLong of string * int
    | PositionsActivityListEndBeforeBegin of LocalDate * LocalDate
    | PositionsActivityOutsideRange of string * LocalDate * LocalDate * LocalDate
    | PositionsActivityPriceWithoutQuantity of string * LocalDate * string
    | PositionsActivityQuantityNotPositive of string * LocalDate * string
    | PositionsActivityRangeEndBeforeBegin of string * LocalDate * LocalDate
    | PositionsActivityRangeEndsAfterCurrentDate of string * LocalDate * LocalDate
    | PositionsActivityRangeListIsEmpty
    | PositionsActivityRangesOverlap of string * (LocalDate * LocalDate) * (LocalDate * LocalDate)
    | PositionsActivitySecurityNotHeld of string * string
    | PositionsActivityShapeInvalid of string * LocalDate * string * ActivityShapeProblem
    | PositionsActivitySourceIsEmpty of string
    | PositionsActivitySourceTooLong of string * int
    | PositionsAssetAccountAlreadyLinked of string * string
    | PositionsBasisMethodNotAllowed of string * string * string
    | PositionsContributionBasisNegative of decimal
    | PositionsContributionBasisNotAllowed of string * string
    | PositionsDimensionValueAlreadyExists of string * string
    | PositionsDimensionValueIdDoesntExist of Guid
    | PositionsDimensionValueNameDoesntMatch of string * string
    | PositionsDimensionValueNameIsEmpty of string
    | PositionsDimensionValueNameTooLong of string * int
    | PositionsDimensionValueUpdateNoOp
    | PositionsDisposalDateBeforeAcquisitionDate of LocalDate * LocalDate
    | PositionsHoldingAlreadyExists of string * string
    | PositionsHoldingDoesntExist of string * string
    | PositionsHoldingIdDoesntExist of Guid
    | PositionsHoldingReferencedByActivities of string * string
    | PositionsHoldingReferencedBySnapshots of string * string
    | PositionsHoldingUpdateNoOp
    | PositionsInstitutionIsEmpty of string
    | PositionsInstitutionTooLong of string * int
    | PositionsInvalidActivityKind of string
    | PositionsInvalidBasisMethod of string
    | PositionsInvalidDimension of string
    | PositionsInvalidPropertyUse of string
    | PositionsInvalidProvenance of string
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
    | PositionsInvestmentLedgerAccountAlreadyLinked of string * string
    | PositionsInvestmentLedgerAccountNotAssetInvestment of string * string * string option
    | PositionsLotAcquiredAfterSnapshot of string * LocalDate * string * LocalDate
    | PositionsLotCostBasisNegative of string * LocalDate * string * decimal
    | PositionsLotQuantityNotPositive of string * LocalDate * string
    | PositionsLotsDontSumToLineQuantity of string * LocalDate * string * decimal * decimal
    | PositionsLotsNotAllowed of string * string
    | PositionsMortgageAccountAlreadyLinked of string * string
    | PositionsMortgageAccountNotLiability of string * string
    | PositionsNetWorthDateOutsideFiscalPeriods of LocalDate
    | PositionsNetWorthHistoryEndBeforeBegin of LocalDate * LocalDate
    | PositionsOwnershipExcludesValuations of string * LocalDate * LocalDate
    | PositionsPreLedgerAccountIsPropertyAsset of string * string
    | PositionsPreLedgerAccountLinkedToInvestmentAccount of string * string
    | PositionsPreLedgerAccountTypeNotAllowed of string * string
    | PositionsPreLedgerBalanceDoesntExist of string * LocalDate
    | PositionsPreLedgerBalanceListIsEmpty
    | PositionsPreLedgerBalanceRepeatedInRequest of string * LocalDate
    | PositionsPreLedgerDateNotBeforeLedger of string * LocalDate
    | PositionsPreLedgerListEndBeforeBegin of LocalDate * LocalDate
    | PositionsPreLedgerNoFiscalPeriod of string * LocalDate
    | PositionsPrimaryResidencesOverlap of string * string
    | PositionsPropertyAssetAccountRepeated of string * string
    | PositionsPropertyHasNoOwners of string
    | PositionsPropertyHasValuations of string
    | PositionsPropertyIdDoesntExist of Guid
    | PositionsPropertyLedgerAccountNotAssetFixedAsset of string * string * string option
    | PositionsPropertyMortgageAccountRepeated of string * string
    | PositionsPropertyNameAlreadyExists of string
    | PositionsPropertyNameDoesntMatch of string
    | PositionsPropertyNameIsEmpty of string
    | PositionsPropertyNameTooLong of string * int
    | PositionsPropertyOwnerRepeated of string * string
    | PositionsPropertyUpdateNoOp
    | PositionsPurchaseBasisNotPositive of decimal
    | PositionsRollForwardDatesNotInOrder of string * LocalDate * LocalDate
    | PositionsSecurityDimensionGivenTwice of string
    | PositionsSecurityIdDoesntExist of Guid
    | PositionsSecurityNameAlreadyExists of string
    | PositionsSecurityNameDoesntMatch of string
    | PositionsSecurityNameIsEmpty of string
    | PositionsSecurityNameTooLong of string * int
    | PositionsSecurityUpdateNoOp
    | PositionsSnapshotDateLaterThanCurrentDate of string * LocalDate
    | PositionsSnapshotDateOutsideActivePeriod of string * LocalDate
    | PositionsSnapshotDatesEndBeforeBegin of LocalDate * LocalDate
    | PositionsSnapshotDoesntExist of string * LocalDate
    | PositionsSnapshotLineCostBasisNegative of string * LocalDate * string * decimal
    | PositionsSnapshotLineMarketValueNegative of string * LocalDate * string * decimal
    | PositionsSnapshotLineOutsideTolerance of string * LocalDate * string * decimal * decimal
    | PositionsSnapshotLineQuantityNotPositive of string * LocalDate * string
    | PositionsSnapshotListIsEmpty
    | PositionsSnapshotRepeatedInRequest of string * LocalDate
    | PositionsSnapshotSecurityNotHeld of string * string
    | PositionsSnapshotSecurityRepeated of string * LocalDate * string
    | PositionsTaxTreatmentChangeBreaksHoldings of string * string * string list
    | PositionsTaxTreatmentChangeStrandsContributionBasis of string * LocalDate * LocalDate
    | PositionsTaxTreatmentChangeStrandsLots of string * LocalDate * LocalDate
    | PositionsTickerAlreadyExists of string
    | PositionsTickerIsEmpty of string
    | PositionsTickerTooLong of string * int
    | PositionsValuationBasisIsEmpty of string
    | PositionsValuationBasisTooLong of string * int
    | PositionsValuationDateLaterThanCurrentDate of string * LocalDate
    | PositionsValuationDateOutsideOwnership of string * LocalDate
    | PositionsValuationDoesntExist of string * LocalDate
    | PositionsValuationIdDoesntExist of Guid
    | PositionsValuationUpdateNoOp
    | PositionsValuationValueNotPositive of decimal
    | PositionsWealthHistoryEndBeforeBegin of LocalDate * LocalDate

    interface IAppError with
        member this.DomainName = nameof PositionsError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | PositionsAccountGroupIsEmpty raw -> $"Account group cannot be empty. Provided account group is \"{raw}\"."
            | PositionsAccountGroupTooLong(raw, max) -> $"Account group cannot exceed {max} characters. Provided account group is \"{raw}\"."
            | PositionsAccountSnapshotIdDoesntExist uuid -> $"No Account Snapshot exists with ID {uuid}."
            | PositionsAccountSnapshotUpdateNoOp -> "Updating the Account Snapshot record failed because at least one updatable parameter must be set."
            | PositionsActivePeriodExcludesRecords(account, snapshots, activities) ->
                let described what dates =
                    dates |> Option.map (fun (earliest: LocalDate, latest: LocalDate) -> $"{what} dated from {earliest} to {latest}")
                let offending = [ described "snapshots" snapshots; described "activities" activities ] |> List.choose id
                $"""Investment Account "{account}" would no longer be active on the dates of some of its records: {offending |> String.concat " and "}."""
            | PositionsActivityAmountNegative(account, date, kind, raw) -> $"The {kind} activity of Investment Account \"{account}\" dated {date} has a negative amount ({raw}); the kind says which way the money moved."
            | PositionsActivityDateOutsideActivePeriod(account, date) -> $"Investment Account \"{account}\" is not active on {date}, so it cannot have an activity that day."
            | PositionsActivityDescriptionIsEmpty raw -> $"Activity description cannot be empty. Provided description is \"{raw}\"."
            | PositionsActivityDescriptionTooLong(raw, max) -> $"Activity description cannot exceed {max} characters. Provided description is \"{raw}\"."
            | PositionsActivityListEndBeforeBegin(beginDate, endDate) -> $"The end date ({endDate}) cannot be earlier than the begin date ({beginDate})."
            | PositionsActivityOutsideRange(account, beginDate, endDate, date) -> $"An activity of Investment Account \"{account}\" is dated {date}, outside the range supplied for it, {beginDate} to {endDate}."
            | PositionsActivityPriceWithoutQuantity(account, date, kind) -> $"The {kind} activity of Investment Account \"{account}\" dated {date} carries a price but no quantity."
            | PositionsActivityQuantityNotPositive(account, date, kind) -> $"The {kind} activity of Investment Account \"{account}\" dated {date} carries a quantity that is not greater than zero."
            | PositionsActivityRangeEndBeforeBegin(account, beginDate, endDate) -> $"The activity range of Investment Account \"{account}\" ends ({endDate}) before it begins ({beginDate})."
            | PositionsActivityRangeEndsAfterCurrentDate(account, beginDate, endDate) -> $"The activity range of Investment Account \"{account}\", {beginDate} to {endDate}, ends later than the current date."
            | PositionsActivityRangeListIsEmpty -> "At least one account's activity range must be given to record."
            | PositionsActivityRangesOverlap(account, (firstBegin, firstEnd), (secondBegin, secondEnd)) -> $"Investment Account \"{account}\" is given two overlapping activity ranges in one request: {firstBegin} to {firstEnd}, and {secondBegin} to {secondEnd}."
            | PositionsActivitySecurityNotHeld(account, security) -> $"Investment Account \"{account}\" has no Holding of \"{security}\", so an activity cannot name it. Create the Holding first."
            | PositionsActivityShapeInvalid(account, date, kind, problem) ->
                let what =
                    match problem with
                    | SecurityMissing -> "names no Security, and one is required"
                    | QuantityMissing -> "carries no quantity, and one is required"
                    | QuantityNotAllowed -> "carries a quantity, which this kind never does"
                    | QuantityWithoutSecurity -> "carries a quantity without naming a Security"
                $"The {kind} activity of Investment Account \"{account}\" dated {date} {what}."
            | PositionsActivitySourceIsEmpty raw -> $"Activity source cannot be empty when given. Provided source is \"{raw}\"."
            | PositionsActivitySourceTooLong(raw, max) -> $"Activity source cannot exceed {max} characters. Provided source is \"{raw}\"."
            | PositionsAssetAccountAlreadyLinked(code, property) -> $"Ledger account {code} is already an asset account of Property \"{property}\"."
            | PositionsBasisMethodNotAllowed(account, security, taxTreatment) -> $"The Holding of \"{security}\" in Investment Account \"{account}\" breaks the basis method rule for a {taxTreatment} account: a Taxable account's Holding has a basis method, and any other account's Holding has none."
            | PositionsContributionBasisNegative raw -> $"A contribution basis cannot be negative. Provided contribution basis is {raw}."
            | PositionsContributionBasisNotAllowed(account, taxTreatment) -> $"Investment Account \"{account}\" is {taxTreatment}; only a Roth account's snapshot carries a contribution basis."
            | PositionsDimensionValueAlreadyExists(dimension, name) -> $"A Dimension Value named \"{name}\" already exists in dimension {dimension}."
            | PositionsDimensionValueIdDoesntExist uuid -> $"No Dimension Value exists with ID {uuid}."
            | PositionsDimensionValueNameDoesntMatch(dimension, name) -> $"No Dimension Value is named \"{name}\" in dimension {dimension}."
            | PositionsDimensionValueNameIsEmpty raw -> $"Dimension Value name cannot be empty. Provided name is \"{raw}\"."
            | PositionsDimensionValueNameTooLong(raw, max) -> $"Dimension Value name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsDimensionValueUpdateNoOp -> "Updating the Dimension Value record failed because at least one updatable parameter must be set."
            | PositionsDisposalDateBeforeAcquisitionDate(acquisition, disposal) -> $"A Property's disposal date ({disposal}) cannot be before its acquisition date ({acquisition})."
            | PositionsHoldingAlreadyExists(account, security) -> $"Investment Account \"{account}\" already has a Holding of \"{security}\"."
            | PositionsHoldingDoesntExist(account, security) -> $"Investment Account \"{account}\" has no Holding of \"{security}\"."
            | PositionsHoldingIdDoesntExist uuid -> $"No Holding exists with ID {uuid}."
            | PositionsHoldingReferencedByActivities(account, security) -> $"The Holding of \"{security}\" in Investment Account \"{account}\" cannot be deleted: an Activity names it."
            | PositionsHoldingReferencedBySnapshots(account, security) -> $"The Holding of \"{security}\" in Investment Account \"{account}\" cannot be deleted: an Account Snapshot line references it."
            | PositionsHoldingUpdateNoOp -> "Updating the Holding record failed because at least one updatable parameter must be set."
            | PositionsInstitutionIsEmpty raw -> $"Institution cannot be empty. Provided institution is \"{raw}\"."
            | PositionsInstitutionTooLong(raw, max) -> $"Institution cannot exceed {max} characters. Provided institution is \"{raw}\"."
            | PositionsInvalidActivityKind raw -> $"Invalid activity kind of \"{raw}\"."
            | PositionsInvalidBasisMethod raw -> $"Invalid basis method of \"{raw}\"."
            | PositionsInvalidDimension raw -> $"Invalid dimension of \"{raw}\"."
            | PositionsInvalidPropertyUse raw -> $"Invalid property use of \"{raw}\"."
            | PositionsInvalidProvenance raw -> $"Invalid provenance of \"{raw}\"."
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
            | PositionsInvestmentLedgerAccountAlreadyLinked(code, account) -> $"Ledger account {code} already stands for Investment Account \"{account}\"."
            | PositionsInvestmentLedgerAccountNotAssetInvestment(code, accountType, subtype) ->
                let shownSubtype = subtype |> Option.defaultValue "none"
                $"Ledger account {code} cannot stand for an Investment Account: it must be an Asset account of subtype Investment, and it is {accountType} with subtype {shownSubtype}."
            | PositionsLotAcquiredAfterSnapshot(account, date, security, acquired) -> $"The {date} snapshot of Investment Account \"{account}\" reports a lot of \"{security}\" acquired on {acquired}, after the snapshot date."
            | PositionsLotCostBasisNegative(account, date, security, raw) -> $"The {date} snapshot of Investment Account \"{account}\" reports a lot of \"{security}\" with a negative cost basis ({raw})."
            | PositionsLotQuantityNotPositive(account, date, security) -> $"The {date} snapshot of Investment Account \"{account}\" reports a lot of \"{security}\" with a quantity that is not greater than zero."
            | PositionsLotsDontSumToLineQuantity(account, date, security, lotSum, lineQuantity) -> $"The {date} snapshot of Investment Account \"{account}\" reports lots of \"{security}\" totalling {lotSum}, but the line's quantity is {lineQuantity}."
            | PositionsLotsNotAllowed(account, security) -> $"Investment Account \"{account}\" is not Taxable, so its line for \"{security}\" cannot carry lots."
            | PositionsMortgageAccountAlreadyLinked(code, property) -> $"Ledger account {code} is already a mortgage account of Property \"{property}\"."
            | PositionsMortgageAccountNotLiability(code, accountType) -> $"Ledger account {code} cannot be a mortgage account: it must be a Liability account, and it is {accountType}."
            | PositionsNetWorthDateOutsideFiscalPeriods date -> $"Net worth cannot be computed as of {date}: the date falls in no fiscal period and is not before the first one."
            | PositionsNetWorthHistoryEndBeforeBegin(beginDate, endDate) -> $"The end date ({endDate}) cannot be earlier than the begin date ({beginDate})."
            | PositionsOwnershipExcludesValuations(property, earliest, latest) -> $"Property \"{property}\" would have Valuations outside its ownership, the earliest {earliest} and the latest {latest}."
            | PositionsPreLedgerAccountIsPropertyAsset(code, property) -> $"Ledger account {code} is an asset account of Property \"{property}\", so it cannot carry a pre-ledger balance."
            | PositionsPreLedgerAccountLinkedToInvestmentAccount(code, account) -> $"Ledger account {code} stands for Investment Account \"{account}\", so it cannot carry a pre-ledger balance."
            | PositionsPreLedgerAccountTypeNotAllowed(code, accountType) -> $"Ledger account {code} is {accountType}; only an Asset or Liability account can carry a pre-ledger balance."
            | PositionsPreLedgerBalanceDoesntExist(code, date) -> $"Ledger account {code} has no pre-ledger balance dated {date}."
            | PositionsPreLedgerBalanceListIsEmpty -> "At least one pre-ledger balance must be given to record."
            | PositionsPreLedgerBalanceRepeatedInRequest(code, date) -> $"Ledger account {code} and date {date} appear more than once in one request."
            | PositionsPreLedgerDateNotBeforeLedger(code, date) -> $"A pre-ledger balance of ledger account {code} cannot be dated {date}: it must be earlier than the start of the earliest fiscal period."
            | PositionsPreLedgerListEndBeforeBegin(beginDate, endDate) -> $"The end date ({endDate}) cannot be earlier than the begin date ({beginDate})."
            | PositionsPreLedgerNoFiscalPeriod(code, date) -> $"A pre-ledger balance of ledger account {code} dated {date} cannot be recorded: no fiscal period exists, so no date is before the ledger."
            | PositionsPrimaryResidencesOverlap(first, second) -> $"Properties \"{first}\" and \"{second}\" would both be a primary residence owned on the same date."
            | PositionsPropertyAssetAccountRepeated(property, code) -> $"Property \"{property}\" names ledger account {code} as an asset account more than once."
            | PositionsPropertyHasNoOwners property -> $"Property \"{property}\" must have at least one owner."
            | PositionsPropertyHasValuations property -> $"Property \"{property}\" cannot be deleted: it has Valuations."
            | PositionsPropertyIdDoesntExist uuid -> $"No Property exists with ID {uuid}."
            | PositionsPropertyLedgerAccountNotAssetFixedAsset(code, accountType, subtype) ->
                let shownSubtype = subtype |> Option.defaultValue "none"
                $"Ledger account {code} cannot stand for a Property: it must be an Asset account of subtype FixedAsset, and it is {accountType} with subtype {shownSubtype}."
            | PositionsPropertyMortgageAccountRepeated(property, code) -> $"Property \"{property}\" names ledger account {code} as a mortgage account more than once."
            | PositionsPropertyNameAlreadyExists name -> $"A Property named \"{name}\" already exists."
            | PositionsPropertyNameDoesntMatch name -> $"No Property is named \"{name}\"."
            | PositionsPropertyNameIsEmpty raw -> $"Property name cannot be empty. Provided name is \"{raw}\"."
            | PositionsPropertyNameTooLong(raw, max) -> $"Property name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsPropertyOwnerRepeated(property, person) -> $"Property \"{property}\" names Person \"{person}\" as an owner more than once."
            | PositionsPropertyUpdateNoOp -> "Updating the Property record failed because at least one updatable parameter must be set."
            | PositionsPurchaseBasisNotPositive raw -> $"A purchase basis must be greater than zero. Provided purchase basis is {raw}."
            | PositionsRollForwardDatesNotInOrder(account, firstDate, secondDate) -> $"Investment Account \"{account}\" cannot be rolled forward from {firstDate} to {secondDate}: the first date must be earlier than the second."
            | PositionsSecurityDimensionGivenTwice dimension -> $"A Security can hold at most one value in dimension {dimension}, and was given more than one."
            | PositionsSecurityIdDoesntExist uuid -> $"No Security exists with ID {uuid}."
            | PositionsSecurityNameAlreadyExists name -> $"A Security named \"{name}\" already exists."
            | PositionsSecurityNameDoesntMatch name -> $"No Security is named \"{name}\"."
            | PositionsSecurityNameIsEmpty raw -> $"Security name cannot be empty. Provided name is \"{raw}\"."
            | PositionsSecurityNameTooLong(raw, max) -> $"Security name cannot exceed {max} characters. Provided name is \"{raw}\"."
            | PositionsSecurityUpdateNoOp -> "Updating the Security record failed because at least one updatable parameter must be set."
            | PositionsSnapshotDateLaterThanCurrentDate(account, date) -> $"A snapshot of Investment Account \"{account}\" cannot be dated {date}, later than the current date."
            | PositionsSnapshotDateOutsideActivePeriod(account, date) -> $"Investment Account \"{account}\" is not active on {date}, so it cannot have a snapshot that day."
            | PositionsSnapshotDatesEndBeforeBegin(beginDate, endDate) -> $"The end date ({endDate}) cannot be earlier than the begin date ({beginDate})."
            | PositionsSnapshotDoesntExist(account, date) -> $"Investment Account \"{account}\" has no snapshot dated {date}."
            | PositionsSnapshotLineCostBasisNegative(account, date, security, raw) -> $"The {date} snapshot of Investment Account \"{account}\" reports a negative cost basis ({raw}) for \"{security}\"."
            | PositionsSnapshotLineMarketValueNegative(account, date, security, raw) -> $"The {date} snapshot of Investment Account \"{account}\" reports a negative market value ({raw}) for \"{security}\"."
            | PositionsSnapshotLineOutsideTolerance(account, date, security, product, marketValue) -> $"The {date} snapshot of Investment Account \"{account}\" reports \"{security}\" at a market value of {marketValue}, but its quantity times its price is {product}, more than 0.05 away."
            | PositionsSnapshotLineQuantityNotPositive(account, date, security) -> $"The {date} snapshot of Investment Account \"{account}\" lists \"{security}\" with a quantity that is not greater than zero; a security not held is omitted."
            | PositionsSnapshotListIsEmpty -> "At least one Account Snapshot must be given to record."
            | PositionsSnapshotRepeatedInRequest(account, date) -> $"Investment Account \"{account}\" and date {date} appear more than once in one request."
            | PositionsSnapshotSecurityNotHeld(account, security) -> $"Investment Account \"{account}\" has no Holding of \"{security}\". Create the Holding first."
            | PositionsSnapshotSecurityRepeated(account, date, security) -> $"The {date} snapshot of Investment Account \"{account}\" lists \"{security}\" on more than one line."
            | PositionsTaxTreatmentChangeBreaksHoldings(account, taxTreatment, securities) -> $"""Investment Account "{account}" cannot become {taxTreatment}: its Holdings of {securities |> String.concat ", "} would break the basis method rule."""
            | PositionsTaxTreatmentChangeStrandsContributionBasis(account, earliest, latest) -> $"Investment Account \"{account}\" cannot stop being Roth: its snapshots carry a contribution basis, the earliest dated {earliest} and the latest {latest}."
            | PositionsTaxTreatmentChangeStrandsLots(account, earliest, latest) -> $"Investment Account \"{account}\" cannot stop being Taxable: its snapshot lines carry lots, the earliest dated {earliest} and the latest {latest}."
            | PositionsTickerAlreadyExists ticker -> $"A Security with ticker \"{ticker}\" already exists."
            | PositionsTickerIsEmpty raw -> $"Ticker cannot be empty. Provided ticker is \"{raw}\"."
            | PositionsTickerTooLong(raw, max) -> $"Ticker cannot exceed {max} characters. Provided ticker is \"{raw}\"."
            | PositionsValuationBasisIsEmpty raw -> $"Valuation basis cannot be empty. Provided basis is \"{raw}\"."
            | PositionsValuationBasisTooLong(raw, max) -> $"Valuation basis cannot exceed {max} characters. Provided basis is \"{raw}\"."
            | PositionsValuationDateLaterThanCurrentDate(property, date) -> $"A Valuation of Property \"{property}\" cannot be dated {date}, later than the current date."
            | PositionsValuationDateOutsideOwnership(property, date) -> $"A Valuation of Property \"{property}\" cannot be dated {date}, outside the dates it was owned."
            | PositionsValuationDoesntExist(property, date) -> $"Property \"{property}\" has no Valuation dated {date}."
            | PositionsValuationIdDoesntExist uuid -> $"No Valuation exists with ID {uuid}."
            | PositionsValuationUpdateNoOp -> "Updating the Valuation record failed because at least one updatable parameter must be set."
            | PositionsValuationValueNotPositive raw -> $"A valuation must be greater than zero. Provided value is {raw}."
            | PositionsWealthHistoryEndBeforeBegin(beginDate, endDate) -> $"The end date ({endDate}) cannot be earlier than the begin date ({beginDate})."

let toMessage (e: PositionsError) = (e :> IAppError).ToMessage()
let toAppError (e: PositionsError) : IAppError = e :> IAppError
let error (e: PositionsError) : Result<'T, IAppError> = Error (e :> IAppError)
