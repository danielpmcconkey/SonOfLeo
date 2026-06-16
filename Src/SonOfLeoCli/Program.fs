open System
open Model.Audit
open Model.Ledger.Account.Account
open Model.Ledger.AccountComponent
open NodaTime
open Model.Ledger.Account
open Model.UI
open Model.UI.UiPrimitives
open Utilities.ResultCE
let accountCreate payload =
    result {
        let! accountPrimitives = Json.fromJson<AccountPrimitives> payload
        let envelope = AuditEnvelope.create AccountCreate
        let! account = Account.constructNewAndSaveToDb
                         accountPrimitives.code
                         accountPrimitives.name
                         accountPrimitives.accountTypeSt
                         accountPrimitives.activeBegin
                         accountPrimitives.activeEnd
                         accountPrimitives.subType
                         accountPrimitives.parentId
                         accountPrimitives.reference
                         envelope
        let returnAccount : AccountPrimitives = {
            id = Some (Account.id account)
            code = AccountCode.value (Account.code account)
            name = AccountName.value (Account.name account)
            accountTypeSt = AccountType.toString (Account.accountType account)
            activeBegin = Account.activeBegin account
            activeEnd = Account.activeEnd account
            subType = Account.accountSubType account |> Option.map AccountSubtype.toString
            parentId = Account.parentId account
            reference = Account.externalReference account |> Option.map AccountExternalReference.value
            modifiedAt = Some (Account.modifiedAt account)
            createdAt = Some(Account.createdAt account)
        }
        return! Json.toJson<AccountPrimitives> returnAccount
    }
let route (domain) (verb) (rest) (payload) : Result<string, string> =
    match domain with
    | "Account" ->
        match verb with
        | "Create" -> accountCreate payload
        | _ -> Error $"Unknown account activity: {verb}"
    | _ -> Error $"Unknown domain: {domain}"
    
[<EntryPoint>]
let main args =
    let payload = Console.In.ReadToEnd()
    match args |> Array.toList with
    | domain :: verb :: rest ->
        let result = (route domain verb rest payload)
        match result with
        | Ok n -> n |> printfn "%s"; 0
        | Error e -> e |> eprintfn "%s"; 1
    | _ -> eprintfn "Usage: sonofleo <domain> <verb> [args...]"; 1
