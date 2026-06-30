namespace Tests.Integrated

open System
open Xunit
open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.FiscalPeriods
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open Model.Ledger.Accounts.AccountComponent
open Tests.Integrated._Cleanup

type FixtureData = {
    assets1000Id: Guid
    liabilities2000Id: Guid
    equity3000Id: Guid
    revenue4000Id: Guid
    expenses5000Id: Guid
    rothIra1250Id: Guid
    moneyMarket1270Id: Guid
    mortgage2210Id: Guid
    creditCard2220Id: Guid
    retirement3030Id: Guid
    personalRevenue4290Id: Guid
    food5350Id: Guid
    entertainment5650Id: Guid
    closedBank1290Id: Guid
    fiscalPeriodIds: Guid list
}

type TestDataFixture() =
    let data =
        let today = Calendar.today()
        let lastYear = today.PlusYears(-1)
        let envelope = AuditEnvelope.create AccountCreate
        let twoMonthsAgo = today.PlusMonths(-2)

        let stageResult = result {

            // =============================================================================
            // Create accounts
            // =============================================================================

            let! assets1000 =
                Account.constructNewAndSaveToDb "F-1000" "Assets" "Asset"
                    lastYear None None None None envelope None
            let! liabilities2000 =
                Account.constructNewAndSaveToDb "F-2000" "Liabilities" "Liability"
                    lastYear None None None None envelope None
            let! equity3000 =
                Account.constructNewAndSaveToDb "F-3000" "Equity" "Equity"
                    lastYear None None None None envelope None
            let! revenue4000 =
                Account.constructNewAndSaveToDb "F-4000" "Revenue" "Revenue"
                    lastYear None None None None envelope None
            let! expenses5000 =
                Account.constructNewAndSaveToDb "F-5000" "Expenses" "Expense"
                    lastYear None None None None envelope None

            let assets1000Id = assets1000 |> Account.uniqueId
            let liabilities2000Id = liabilities2000 |> Account.uniqueId
            let equity3000Id = equity3000 |> Account.uniqueId
            let revenue4000Id = revenue4000 |> Account.uniqueId
            let expenses5000Id = expenses5000 |> Account.uniqueId

            let! rothIra1250 =
                Account.constructNewAndSaveToDb "F-1250" "Roth IRA" "Asset"
                    lastYear None (Some "Investment") (Some assets1000Id) None envelope None
            let! moneyMarket1270 =
                Account.constructNewAndSaveToDb "F-1270" "Money Market" "Asset"
                    lastYear None (Some "Cash") (Some assets1000Id) None envelope None
            let! mortgage2210 =
                Account.constructNewAndSaveToDb "F-2210" "Mortgage Payable" "Liability"
                    lastYear None (Some "LongTermLiability") (Some liabilities2000Id) None envelope None
            let! creditCard2220 =
                Account.constructNewAndSaveToDb "F-2220" "Credit Card" "Liability"
                    lastYear None (Some "CurrentLiability") (Some liabilities2000Id) None envelope None
            let! retirement3030 =
                Account.constructNewAndSaveToDb "F-3030" "Retirement Contributions" "Equity"
                    lastYear None None (Some equity3000Id) None envelope None
            let! personalRevenue4290 =
                Account.constructNewAndSaveToDb "F-4290" "Personal Revenue" "Revenue"
                    lastYear None (Some "OperatingRevenue") (Some revenue4000Id) None envelope None
            let! food5350 =
                Account.constructNewAndSaveToDb "F-5350" "Food" "Expense"
                    lastYear None (Some "OperatingExpense") (Some expenses5000Id) None envelope None
            let! entertainment5650 =
                Account.constructNewAndSaveToDb "F-5650" "Entertainment" "Expense"
                    lastYear None (Some "OperatingExpense") (Some expenses5000Id) None envelope None
            let! closedBank1290 =
                Account.constructNewAndSaveToDb "F-1290" "Closed Bank" "Asset"
                    lastYear (Some twoMonthsAgo) (Some "Cash") (Some assets1000Id) None envelope None

            // =============================================================================
            // Create fiscal periods
            // =============================================================================

            let! fiscalPeriods =
                [-4..4]
                |> List.map (fun x ->
                    let date = x |> today.PlusMonths
                    let monthF = date.Month.ToString("D2")
                    let key = $"{date.Year}-{monthF}"
                    FiscalPeriod.constructNewAndSaveToDb key envelope None)
                |> ListHelper.listOfResultsToResultsList

            return {
                assets1000Id = assets1000Id
                liabilities2000Id = liabilities2000Id
                equity3000Id = equity3000Id
                revenue4000Id = revenue4000Id
                expenses5000Id = expenses5000Id
                rothIra1250Id = rothIra1250 |> Account.uniqueId
                moneyMarket1270Id = moneyMarket1270 |> Account.uniqueId
                mortgage2210Id = mortgage2210 |> Account.uniqueId
                creditCard2220Id = creditCard2220 |> Account.uniqueId
                retirement3030Id = retirement3030 |> Account.uniqueId
                personalRevenue4290Id = personalRevenue4290 |> Account.uniqueId
                food5350Id = food5350 |> Account.uniqueId
                entertainment5650Id = entertainment5650 |> Account.uniqueId
                closedBank1290Id = closedBank1290 |> Account.uniqueId
                fiscalPeriodIds = fiscalPeriods |> List.map FiscalPeriod.uniqueId
            }
        }
        stageResult |> Result.defaultWith failwith

    member _.Data = data

    interface IDisposable with
        member _.Dispose() =
            // children first, then parents, then fiscal periods
            let childIds =
                [ data.rothIra1250Id; data.moneyMarket1270Id; data.closedBank1290Id
                  data.mortgage2210Id; data.creditCard2220Id
                  data.retirement3030Id
                  data.personalRevenue4290Id
                  data.food5350Id; data.entertainment5650Id ]
            let parentIds =
                [ data.assets1000Id; data.liabilities2000Id; data.equity3000Id
                  data.revenue4000Id; data.expenses5000Id ]

            childIds |> List.map (Some >> cleanUpAccountId) |> ignore
            parentIds |> List.map (Some >> cleanUpAccountId) |> ignore
            data.fiscalPeriodIds |> List.map (Some >> cleanUpFiscalPeriodId) |> ignore

[<CollectionDefinition("SharedTestData")>]
type SharedTestDataCollection() =
    interface ICollectionFixture<TestDataFixture>
