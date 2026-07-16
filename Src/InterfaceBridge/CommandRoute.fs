module InterfaceBridge.CommandRoute

open Utilities.AppError

type CommandRoute = { // REQ-NGUI-1.1
    domain: string
    verb: string
    description: string
    inputType: string
    outputType: string
    handler: string -> string list -> Result<string, AppError> }  // REQ-NGUI-1.2