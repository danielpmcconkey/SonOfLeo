module Business.FinancialServices.Positions.PositionsComponent

open System
open NodaTime
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError

let private trimmedAndBounded
    (maxLength: int)
    (emptyError: string -> PositionsError)
    (tooLongError: string * int -> PositionsError)
    (raw: string)
    : Result<string, IAppError> =
    let trimmed = raw.Trim()
    if String.IsNullOrWhiteSpace trimmed then error (emptyError raw)
    elif trimmed.Length > maxLength then error (tooLongError(raw, maxLength))
    else Ok trimmed

type DimensionValueId = private DimensionValueId of Guid
module DimensionValueId =
    let create () : DimensionValueId = DimensionValueId(Guid.NewGuid())
    let fromGuid g = DimensionValueId g
    let value (DimensionValueId g) : Guid = g

type SecurityId = private SecurityId of Guid
module SecurityId =
    let create () : SecurityId = SecurityId(Guid.NewGuid())
    let fromGuid g = SecurityId g
    let value (SecurityId g) : Guid = g

type InvestmentAccountId = private InvestmentAccountId of Guid
module InvestmentAccountId =
    let create () : InvestmentAccountId = InvestmentAccountId(Guid.NewGuid())
    let fromGuid g = InvestmentAccountId g
    let value (InvestmentAccountId g) : Guid = g

type HoldingId = private HoldingId of Guid
module HoldingId =
    let create () : HoldingId = HoldingId(Guid.NewGuid())
    let fromGuid g = HoldingId g
    let value (HoldingId g) : Guid = g

type AccountSnapshotId = private AccountSnapshotId of Guid
module AccountSnapshotId =
    let create () : AccountSnapshotId = AccountSnapshotId(Guid.NewGuid())
    let fromGuid g = AccountSnapshotId g
    let value (AccountSnapshotId g) : Guid = g

type AccountSnapshotLineId = private AccountSnapshotLineId of Guid
module AccountSnapshotLineId =
    let create () : AccountSnapshotLineId = AccountSnapshotLineId(Guid.NewGuid())
    let fromGuid g = AccountSnapshotLineId g
    let value (AccountSnapshotLineId g) : Guid = g

type PropertyId = private PropertyId of Guid
module PropertyId =
    let create () : PropertyId = PropertyId(Guid.NewGuid())
    let fromGuid g = PropertyId g
    let value (PropertyId g) : Guid = g

type ValuationId = private ValuationId of Guid
module ValuationId =
    let create () : ValuationId = ValuationId(Guid.NewGuid())
    let fromGuid g = ValuationId g
    let value (ValuationId g) : Guid = g

type Dimension =
    | InvestmentType
    | MarketCap
    | IndexType
    | Sector
    | Region
    | Objective
    | Benchmark

module Dimension =
    let all = [ InvestmentType; MarketCap; IndexType; Sector; Region; Objective; Benchmark ]
    let toString dimension =
        match dimension with
        | InvestmentType -> "InvestmentType"
        | MarketCap -> "MarketCap"
        | IndexType -> "IndexType"
        | Sector -> "Sector"
        | Region -> "Region"
        | Objective -> "Objective"
        | Benchmark -> "Benchmark"
    let fromString (raw: string) : Result<Dimension, IAppError> =
        match all |> List.tryFind (fun d -> toString d = raw) with
        | Some dimension -> Ok dimension
        | None -> error (PositionsInvalidDimension raw)
    /// The security table's column holding this dimension's value.
    let securityColumn dimension =
        match dimension with
        | InvestmentType -> "investment_type_value_id"
        | MarketCap -> "market_cap_value_id"
        | IndexType -> "index_type_value_id"
        | Sector -> "sector_value_id"
        | Region -> "region_value_id"
        | Objective -> "objective_value_id"
        | Benchmark -> "benchmark_value_id"

