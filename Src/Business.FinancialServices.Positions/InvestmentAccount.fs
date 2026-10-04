module Business.FinancialServices.Positions.InvestmentAccount

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.General
open Business.General.Person
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type InvestmentAccount = private {
    investmentAccountId: InvestmentAccountId
    investmentAccountName: InvestmentAccountName
    institution: Institution
    accountGroup: AccountGroup
    taxTreatment: TaxTreatment
    owners: PersonId list
    activityPeriod: ActivityPeriod.ActivityPeriod
    ledgerAccountId: AccountId option
    createdAt: Instant
    modifiedAt: Instant
}

/// An ownersUpdate carries the complete new set of owners.
type InvestmentAccountFieldUpdates = {
    investmentAccountIdToUpdate: InvestmentAccountId
    investmentAccountNameUpdate: FieldUpdate<InvestmentAccountName>
    institutionUpdate: FieldUpdate<Institution>
    accountGroupUpdate: FieldUpdate<AccountGroup>
    taxTreatmentUpdate: FieldUpdate<TaxTreatment>
    ownersUpdate: FieldUpdate<PersonId list>
    activityPeriodUpdate: FieldUpdate<ActivityPeriod.ActivityPeriod>
    ledgerAccountIdUpdate: FieldUpdate<AccountId option>
}

let investmentAccountId a = a.investmentAccountId
let investmentAccountName a = a.investmentAccountName
let institution a = a.institution
let accountGroup a = a.accountGroup
let taxTreatment a = a.taxTreatment
let owners a = a.owners
let activityPeriod a = a.activityPeriod
let ledgerAccountId a = a.ledgerAccountId
let createdAt a = a.createdAt
let modifiedAt a = a.modifiedAt

