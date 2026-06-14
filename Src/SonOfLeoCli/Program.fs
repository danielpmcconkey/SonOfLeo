open System
open Model.Audit
open NodaTime
open Model.Ledger.Account

//let result = Account.fetchById (Guid.Parse("d544202b-0099-4b4d-b262-6622f047ed05"))


let code1 = "REQ-AC-1.4"
let code2 = code1
let name1 = "AccountCode must be unique"
let name2 = "AccountCode must still be unique"
let accountType = "Asset"
let activeBegin = Instant.FromDateTimeOffset(DateTimeOffset.Now)
let activeEnd = None
let subtype = None
let parentId = None
let reference = None
let envelope1 = AuditEnvelope.create AccountCreate
let envelope2 = AuditEnvelope.create AccountCreate
    
let result = Account.constructNewAndSaveToDb code1 name1 accountType activeBegin activeEnd subtype parentId reference envelope1
        



printfn "%A" result