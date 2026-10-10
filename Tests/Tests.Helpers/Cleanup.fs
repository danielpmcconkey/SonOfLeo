module Tests.Helpers.Cleanup

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.Operation.AuditEnvelope
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open Business.FinancialServices.Classification.ClassificationComponent
open App.Session
open App.Operation.CoreAuditableAction


(*
 * These functions are used in tests' "finally" blocks where the test flow that
 * may or may not have failed before calling cleanup. Therefore, they take
 * options as their key parameters. Do the option resolution here so you don't
 * have to do it everywhere 
 *)

//=================================================
// Account clean up
//=================================================

let cleanUpAccountId (accountId: AccountId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match accountId with
    | None -> Ok()
    | Some x ->
        let uniqueId = x |> AccountId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uniqueId } ]
        let query =
            $"""
                delete from ledger.account
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

let cleanUpAccountList (l: AccountId option list) : Result<unit, IAppError> =
    l
    |> List.map cleanUpAccountId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of account IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map _.ToMessage() |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

let cleanUpParentIdAndChildren (parentId: AccountId option) (children: AccountId option list) : Result<unit, IAppError> =
    result {
        let! _ =
            children // clean the children before parent
            |> cleanUpAccountList
        let! _ = cleanUpAccountId parentId // note that the parent won't be cleaned up if any of the child cleanups failed
        return ()
    }

//=================================================
// Fiscal Period clean up
//=================================================
let cleanUpFiscalPeriodId (fpId: FiscalPeriodId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match fpId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> FiscalPeriodId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        let query =
            $"""
                delete from ledger.fiscal_period
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

