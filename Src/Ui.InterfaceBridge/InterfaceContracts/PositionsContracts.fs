module Ui.InterfaceBridge.InterfaceContracts.PositionsContracts

open NodaTime
open App.Utility.FieldUpdate

// every ledger account in a return carries its name beside its code
type LedgerAccountReturn = { code: string; name: string }

// ---- Dimension Values ----

type DimensionValueReturn = { dimension: string; valueName: string }

type DimensionValueCreateInput = { dimension: string; valueName: string }

type DimensionValueRenameInput = { dimension: string; currentName: string; newName: string }

type DimensionValueListInput = { dimension: string }

// ---- Securities ----

type SecurityReturn = {
    securityName: string
    ticker: string option
    // one entry per dimension that has a value, in the order of the seven dimensions
    dimensionValues: DimensionValueReturn list
    createdAt: Instant
    modifiedAt: Instant
}

type SecurityDimensionValueInput = { dimension: string; valueName: string }

type SecurityCreateInput = {
    securityName: string
    ticker: string option
    dimensionValues: SecurityDimensionValueInput list
}

// a dimension given with a valueName is set to it; given with none, it is cleared; not given, it is unchanged
type DimensionValueUpdateInput = { dimension: string; valueName: string option }

// the Security is addressed by its current name
type SecurityUpdateInput = {
    securityName: string
    securityNameUpdate: FieldUpdate<string>
    tickerUpdate: FieldUpdate<string option>
    dimensionValueUpdates: DimensionValueUpdateInput list
}

// ---- Investment Accounts ----

type InvestmentAccountReturn = {
    accountName: string
    institution: string
    accountGroup: string
    taxTreatment: string
    owners: string list
    activeBegin: LocalDate
    activeEnd: LocalDate option
    ledgerAccount: LedgerAccountReturn option
    createdAt: Instant
    modifiedAt: Instant
}

type InvestmentAccountCreateInput = {
    accountName: string
    institution: string
    accountGroup: string
    taxTreatment: string
    owners: string list
    activeBegin: LocalDate
    activeEnd: LocalDate option
    ledgerAccountCode: string option
}

// the account is addressed by its current name; ownersUpdate carries the complete new set
type InvestmentAccountUpdateInput = {
    accountName: string
    accountNameUpdate: FieldUpdate<string>
    institutionUpdate: FieldUpdate<string>
    accountGroupUpdate: FieldUpdate<string>
    taxTreatmentUpdate: FieldUpdate<string>
    ownersUpdate: FieldUpdate<string list>
    activeBeginUpdate: FieldUpdate<LocalDate>
    activeEndUpdate: FieldUpdate<LocalDate option>
    ledgerAccountCodeUpdate: FieldUpdate<string option>
}

// ---- Holdings ----

type HoldingReturn = {
    accountName: string
    securityName: string
    basisMethod: string option
    createdAt: Instant
    modifiedAt: Instant
}

type HoldingCreateInput = { accountName: string; securityName: string; basisMethod: string option }

type HoldingUpdateBasisMethodInput = { accountName: string; securityName: string; basisMethod: string option }

type HoldingDeleteInput = { accountName: string; securityName: string }

type HoldingListInput = { accountName: string option }

type HoldingFetchAsOfInput = { asOf: LocalDate }

// a lot as the institution reported it; a line's lots come back in the order they were sent
type AccountSnapshotLotReturn = { acquiredDate: LocalDate; quantity: decimal; reportedCostBasis: decimal option }

type HoldingsAsOfLineReturn = {
    securityName: string
    ticker: string option
    dimensionValues: DimensionValueReturn list
    basisMethod: string option
    quantity: decimal
    price: decimal
    marketValue: decimal
    reportedCostBasis: decimal option
    lots: AccountSnapshotLotReturn list
}

type HoldingsAsOfAccountReturn = {
    accountName: string
    institution: string
    accountGroup: string
    taxTreatment: string
    owners: string list
    ledgerAccount: LedgerAccountReturn option
    snapshotDate: LocalDate
    provenance: string
    contributionBasis: decimal option
    lines: HoldingsAsOfLineReturn list
}

// ---- Account Snapshots ----

type AccountSnapshotLotInput = { acquiredDate: LocalDate; quantity: decimal; reportedCostBasis: decimal option }

// lots are recorded in the order sent; an empty list means the institution supplied none for the line
type AccountSnapshotLineInput = {
    securityName: string
    quantity: decimal
    price: decimal
    marketValue: decimal
    reportedCostBasis: decimal option
    lots: AccountSnapshotLotInput list
}

type AccountSnapshotInput = {
    accountName: string
    snapshotDate: LocalDate
    provenance: string
    contributionBasis: decimal option
    lines: AccountSnapshotLineInput list
}

// every snapshot is recorded, or none is
type AccountSnapshotRecordInput = { snapshots: AccountSnapshotInput list }

type AccountSnapshotLineReturn = {
    securityName: string
    quantity: decimal
    price: decimal
    marketValue: decimal
    reportedCostBasis: decimal option
    lots: AccountSnapshotLotReturn list
}

type AccountSnapshotReturn = {
    accountName: string
    snapshotDate: LocalDate
    provenance: string
    contributionBasis: decimal option
    lines: AccountSnapshotLineReturn list
    createdAt: Instant
    modifiedAt: Instant
}

type RecordedAccountSnapshotReturn = { snapshot: AccountSnapshotReturn; replacedExisting: bool }

type AccountSnapshotDeleteInput = { accountName: string; snapshotDate: LocalDate }

type AccountSnapshotFetchInput = { accountName: string; snapshotDate: LocalDate }

type AccountSnapshotListDatesInput = { accountName: string; beginDate: LocalDate; endDate: LocalDate }

type AccountSnapshotDateReturn = { snapshotDate: LocalDate; provenance: string }

// ---- Properties ----

type PropertyReturn = {
    propertyName: string
    propertyUse: string
    owners: string list
    acquisitionDate: LocalDate
    disposalDate: LocalDate option
    purchaseBasis: decimal
    assetAccounts: LedgerAccountReturn list
    mortgageAccounts: LedgerAccountReturn list
    createdAt: Instant
    modifiedAt: Instant
}

type PropertyCreateInput = {
    propertyName: string
    propertyUse: string
    owners: string list
    acquisitionDate: LocalDate
    disposalDate: LocalDate option
    purchaseBasis: decimal
    assetAccountCodes: string list
    mortgageAccountCodes: string list
}

// the Property is addressed by its current name; ownersUpdate, assetAccountCodesUpdate and mortgageAccountCodesUpdate
// carry complete new sets
type PropertyUpdateInput = {
    propertyName: string
    propertyNameUpdate: FieldUpdate<string>
    propertyUseUpdate: FieldUpdate<string>
    ownersUpdate: FieldUpdate<string list>
    acquisitionDateUpdate: FieldUpdate<LocalDate>
    disposalDateUpdate: FieldUpdate<LocalDate option>
    purchaseBasisUpdate: FieldUpdate<decimal>
    assetAccountCodesUpdate: FieldUpdate<string list>
    mortgageAccountCodesUpdate: FieldUpdate<string list>
}

type PropertyDeleteInput = { propertyName: string }

// ---- Valuations ----

type ValuationReturn = {
    propertyName: string
    valuationDate: LocalDate
    value: decimal
    basis: string
    createdAt: Instant
    modifiedAt: Instant
}

type ValuationRecordInput = { propertyName: string; valuationDate: LocalDate; value: decimal; basis: string }

type ValuationDeleteInput = { propertyName: string; valuationDate: LocalDate }

type ValuationListInput = { propertyName: string }
