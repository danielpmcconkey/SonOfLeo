namespace Model.UI

open System
open NodaTime

module InterfaceContractTypes =

    type CommandRoute = {
        domain: string
        verb: string
        description: string
        inputType: string
        outputType: string
        handler: string -> string list -> Result<string, string>
    }
    
    
    // ****************************************
    // ACCOUNT DOMAIN
    // ****************************************
    
    // return
    type AccountReturn = {
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
    type AccountCreateInput = {
        code: string
        name: string
        accountTypeSt: string
        activeBegin: Instant
        activeEnd: Instant option
        subType: string option
        parentId: Guid option
        reference: string option }
    // read
    type AccountFetchByIdInput = { id: Guid }
    type AccountFetchByCodeInput = { code: string }
    type AccountFetchByParentIdInput = { parentId: Guid }
    type AccountFetchByAccountTypeInput = { accountTypeSt: string }
    type AccountFetchAllInput = { activeOnly: bool; }
    // update
    type AccountDeactivationInput = { id: Guid; activeEnd: Instant }
    type AccountUpdateNameInput = {id: Guid; newName: string }
    type AccountUpdateExternalReferenceInput = {id: Guid; newReference: string option }

    