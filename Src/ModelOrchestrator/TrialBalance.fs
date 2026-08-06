module ModelOrchestrator.TrialBalanceReport

open Context.Context
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper

type TrialBalanceRowNested =
    { accountCode: AccountCode
      accountName: AccountName
      generation: int
      totalCredits: Money
      totalDebits: Money
      netBalance: Money
      children:  TrialBalanceRowNested list }

type TrialBalanceRowFlattened =
    { accountCode: AccountCode
      accountName: AccountName
      generation: int
      totalCredits: Money
      totalDebits: Money
      netBalance: Money }
    
let rec private crawlAndCompile
    (accountToCrawl: Account)
    (allAccounts: Account list)
    (allAccountBalances: AccountBalance.AccountBalance list)
    (thisGeneration: int)
    : Result<TrialBalanceRowNested, AppError> =
    let balanceRowForThisAccount = allAccountBalances |> List.filter(fun ab -> ab.accountId = (accountToCrawl |> Account.accountId)) |> List.head
    let creditsForThisAccount = balanceRowForThisAccount.totalCredits
    let debitsForThisAccount = balanceRowForThisAccount.totalDebits
    let netForThisAccount = balanceRowForThisAccount.netBalance
    let children = allAccounts |> List.filter(fun a -> a |> Account.parentId = (accountToCrawl |> Account.accountId |> Some))
    if children |> List.isEmpty
    then
        // you're a bottom rung, just add your own tallies
        Ok { accountCode = accountToCrawl |> Account.code
             accountName = accountToCrawl |> Account.accountName
             generation = thisGeneration
             totalCredits = creditsForThisAccount
             totalDebits = debitsForThisAccount
             netBalance = netForThisAccount
             children = [] }
    else result {
        // send each child through the recursion loop
        let! compiledChildren =
            children
            |> List.map(fun child -> crawlAndCompile child allAccounts allAccountBalances (thisGeneration + 1))
            |> convertListOfResultsToResultsList
        // sum the credits, debits, and net across all children
        let! sumChildrenCredits =
            compiledChildren
            |> List.map(_.totalCredits)
            |> Money.sumList
        let! sumChildrenDebits =
            compiledChildren
            |> List.map(_.totalDebits)
            |> Money.sumList
        let! sumChildrenBalances =
            compiledChildren
            |> List.map(_.netBalance)
            |> Money.sumList
        let! totalCredits = sumChildrenCredits |> Money.add creditsForThisAccount
        let! totalDebits = sumChildrenDebits |> Money.add debitsForThisAccount
        let! totalNet = sumChildrenBalances |> Money.add netForThisAccount
        // create the parent row
        return
               { accountCode = accountToCrawl |> Account.code
                 accountName = accountToCrawl |> Account.accountName
                 generation = thisGeneration
                 totalCredits = totalCredits
                 totalDebits = totalDebits
                 netBalance = totalNet
                 children = compiledChildren } }

let rec private flattenNestedTrialBalance
    (nested: TrialBalanceRowNested)
    : TrialBalanceRowFlattened list =
    let selfFlattened = 
        { accountCode = nested.accountCode
          accountName = nested.accountName
          generation = nested.generation
          totalCredits = nested.totalCredits
          totalDebits = nested.totalDebits
          netBalance = nested.netBalance }
    selfFlattened:: (nested.children |> List.collect flattenNestedTrialBalance)
    
let fetchTrialBalanceData
    (context: Context)
    (asOf: LocalDate)
    : Result<TrialBalanceRowFlattened list, AppError> =
    result {
        let! accountBalances = AccountBalance.fetchByAccountIdList context None (Some asOf)
        let! allAccounts = Account.fetchAll context false
        let topLevelParents = allAccounts |> List.filter(fun a -> a |> Account.parentId |> Option.isNone)
        let! nestedAndSeparated =
            topLevelParents
            |> List.map (fun a -> crawlAndCompile a allAccounts accountBalances 0)
            |> convertListOfResultsToResultsList
        let flattenedAndSeparated = nestedAndSeparated |> List.map (fun x -> x |> flattenNestedTrialBalance)
        let allOneList = flattenedAndSeparated |> List.collect id
        let allOneListSorted = allOneList |> List.sortBy(_.accountCode)
        return allOneListSorted
    }