type DimensionValueName = private DimensionValueName of string
module DimensionValueName =
    let maxLength = 100
    let value (DimensionValueName n) = n
    let create (raw: string) : Result<DimensionValueName, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsDimensionValueNameIsEmpty PositionsDimensionValueNameTooLong
        |> Result.map DimensionValueName

type SecurityName = private SecurityName of string
module SecurityName =
    let maxLength = 200
    let value (SecurityName n) = n
    let create (raw: string) : Result<SecurityName, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsSecurityNameIsEmpty PositionsSecurityNameTooLong
        |> Result.map SecurityName

type Ticker = private Ticker of string
module Ticker =
    let maxLength = 20
    let value (Ticker t) = t
    let create (raw: string) : Result<Ticker, IAppError> =
        raw |> trimmedAndBounded maxLength PositionsTickerIsEmpty PositionsTickerTooLong |> Result.map Ticker

type InvestmentAccountName = private InvestmentAccountName of string
module InvestmentAccountName =
    let maxLength = 100
    let value (InvestmentAccountName n) = n
    let create (raw: string) : Result<InvestmentAccountName, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsInvestmentAccountNameIsEmpty PositionsInvestmentAccountNameTooLong
        |> Result.map InvestmentAccountName

type Institution = private Institution of string
module Institution =
    let maxLength = 100
    let value (Institution i) = i
    let create (raw: string) : Result<Institution, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsInstitutionIsEmpty PositionsInstitutionTooLong
        |> Result.map Institution

type AccountGroup = private AccountGroup of string
module AccountGroup =
    let maxLength = 100
    let value (AccountGroup g) = g
    let create (raw: string) : Result<AccountGroup, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsAccountGroupIsEmpty PositionsAccountGroupTooLong
        |> Result.map AccountGroup

type TaxTreatment =
    | Taxable
    | TaxDeferred
    | Roth
    | Hsa

module TaxTreatment =
    let all = [ Taxable; TaxDeferred; Roth; Hsa ]
    let toString taxTreatment =
        match taxTreatment with
        | Taxable -> "Taxable"
        | TaxDeferred -> "TaxDeferred"
        | Roth -> "Roth"
        | Hsa -> "Hsa"
    let fromString (raw: string) : Result<TaxTreatment, IAppError> =
        match all |> List.tryFind (fun t -> toString t = raw) with
        | Some taxTreatment -> Ok taxTreatment
        | None -> error (PositionsInvalidTaxTreatment raw)
    let allowsJointOwnership taxTreatment = taxTreatment = Taxable

type BasisMethod =
    | AverageCost
    | SpecificLot

module BasisMethod =
    let toString basisMethod =
        match basisMethod with
        | AverageCost -> "AverageCost"
        | SpecificLot -> "SpecificLot"
    let fromString (raw: string) : Result<BasisMethod, IAppError> =
        match raw with
        | "AverageCost" -> Ok AverageCost
        | "SpecificLot" -> Ok SpecificLot
        | _ -> error (PositionsInvalidBasisMethod raw)
    /// A Taxable account's Holding carries a basis method; a Holding in any other account carries none.
    let isAllowedFor (taxTreatment: TaxTreatment) (basisMethod: BasisMethod option) : bool =
        match taxTreatment, basisMethod with
        | TaxTreatment.Taxable, Some _ -> true
        | TaxTreatment.Taxable, None -> false
        | _, Some _ -> false
        | _, None -> true

type Provenance =
    | Reported
    | Imported

module Provenance =
    let toString provenance =
        match provenance with
        | Reported -> "Reported"
        | Imported -> "Imported"
    let fromString (raw: string) : Result<Provenance, IAppError> =
        match raw with
        | "Reported" -> Ok Reported
        | "Imported" -> Ok Imported
        | _ -> error (PositionsInvalidProvenance raw)

type PropertyName = private PropertyName of string
module PropertyName =
    let maxLength = 100
    let value (PropertyName n) = n
    let create (raw: string) : Result<PropertyName, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsPropertyNameIsEmpty PositionsPropertyNameTooLong
        |> Result.map PropertyName