let create
    (investmentAccountId: InvestmentAccountId)
    (investmentAccountName: InvestmentAccountName)
    (institution: Institution)
    (accountGroup: AccountGroup)
    (taxTreatment: TaxTreatment)
    (owners: PersonId list)
    (activityPeriod: ActivityPeriod.ActivityPeriod)
    (ledgerAccountId: AccountId option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : InvestmentAccount =
    { investmentAccountId = investmentAccountId
      investmentAccountName = investmentAccountName
      institution = institution
      accountGroup = accountGroup
      taxTreatment = taxTreatment
      owners = owners |> List.distinct |> List.sortBy PersonId.value
      activityPeriod =
        activityPeriod
        |> ActivityPeriod.insistBeginValidationBehavior ActivityPeriod.NotConsideredAvailableBeforeBeginDate
      ledgerAccountId = ledgerAccountId
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private persistOwners
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (owners: PersonId list)
    : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.investment_account_owner(investment_account_id, person_id)
        values (@investment_account_id, @person_id);"""
    owners
    |> List.map (fun owner ->
        let parameters =
            [ { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) }
              { name = "@person_id"; value = UniqueId(owner |> PersonId.value) } ]
        executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private deleteOwners (context: Context.Context) (investmentAccountId: InvestmentAccountId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.investment_account_owner
        where investment_account_id = @investment_account_id;"""
    let parameters =
        [ { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters AnyQuantityIsAcceptable

let persist (context: Context.Context) (investmentAccount: InvestmentAccount) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.investment_account(
            unique_id, account_name, institution, account_group, tax_treatment, active_begin, active_end,
            ledger_account_id, created_at, modified_at)
        values (
            @unique_id, @account_name, @institution, @account_group, @tax_treatment, @active_begin, @active_end,
            @ledger_account_id, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(investmentAccount.investmentAccountId |> InvestmentAccountId.value) }
          { name = "@account_name"
            value = CharString(investmentAccount.investmentAccountName |> InvestmentAccountName.value) }
          { name = "@institution"; value = CharString(investmentAccount.institution |> Institution.value) }
          { name = "@account_group"; value = CharString(investmentAccount.accountGroup |> AccountGroup.value) }
          { name = "@tax_treatment"; value = CharString(investmentAccount.taxTreatment |> TaxTreatment.toString) }
          { name = "@active_begin"; value = DbLocalDate(investmentAccount.activityPeriod |> ActivityPeriod.activeBegin) }
          { name = "@active_end"
            value = NullableDbLocalDate(investmentAccount.activityPeriod |> ActivityPeriod.activeEnd) }
          { name = "@ledger_account_id"
            value = NullableUniqueId(investmentAccount.ledgerAccountId |> Option.map AccountId.value) }
          { name = "@created_at"; value = DbInstant investmentAccount.createdAt }
          { name = "@modified_at"; value = DbInstant investmentAccount.modifiedAt } ]
    result {
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        do! persistOwners context investmentAccount.investmentAccountId investmentAccount.owners
    }

let private parseIdList (raw: string option) : Guid list =
    match raw with
    | None -> []
    | Some joined -> joined.Split(',') |> Array.map Guid.Parse |> Array.toList

let private reconstitute raw =
    result {
        let uuid, nameStr, institutionStr, groupStr, treatmentStr, ownerIds, activeBegin, activeEnd, ledgerId, createdAt,
            modifiedAt = raw
        let! name = nameStr |> InvestmentAccountName.create
        let! institution = institutionStr |> Institution.create
        let! group = groupStr |> AccountGroup.create
        let! treatment = treatmentStr |> TaxTreatment.fromString
        let! period = ActivityPeriod.create activeBegin activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
        let owners = ownerIds |> parseIdList |> List.map PersonId.fromGuid
        return
            create
                (uuid |> InvestmentAccountId.fromGuid)
                name
                institution
                group
                treatment
                owners
                period
                (ledgerId |> Option.map AccountId.fromGuid)
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "account_name"),
    (row |> RowReader.getString "institution"),
    (row |> RowReader.getString "account_group"),
    (row |> RowReader.getString "tax_treatment"),
    (row |> RowReader.getStringOption "owner_ids"),
    (row |> RowReader.getDate "active_begin"),
    (row |> RowReader.getDateOption "active_end"),
    (row |> RowReader.getUuidOption "ledger_account_id"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let query
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<InvestmentAccount list, IAppError> =
    let from = "positions.investment_account ia"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<InvestmentAccount list, IAppError> =
    let select =
        """
        ia.unique_id, ia.account_name, ia.institution, ia.account_group, ia.tax_treatment,
        (select string_agg(iao.person_id::text, ',' order by iao.person_id)
         from positions.investment_account_owner iao
         where iao.investment_account_id = ia.unique_id) as owner_ids,
        ia.active_begin, ia.active_end, ia.ledger_account_id, ia.created_at, ia.modified_at"""
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (investmentAccountId: InvestmentAccountId) : Result<InvestmentAccount, IAppError> =
    let uuid = investmentAccountId |> InvestmentAccountId.value
    fetchAny context (Some "ia.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsInvestmentAccountIdDoesntExist uuid)
    |> Result.map List.head

let fetchAll (context: Context.Context) : Result<InvestmentAccount list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByName
    (context: Context.Context)
    (investmentAccountName: InvestmentAccountName)
    : Result<InvestmentAccount option, IAppError> =
    let parameters =
        [ { name = "@account_name"; value = CharString(investmentAccountName |> InvestmentAccountName.value) } ]
    fetchAny context (Some "ia.account_name = @account_name") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let fetchByLedgerAccountId (context: Context.Context) (accountId: AccountId) : Result<InvestmentAccount option, IAppError> =
    let parameters = [ { name = "@ledger_account_id"; value = UniqueId(accountId |> AccountId.value) } ]
    fetchAny context (Some "ia.ledger_account_id = @ledger_account_id") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let update
    (context: Context.Context)
    (fieldUpdates: InvestmentAccountFieldUpdates)
    : Result<InvestmentAccount, IAppError> =
    let accountId = fieldUpdates.investmentAccountIdToUpdate
    let uuid = accountId |> InvestmentAccountId.value
    let columnUpdates =
        [ fieldUpdates.investmentAccountNameUpdate
          |> mapNoChangeToOptionWithConversion (fun n ->
              [ "account_name = @account_name",
                { name = "@account_name"; value = CharString(InvestmentAccountName.value n) } ])
          fieldUpdates.institutionUpdate
          |> mapNoChangeToOptionWithConversion (fun i ->
              [ "institution = @institution", { name = "@institution"; value = CharString(Institution.value i) } ])
          fieldUpdates.accountGroupUpdate
          |> mapNoChangeToOptionWithConversion (fun g ->
              [ "account_group = @account_group", { name = "@account_group"; value = CharString(AccountGroup.value g) } ])
          fieldUpdates.taxTreatmentUpdate
          |> mapNoChangeToOptionWithConversion (fun t ->
              [ "tax_treatment = @tax_treatment",
                { name = "@tax_treatment"; value = CharString(TaxTreatment.toString t) } ])
          fieldUpdates.activityPeriodUpdate
          |> mapNoChangeToOptionWithConversion (fun ap ->
              [ "active_begin = @active_begin", { name = "@active_begin"; value = DbLocalDate(ActivityPeriod.activeBegin ap) }
                "active_end = @active_end", { name = "@active_end"; value = NullableDbLocalDate(ActivityPeriod.activeEnd ap) } ])
          fieldUpdates.ledgerAccountIdUpdate
          |> mapNoChangeToOptionWithConversion (fun l ->
              [ "ledger_account_id = @ledger_account_id",
                { name = "@ledger_account_id"; value = NullableUniqueId(l |> Option.map AccountId.value) } ]) ]
        |> List.choose id
        |> List.concat
    let ownersUpdate = fieldUpdates.ownersUpdate |> mapNoChangeToOptionWithConversion id
    let setClauses = columnUpdates |> List.map (fun (clause, _) -> $"{clause}, ") |> String.concat ""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (columnUpdates |> List.map snd)
    let queryStatement =
        $"""
        update positions.investment_account
        set {setClauses}modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do!
            if columnUpdates |> List.isEmpty && ownersUpdate |> Option.isNone then
                error PositionsInvestmentAccountUpdateNoOp
            else
                Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsInvestmentAccountIdDoesntExist uuid)
        do!
            match ownersUpdate with
            | Some newOwners ->
                deleteOwners context accountId
                |> Result.bind (fun () -> persistOwners context accountId (newOwners |> List.distinct))
            | None -> Ok()
        return! accountId |> fetchById context
    }