let cleanUpFiscalPeriodKey (key: string option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match key with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@period_key"; value = CharString x } ]
        let query =
            $"""
                delete from ledger.fiscal_period
                WHERE period_key = @period_key;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

let cleanUpFiscalPeriodIdsList (l: FiscalPeriodId option list) : Result<unit, IAppError> =
    l
    |> List.map cleanUpFiscalPeriodId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of fiscal period IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map _.ToMessage() |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

let cleanUpFiscalPeriodKeysList (l: string option list) : Result<unit, IAppError> =
    l
    |> List.map cleanUpFiscalPeriodKey
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of fiscal period keys. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map _.ToMessage() |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

//=================================================
// Journal Entry clean up
//=================================================

let cleanUpJournalEntryId (journalEntryHeaderId: JournalEntryHeaderId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match journalEntryHeaderId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> JournalEntryHeaderId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        // delete children before the header, in FK order
        let commentQuery =
            $"""
                delete from ledger.journal_entry_comment
                WHERE journal_primary_entry_id = @unique_id
                   OR journal_secondary_entry_id = @unique_id;
            """
        let extReferenceQuery =
            $"""
                delete from ledger.journal_entry_ext_reference
                WHERE journal_entry_id = @unique_id;
            """
        let lineQuery =
            $"""
                delete from ledger.journal_entry_line
                WHERE journal_entry_id = @unique_id;
            """
        let headerQuery =
            $"""
                delete from ledger.journal_entry
                WHERE unique_id = @unique_id;
            """

        result {
            let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) commentQuery parameters AnyQuantityIsAcceptable
            let! _ =
                executeNonQuery (context |> Context.getDatabaseTransaction) extReferenceQuery parameters AnyQuantityIsAcceptable
            let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) lineQuery parameters AnyQuantityIsAcceptable
            return! executeNonQuery (context |> Context.getDatabaseTransaction) headerQuery parameters ExactlyOne
        }

let cleanUpJournalEntryExtReferenceId (uniqueId: Guid option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match uniqueId with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@unique_id"; value = UniqueId x } ]
        let query =
            $"""
                delete from ledger.journal_entry_ext_reference
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

let cleanUpJournalEntryCommentId (uniqueId: Guid option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match uniqueId with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@unique_id"; value = UniqueId x } ]
        let query =
            $"""
                delete from ledger.journal_entry_comment
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

let cleanUpJournalEntryList (l: JournalEntryHeaderId option list) : Result<unit, IAppError> =
    l
    |> List.map cleanUpJournalEntryId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of journal entry IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map _.ToMessage() |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

//=================================================
// Staged entry clean up
//=================================================

let cleanUpStageEntryHeaderId (headerId: StageEntryHeaderId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match headerId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> StageEntryHeaderId.value
        let parameters = [ { name = "@entry_id"; value = UniqueId uuid } ]
        let headerParameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        // delete children before the header, in FK order
        let auditQuery =
            $"""
                delete from ingestion.staged_entry_audit
                WHERE entry_id = @entry_id;
            """
        let ruleMatchQuery =
            $"""
                delete from classification.rule_match
                WHERE stage_entry_line_id IN (select unique_id from ingestion.staged_entry_line where entry_id = @entry_id);
            """
        let lineQuery =
            $"""
                delete from ingestion.staged_entry_line
                WHERE entry_id = @entry_id;
            """
        let headerQuery =
            $"""
                delete from ingestion.staged_entry
                WHERE unique_id = @unique_id;
            """

        result {
            let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) auditQuery parameters AnyQuantityIsAcceptable
            let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) ruleMatchQuery parameters AnyQuantityIsAcceptable
            let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) lineQuery parameters AnyQuantityIsAcceptable
            return! executeNonQuery (context |> Context.getDatabaseTransaction) headerQuery headerParameters ExactlyOne
        }

let cleanUpStageEntryHeaderIdList (l: StageEntryHeaderId option list) : Result<unit, IAppError> =
    l
    |> List.map cleanUpStageEntryHeaderId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of staged entry IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map _.ToMessage() |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

//=================================================
// Ingestion source clean up
//=================================================

let cleanUpIngestionSourceId (sourceId: IngestionSourceId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match sourceId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> IngestionSourceId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        let query =
            $"""
                delete from ingestion.source
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

//=================================================
// Classification rule clean up
//=================================================

(* No child rows are deleted here. A rule a test created has never classified anything, so
   nothing in classification.rule_match points at it — and if something does, ExactlyOne surfacing
   the FK violation is the right outcome rather than quietly widening the delete. *)
let cleanUpClassificationRuleId (ruleId: ClassificationRuleId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match ruleId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> ClassificationRuleId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        let query =
            $"""
                delete from classification.classification_rule
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

//=================================================
// Cash flow clean up
//=================================================

/// Deletes a Master Agreement and everything hanging off it: its legs' links, its Payments, Invoices and Instances,
/// then the legs and the agreement itself. Staged entries the links pointed at are left for their own clean up.
let cleanUpMasterAgreementTree (agreementId: Guid option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match agreementId with
    | None -> Ok()
    | Some uuid ->
        let parameters = [ { name = "@agreement_id"; value = UniqueId uuid } ]
        // delete children before parents, in FK order
        let queries =
            [ """delete from cashflow.payment_agreement_link WHERE payment_agreement_id IN
                    (select unique_id from cashflow.payment_agreement where master_agreement_id = @agreement_id);"""
              """delete from cashflow.payment WHERE invoice_id IN
                    (select i.unique_id from cashflow.invoice i join cashflow.instance n on n.unique_id = i.instance_id
                     where n.master_agreement_id = @agreement_id);"""
              """delete from cashflow.invoice WHERE instance_id IN
                    (select unique_id from cashflow.instance where master_agreement_id = @agreement_id);"""
              """delete from cashflow.instance WHERE master_agreement_id = @agreement_id;"""
              """delete from cashflow.payment_agreement WHERE master_agreement_id = @agreement_id;""" ]
        result {
            do!
                queries
                |> List.map (fun query ->
                    executeNonQuery (context |> Context.getDatabaseTransaction) query parameters AnyQuantityIsAcceptable
                    |> Result.map ignore)
                |> List.fold (fun acc r -> acc |> Result.bind (fun () -> r)) (Ok())
            let masterQuery = """delete from cashflow.master_agreement WHERE unique_id = @agreement_id;"""
            return!
                executeNonQuery (context |> Context.getDatabaseTransaction) masterQuery parameters ExactlyOne
                |> Result.map ignore
        }

/// Deletes the rule matches a classification rule recorded, which must go before the rule itself.
let cleanUpRuleMatchesOfRuleId (ruleId: ClassificationRuleId option) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    match ruleId with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@rule_id"; value = UniqueId(x |> ClassificationRuleId.value) } ]
        let query = """delete from classification.rule_match WHERE classification_rule_id = @rule_id;"""
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters AnyQuantityIsAcceptable
        |> Result.map ignore

//=================================================
// Person and positions clean up
//=================================================
(* Route tests commit, and they address what they make by name, as the routes do. So these delete by name: each
   takes the rows that hang off the entity first, then the entity. A name that matches nothing deletes nothing. *)

let private executeCleanUpStatements (statements: (string * QueryParameter list) list) : Result<unit, IAppError> =
    let context = Context.create NoTransaction FetchOnly
    statements
    |> List.fold
        (fun acc (query, parameters) ->
            acc
            |> Result.bind (fun () ->
                executeNonQuery (context |> Context.getDatabaseTransaction) query parameters AnyQuantityIsAcceptable
                |> Result.map ignore))
        (Ok())

/// Deletes an Investment Account by name, with its activities, its snapshots and their lines and lots, its Holdings
/// and its owner rows.
let cleanUpInvestmentAccountByName (name: string) : Result<unit, IAppError> =
    let parameters = [ { name = "@name"; value = CharString name } ]
    let ofAccount = "(select unique_id from positions.investment_account where account_name = @name)"
    [ $"""delete from positions.investment_activity WHERE investment_account_id IN {ofAccount};"""
      $"""delete from positions.account_snapshot_line WHERE account_snapshot_id IN
            (select unique_id from positions.account_snapshot where investment_account_id IN {ofAccount});"""
      $"""delete from positions.account_snapshot WHERE investment_account_id IN {ofAccount};"""
      $"""delete from positions.holding WHERE investment_account_id IN {ofAccount};"""
      $"""delete from positions.investment_account_owner WHERE investment_account_id IN {ofAccount};"""
      """delete from positions.investment_account WHERE account_name = @name;""" ]
    |> List.map (fun query -> query, parameters)
    |> executeCleanUpStatements

/// Deletes a Security by name, with any Holding of it and any snapshot line or activity on such a Holding.
let cleanUpSecurityByName (name: string) : Result<unit, IAppError> =
    let parameters = [ { name = "@name"; value = CharString name } ]
    let holdingsOf = "(select unique_id from positions.holding where security_id IN (select unique_id from positions.security where security_name = @name))"
    [ $"""delete from positions.investment_activity WHERE holding_id IN {holdingsOf};"""
      $"""delete from positions.account_snapshot_line WHERE holding_id IN {holdingsOf};"""
      $"""delete from positions.holding WHERE unique_id IN {holdingsOf};"""
      """delete from positions.security WHERE security_name = @name;""" ]
    |> List.map (fun query -> query, parameters)
    |> executeCleanUpStatements

/// Deletes the Pre-ledger Balance of the ledger account with the code, dated so. One that doesn't exist deletes nothing.
let cleanUpPreLedgerBalance (accountCode: string) (balanceDate: NodaTime.LocalDate) : Result<unit, IAppError> =
    [ """delete from positions.pre_ledger_balance WHERE balance_date = @balance_date
            AND ledger_account_id IN (select unique_id from ledger.account where code = @code);""",
      [ { name = "@code"; value = CharString accountCode }; { name = "@balance_date"; value = DbLocalDate balanceDate } ] ]
    |> executeCleanUpStatements

/// Deletes a Dimension Value by dimension and name. A Security still pointing at it must go first.
let cleanUpDimensionValueByName (dimension: string) (name: string) : Result<unit, IAppError> =
    [ """delete from positions.dimension_value WHERE dimension = @dimension AND value_name = @name;""",
      [ { name = "@dimension"; value = CharString dimension }; { name = "@name"; value = CharString name } ] ]
    |> executeCleanUpStatements

/// Deletes a Property by name, with its Valuations, its asset and mortgage links and its owner rows.
let cleanUpPropertyByName (name: string) : Result<unit, IAppError> =
    let parameters = [ { name = "@name"; value = CharString name } ]
    let ofProperty = "(select unique_id from positions.property where property_name = @name)"
    [ $"""delete from positions.valuation WHERE property_id IN {ofProperty};"""
      $"""delete from positions.property_mortgage_account WHERE property_id IN {ofProperty};"""
      $"""delete from positions.property_asset_account WHERE property_id IN {ofProperty};"""
      $"""delete from positions.property_owner WHERE property_id IN {ofProperty};"""
      """delete from positions.property WHERE property_name = @name;""" ]
    |> List.map (fun query -> query, parameters)
    |> executeCleanUpStatements

/// Deletes a Person by name. Accounts and Properties that list the Person as an owner must go first.
let cleanUpPersonByName (name: string) : Result<unit, IAppError> =
    [ """delete from general.person WHERE person_name = @name;""", [ { name = "@name"; value = CharString name } ] ]
    |> executeCleanUpStatements

/// Runs every clean up, even after one fails, and reports all the failures together.
let cleanUpAll (cleanUps: (unit -> Result<unit, IAppError>) list) : Result<unit, IAppError> =
    cleanUps
    |> List.map (fun cleanUp -> cleanUp ())
    |> List.choose (function
        | Error e -> Some(e.ToMessage())
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors -> Error(TestingError $"""One or more clean ups failed||{errors |> String.concat "||"}""")
