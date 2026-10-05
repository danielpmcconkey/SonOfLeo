module Business.CrossDomainOrchestration.DimensionValueOrchestration

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

let private confirmNameFree
    (context: Context.Context)
    (dimension: Dimension)
    (name: DimensionValueName)
    (self: DimensionValueId option)
    : Result<unit, IAppError> =
    result {
        let! found = DimensionValue.fetchByDimensionAndName context dimension name
        match found with
        | Some dimensionValue when Some(dimensionValue |> DimensionValue.dimensionValueId) <> self ->
            return!
                error (
                    PositionsDimensionValueAlreadyExists(dimension |> Dimension.toString, name |> DimensionValueName.value)
                )
        | _ -> return ()
    }

let constructNewAndPersist
    (context: Context.Context)
    (dimension: Dimension)
    (name: DimensionValueName)
    : Result<DimensionValue.DimensionValue, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let dimensionValue = DimensionValue.create (DimensionValueId.create ()) dimension name instant instant
    result {
        do! confirmNameFree context dimension name None
        do! dimensionValue |> DimensionValue.persist context
        return dimensionValue
    }

/// A Dimension Value keeps its dimension; only its name changes.
let renameDimensionValue
    (context: Context.Context)
    (fieldUpdates: DimensionValue.DimensionValueFieldUpdates)
    : Result<DimensionValue.DimensionValue, IAppError> =
    result {
        let self = fieldUpdates.dimensionValueIdToUpdate
        let! dimensionValue = self |> DimensionValue.fetchById context
        do!
            match fieldUpdates.dimensionValueNameUpdate with
            | SetTo newName -> confirmNameFree context (dimensionValue |> DimensionValue.dimension) newName (Some self)
            | NoChange -> Ok()
        return! DimensionValue.update context fieldUpdates
    }

let listDimensionValues (context: Context.Context) (dimension: Dimension) : Result<DimensionValue.DimensionValue list, IAppError> =
    DimensionValue.fetchByDimension context dimension
    |> Result.map (List.sortBy (DimensionValue.dimensionValueName >> DimensionValueName.value))
