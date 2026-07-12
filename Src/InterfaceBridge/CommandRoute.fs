module InterfaceBridge.CommandRoute

type CommandRoute = { // REQ-NGUI-1.1
    domain: string
    verb: string
    description: string
    inputType: string
    outputType: string
    handler: string -> string list -> Result<string, string> }  // REQ-NGUI-1.2