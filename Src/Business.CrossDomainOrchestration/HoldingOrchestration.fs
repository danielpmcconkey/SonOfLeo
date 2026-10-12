module Business.CrossDomainOrchestration.HoldingOrchestration

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

let private accountNameOf account = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value

let private securityNameOf security = security |> Security.securityName |> SecurityName.value

let private confirmBasisMethodAllowed
    (account: InvestmentAccount.InvestmentAccount)
    (security: Security.Security)
    (basisMethod: BasisMethod option)
    : Result<unit, IAppError> =
    let taxTreatment = account |> InvestmentAccount.taxTreatment
    if basisMethod |> BasisMethod.isAllowedFor taxTreatment then
        Ok()
    else
        error (PositionsBasisMethodNotAllowed(accountNameOf account, securityNameOf security, taxTreatment |> TaxTreatment.toString))

/// The account, the Security, and the Holding of one in the other if there is one.
let private fetchParts
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (securityId: SecurityId)
    : Result<InvestmentAccount.InvestmentAccount * Security.Security * Holding.Holding option, IAppError> =
    result {
        let! account = investmentAccountId |> InvestmentAccount.fetchById context
        let! security = securityId |> Security.fetchById context
        let! holding = Holding.fetchByInvestmentAccountAndSecurity context investmentAccountId securityId
        return account, security, holding
    }

let private existingHolding account security (holding: Holding.Holding option) : Result<Holding.Holding, IAppError> =
    match holding with
    | Some h -> Ok h
    | None -> error (PositionsHoldingDoesntExist(accountNameOf account, securityNameOf security))

let constructNewAndPersist
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (securityId: SecurityId)
    (basisMethod: BasisMethod option)
    : Result<Holding.Holding, IAppError> =
    let instant = context |> Context.getInitiationInstant
    result {
        let! account, security, existing = fetchParts context investmentAccountId securityId
        do!
            if existing |> Option.isSome then
                error (PositionsHoldingAlreadyExists(accountNameOf account, securityNameOf security))
            else
                Ok()
        do! confirmBasisMethodAllowed account security basisMethod
        let holding = Holding.create (HoldingId.create ()) investmentAccountId securityId basisMethod instant instant
        do! holding |> Holding.persist context
        return holding
    }

// A Lot's cost is read through its Holding's basis method, and the Holding keeps one method with no history.
let private confirmNoLotsReinterpreted
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (security: Security.Security)
    (holding: Holding.Holding)
    (basisMethod: BasisMethod option)
    : Result<unit, IAppError> =
    if basisMethod = (holding |> Holding.basisMethod) then Ok() else
    result {
        let! datesWithLots = holding |> Holding.holdingId |> AccountSnapshotLot.fetchSnapshotDatesWithLotsForHolding context
        if not (datesWithLots |> List.isEmpty) then
            let accountName = accountNameOf account
            let securityName = securityNameOf security
            let earliest = datesWithLots |> List.min
            let latest = datesWithLots |> List.max
            return! error (PositionsHoldingBasisMethodChangeStrandsLots(accountName, securityName, earliest, latest))
    }

let changeHoldingBasisMethod
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (securityId: SecurityId)
    (basisMethod: BasisMethod option)
    : Result<Holding.Holding, IAppError> =
    result {
        let! account, security, existing = fetchParts context investmentAccountId securityId
        let! holding = existingHolding account security existing
        do! confirmBasisMethodAllowed account security basisMethod
        do! confirmNoLotsReinterpreted context account security holding basisMethod
        return!
            Holding.update context { holdingIdToUpdate = holding |> Holding.holdingId; basisMethodUpdate = SetTo basisMethod }
    }

/// Deletes the Holding and returns it as it stood before deletion.
let deleteHolding
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (securityId: SecurityId)
    : Result<Holding.Holding, IAppError> =
    result {
        let! account, security, existing = fetchParts context investmentAccountId securityId
        let! holding = existingHolding account security existing
        let holdingId = holding |> Holding.holdingId
        let! referenced = holdingId |> AccountSnapshotLine.existsForHolding context
        do!
            if referenced then
                error (PositionsHoldingReferencedBySnapshots(accountNameOf account, securityNameOf security))
            else
                Ok()
        let! named = holdingId |> InvestmentActivity.existsForHolding context
        do!
            if named then
                error (PositionsHoldingReferencedByActivities(accountNameOf account, securityNameOf security))
            else
                Ok()
        do! holdingId |> Holding.delete context
        return holding
    }

/// The account's Holdings, or every Holding when no account is given, ordered by account name then Security name.
let listHoldings
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId option)
    : Result<Holding.Holding list, IAppError> =
    result {
        let! holdings =
            match investmentAccountId with
            | Some id ->
                id
                |> InvestmentAccount.fetchById context
                |> Result.bind (fun _ -> Holding.fetchByInvestmentAccount context id)
            | None -> Holding.fetchAll context
        let! accounts = InvestmentAccount.fetchAll context
        let! securities = Security.fetchAll context
        let accountNames = accounts |> List.map (fun a -> InvestmentAccount.investmentAccountId a, accountNameOf a) |> Map.ofList
        let securityNames = securities |> List.map (fun s -> Security.securityId s, securityNameOf s) |> Map.ofList
        let! keyed =
            holdings
            |> List.map (fun holding ->
                let accountId = holding |> Holding.investmentAccountId
                let securityId = holding |> Holding.securityId
                match accountNames |> Map.tryFind accountId, securityNames |> Map.tryFind securityId with
                | Some accountName, Some securityName -> Ok((accountName, securityName), holding)
                | None, _ -> error (PositionsInvestmentAccountIdDoesntExist(accountId |> InvestmentAccountId.value))
                | _, None -> error (PositionsSecurityIdDoesntExist(securityId |> SecurityId.value)))
            |> convertListOfResultsToResultsList
        return keyed |> List.sortBy fst |> List.map snd
    }
