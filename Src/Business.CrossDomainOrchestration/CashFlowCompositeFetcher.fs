module Business.CrossDomainOrchestration.CashFlowCompositeFetcher

open System
open App.Utility
open App.Utility.IAppError
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Session
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.MasterAgreement
open Business.CrossDomainOrchestration.FetchFilters

let createPredicateAndParameters
    (context: Context.Context)
    (filter: AgreementFilter)
    : string * QueryParameter list =
    let agreementPredicate, agreementParameters =
        filter.agreementIds
        |> createIdPredicateAndParameters<MasterAgreementId> MasterAgreementId.value "agreement_id" ["ma.unique_id"]
    let activeAgreementPredicate =
        if filter.activeAgreementsOnly then
            Some "(ma.start_date <= @today and (ma.end_date is null or ma.end_date >= @today))"
        else None
    let activeAgreementParameters =
        let today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
        if filter.activeAgreementsOnly then [{ name = "@today"; value = DbLocalDate today }] else []
    let allPredicates =
        [ agreementPredicate; activeAgreementPredicate ]
        |> List.choose id
        |> String.concat $"{Environment.NewLine}and "
    allPredicates, agreementParameters @ activeAgreementParameters

/// fetchCompositeFiltered returns the Master Agreements the filter selects.
let fetchCompositeFiltered
    (context: Context.Context)
    (expectedRows: AcceptableExpectedRows)
    (filter: AgreementFilter)
    : Result<MasterAgreement list, IAppError> =
    let predicates, parameters = filter |> createPredicateAndParameters context
    let predicate = if predicates = "" then None else Some predicates
    Business.FinancialServices.CashFlow.MasterAgreement.query context None masterAgreementSelectFields None predicate None None None parameters expectedRows