type PropertyUse =
    | PrimaryResidence
    | Rental

module PropertyUse =
    let toString propertyUse =
        match propertyUse with
        | PrimaryResidence -> "PrimaryResidence"
        | Rental -> "Rental"
    let fromString (raw: string) : Result<PropertyUse, IAppError> =
        match raw with
        | "PrimaryResidence" -> Ok PrimaryResidence
        | "Rental" -> Ok Rental
        | _ -> error (PositionsInvalidPropertyUse raw)

type ValuationBasis = private ValuationBasis of string
module ValuationBasis =
    let maxLength = 100
    let value (ValuationBasis b) = b
    let create (raw: string) : Result<ValuationBasis, IAppError> =
        raw
        |> trimmedAndBounded maxLength PositionsValuationBasisIsEmpty PositionsValuationBasisTooLong
        |> Result.map ValuationBasis

type ContributionBasis = private ContributionBasis of Money.Money
module ContributionBasis =
    let value (ContributionBasis m) = m
    let create (money: Money.Money) : Result<ContributionBasis, IAppError> =
        if money |> Money.isNegative then error (PositionsContributionBasisNegative(money |> Money.amount))
        else Ok(ContributionBasis money)

type PurchaseBasis = private PurchaseBasis of Money.Money
module PurchaseBasis =
    let value (PurchaseBasis m) = m
    let create (money: Money.Money) : Result<PurchaseBasis, IAppError> =
        if money |> Money.isPositive then Ok(PurchaseBasis money)
        else error (PositionsPurchaseBasisNotPositive(money |> Money.amount))

type ValuationValue = private ValuationValue of Money.Money
module ValuationValue =
    let value (ValuationValue m) = m
    let create (money: Money.Money) : Result<ValuationValue, IAppError> =
        if money |> Money.isPositive then Ok(ValuationValue money)
        else error (PositionsValuationValueNotPositive(money |> Money.amount))

/// The dates a Property is held: from its acquisition date up to, but not including, its disposal date. Unlike an
/// Investment Account's active period, the last day is not inside it.
type OwnedPeriod = private { acquisitionDate: LocalDate; disposalDate: LocalDate option }
module OwnedPeriod =
    let acquisitionDate (p: OwnedPeriod) = p.acquisitionDate
    let disposalDate (p: OwnedPeriod) = p.disposalDate
    let create (acquisitionDate: LocalDate) (disposalDate: LocalDate option) : Result<OwnedPeriod, IAppError> =
        match disposalDate with
        | Some disposal when disposal < acquisitionDate ->
            error (PositionsDisposalDateBeforeAcquisitionDate(acquisitionDate, disposal))
        | _ -> Ok { acquisitionDate = acquisitionDate; disposalDate = disposalDate }
    let isOwnedOn (date: LocalDate) (p: OwnedPeriod) : bool =
        p.acquisitionDate <= date
        && (match p.disposalDate with
            | Some disposal -> date < disposal
            | None -> true)
    /// Valuations may be dated on the disposal date itself, the day the sale fixes the value.
    let admitsValuationOn (date: LocalDate) (p: OwnedPeriod) : bool =
        p.acquisitionDate <= date
        && (match p.disposalDate with
            | Some disposal -> date <= disposal
            | None -> true)
    /// Whether two periods share an owned date.
    let overlaps (other: OwnedPeriod) (p: OwnedPeriod) : bool =
        let startsBeforeOtherEnds =
            match other.disposalDate with
            | Some otherDisposal -> p.acquisitionDate < otherDisposal
            | None -> true
        let otherStartsBeforeThisEnds =
            match p.disposalDate with
            | Some disposal -> other.acquisitionDate < disposal
            | None -> true
        startsBeforeOtherEnds && otherStartsBeforeThisEnds
