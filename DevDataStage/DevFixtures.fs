module DevDataStage.DevFixtures

open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Utilities
open Utilities.AppError
open Utilities.ResultHelper

let stageDevData (context: Context.Context) (data: FixtureData) =
    let result =
        result {
            let today = Calendar.today()
            let yesterday = today.PlusDays(-1)

            let testBankSource = data.ingestionSources |> List.find (fun s -> s |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")

            let food5350Code = data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-5350") |> Account.code |> AccountCode.value
            let moneyMarket1270Code = data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-1270") |> Account.code |> AccountCode.value
            let entertainment5650Code = data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-5650") |> Account.code |> AccountCode.value

            let ing = StagedEntryStatus.Ingested |> StagedEntryStatus.toString
            let cls = StagedEntryStatus.Classified |> StagedEntryStatus.toString
            let rev = StagedEntryStatus.Reviewed |> StagedEntryStatus.toString
            let ign = StagedEntryStatus.Ignored |> StagedEntryStatus.toString
            let dup = StagedEntryStatus.Duplicate |> StagedEntryStatus.toString
            let nom = StagedEntryStatus.NoMatch |> StagedEntryStatus.toString
            let con = StagedEntryStatus.Conflict |> StagedEntryStatus.toString
            let posted = StagedEntryStatus.Posted |> StagedEntryStatus.toString
            let si = StageStatusChangeMechanism.StageIngestion |> StageStatusChangeMechanism.toString
            let cl = StageStatusChangeMechanism.Classifier |> StageStatusChangeMechanism.toString
            let op = StageStatusChangeMechanism.Operator |> StageStatusChangeMechanism.toString
            let lp = StageStatusChangeMechanism.LedgerPoster |> StageStatusChangeMechanism.toString

            let twoLines code1 code2 amt =
                [ (amt, "Debit", Some code1, None, None)
                  (amt, "Credit", Some code2, None, None) ]

            let twoLinesOneNull code amt =
                [ (amt, "Debit", None, None, None)
                  (amt, "Credit", Some code, None, None) ]

            let mutable count = 0

            // Ingested
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Ingested" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLinesOneNull moneyMarket1270Code 42.00M)
                    [(None, ing, Clock.now(), si)]
            count <- count + 1

            // Classified
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Classified" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLines food5350Code moneyMarket1270Code 150.00M)
                    [(None, ing, Clock.now(), si); (Some ing, cls, Clock.now(), cl)]
            count <- count + 1

            // NoMatch
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - NoMatch" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLinesOneNull moneyMarket1270Code 33.00M)
                    [(None, ing, Clock.now(), si); (Some ing, nom, Clock.now(), cl)]
            count <- count + 1

            // Conflict
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Conflict" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLinesOneNull moneyMarket1270Code 55.00M)
                    [(None, ing, Clock.now(), si); (Some ing, con, Clock.now(), cl)]
            count <- count + 1

            // Reviewed
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Reviewed" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLines entertainment5650Code moneyMarket1270Code 75.00M)
                    [(None, ing, Clock.now(), si); (Some ing, cls, Clock.now(), cl); (Some cls, rev, Clock.now(), op)]
            count <- count + 1

            // Duplicate
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Duplicate" "TXN-001"
                    testBankSource yesterday
                    (twoLines food5350Code moneyMarket1270Code 50.00M)
                    [(None, ing, Clock.now(), si); (Some ing, dup, Clock.now(), si)]
            count <- count + 1

            // Ignored
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Ignored" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLines food5350Code moneyMarket1270Code 30.00M)
                    [(None, ing, Clock.now(), si); (Some ing, ign, Clock.now(), op)]
            count <- count + 1

            // Posted
            let! _ =
                createStageEntryForTest context "/tmp/dev-fixture.dat"
                    "Dev fixture - Posted" (System.Guid.NewGuid().ToString())
                    testBankSource yesterday
                    (twoLines food5350Code moneyMarket1270Code 100.00M)
                    [(None, ing, Clock.now(), si); (Some ing, cls, Clock.now(), cl); (Some cls, posted, Clock.now(), lp)]
            count <- count + 1

            return count
        }
    result |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
