module Business.CrossDomainOrchestration.SecurityOrchestration

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

let private confirmSecurityNameFree
    (context: Context.Context)
    (name: SecurityName)
    (self: SecurityId option)
    : Result<unit, IAppError> =
    result {
        let! found = Security.fetchByName context name
        match found with
        | Some security when Some(security |> Security.securityId) <> self ->
            return! error (PositionsSecurityNameAlreadyExists(name |> SecurityName.value))
        | _ -> return ()
    }

let private confirmTickerFree (context: Context.Context) (ticker: Ticker option) (self: SecurityId option) =
    match ticker with
    | None -> Ok()
    | Some t ->
        result {
            let! found = Security.fetchByTicker context t
            match found with
            | Some security when Some(security |> Security.securityId) <> self ->
                return! error (PositionsTickerAlreadyExists(t |> Ticker.value))
            | _ -> return ()
        }

let private confirmEachDimensionOnce (dimensions: Dimension list) : Result<unit, IAppError> =
    match dimensions |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
    | Some(dimension, _) -> error (PositionsSecurityDimensionGivenTwice(dimension |> Dimension.toString))
    | None -> Ok()

// A value only ever lands in its own dimension's slot.
let private confirmValueInDimension
    (context: Context.Context)
    (dimension: Dimension)
    (dimensionValueId: DimensionValueId)
    : Result<unit, IAppError> =
    result {
        let! dimensionValue = dimensionValueId |> DimensionValue.fetchById context
        if dimensionValue |> DimensionValue.dimension <> dimension then
            return!
                error (
                    PositionsDimensionValueNameDoesntMatch(
                        dimension |> Dimension.toString,
                        dimensionValue |> DimensionValue.dimensionValueName |> DimensionValueName.value
                    )
                )
    }

let private confirmDimensionValues
    (context: Context.Context)
    (givenValues: (Dimension * DimensionValueId option) list)
    : Result<Map<Dimension, DimensionValueId option>, IAppError> =
    result {
        do! givenValues |> List.map fst |> confirmEachDimensionOnce
        do!
            givenValues
            |> List.choose (fun (dimension, valueId) -> valueId |> Option.map (fun id -> dimension, id))
            |> List.map (fun (dimension, valueId) -> confirmValueInDimension context dimension valueId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return givenValues |> Map.ofList
    }

let constructNewAndPersist
    (context: Context.Context)
    (name: SecurityName)
    (ticker: Ticker option)
    (dimensionValues: (Dimension * DimensionValueId) list)
    : Result<Security.Security, IAppError> =
    let instant = context |> Context.getInitiationInstant
    result {
        do! confirmSecurityNameFree context name None
        do! confirmTickerFree context ticker None
        let! confirmed = dimensionValues |> List.map (fun (d, v) -> d, Some v) |> confirmDimensionValues context
        let assigned = confirmed |> Map.toList |> List.choose (fun (d, v) -> v |> Option.map (fun id -> d, id)) |> Map.ofList
        let security = Security.create (SecurityId.create ()) name ticker assigned instant instant
        do! security |> Security.persist context
        return security
    }

/// A dimension given with Some value is set to it; with None it is cleared; a dimension not given is unchanged.
let updateSecurity
    (context: Context.Context)
    (securityId: SecurityId)
    (nameUpdate: FieldUpdate<SecurityName>)
    (tickerUpdate: FieldUpdate<Ticker option>)
    (dimensionValueUpdates: (Dimension * DimensionValueId option) list)
    : Result<Security.Security, IAppError> =
    result {
        let! security = securityId |> Security.fetchById context
        let self = Some(security |> Security.securityId)
        do!
            match nameUpdate with
            | SetTo newName -> confirmSecurityNameFree context newName self
            | NoChange -> Ok()
        do!
            match tickerUpdate with
            | SetTo newTicker -> confirmTickerFree context newTicker self
            | NoChange -> Ok()
        let! confirmed = confirmDimensionValues context dimensionValueUpdates
        return!
            Security.update
                context
                { securityIdToUpdate = securityId
                  securityNameUpdate = nameUpdate
                  tickerUpdate = tickerUpdate
                  dimensionValueUpdates = confirmed }
    }

/// Every Security, ordered by name.
let listSecurities (context: Context.Context) : Result<Security.Security list, IAppError> =
    Security.fetchAll context |> Result.map (List.sortBy (Security.securityName >> SecurityName.value))
