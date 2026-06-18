namespace Model.UI

open System
open NodaTime

module InterfaceContractTypes =

    type CommandRoute = { // REQ-NGUI-1.1
        domain: string
        verb: string
        description: string
        inputType: string
        outputType: string
        handler: string -> string list -> Result<string, string> // REQ-NGUI-1.2
    }
    
    
    // ****************************************
    // ACCOUNT DOMAIN
    // ****************************************
    
    // return
    type AccountReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        id: Guid option
        code: string
        name: string
        accountTypeSt: string
        activeBegin: Instant
        activeEnd: Instant option
        subType: string option
        parentId: Guid option
        reference: string option
        modifiedAt: Instant option
        createdAt: Instant option
    }
    // create
    type AccountCreateInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        code: string
        name: string
        accountTypeSt: string
        activeBegin: Instant
        activeEnd: Instant option
        subType: string option
        parentId: Guid option
        reference: string option }
    // read
    type AccountFetchByIdInput = { id: Guid } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchByCodeInput = { code: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchByParentIdInput = { parentId: Guid } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchByAccountTypeInput = { accountTypeSt: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchAllInput = { activeOnly: bool; } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    // update
    type AccountDeactivationInput = { id: Guid; activeEnd: Instant } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountUpdateNameInput = {id: Guid; newName: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountUpdateExternalReferenceInput = {id: Guid; newReference: string option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2

    