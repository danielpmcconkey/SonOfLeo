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
        code: string
        name: string
        accountTypeSt: string
        activeBegin: LocalDate
        activeEnd: LocalDate option
        subType: string option
        parentCode: string option
        reference: string option
        createdAt: Instant
        modifiedAt: Instant
    }
    // create
    type AccountCreateInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        code: string
        name: string
        accountTypeSt: string
        activeBegin: LocalDate
        activeEnd: LocalDate option
        subType: string option
        parentCode: string option
        reference: string option }
    // read
    type AccountFetchByCodeInput = { code: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchByParentCodeInput = { parentCode: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchByAccountTypeInput = { accountTypeSt: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountFetchAllInput = { activeOnly: bool; } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    // update
    type AccountDeactivationInput = { code: string; activeEnd: LocalDate } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountUpdateNameInput = { code: string; newName: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountUpdateExternalReferenceInput = { code: string; newReference: string option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    
    
    // ****************************************
    // FISCAL PERIOD DOMAIN
    // ****************************************
    
    // return
    type FiscalPeriodReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        periodKey: string
        startDate: LocalDate
        endDate: LocalDate
        isOpen: bool
        createdAt: Instant
        modifiedAt: Instant
    }
    
    /// FiscalPeriodInput is a multi-purpose interface contract, used for create, fetch by key, close, and reopen
    type FiscalPeriodInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        periodKey: string
    }
    type FiscalPeriodFetchAllInput = { openOnly: bool; } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2

