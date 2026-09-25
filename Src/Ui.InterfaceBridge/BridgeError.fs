module Ui.InterfaceBridge.BridgeError

open App.Utility.IAppError

type BridgeError =
    | InterfaceBridgeConversionFailure of string * string * string * string
    
    interface IAppError with
        member this.DomainName = nameof BridgeError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError) -> $"Failed conversion in InterfaceBridge. Original type: {originalType}. Desired type: {desiredType}. Original value: {originalValue}. Additional details: {childError}"
            
let toMessage (e: BridgeError) = (e :> IAppError).ToMessage()
let toAppError (e: BridgeError) : IAppError = e :> IAppError
let error (e: BridgeError) : Result<'T, IAppError> = Error (e :> IAppError)



