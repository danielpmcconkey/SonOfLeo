namespace Model.UI

open System
open NodaTime

module UiPrimitives =
    
    type AccountPrimitives = {
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

    