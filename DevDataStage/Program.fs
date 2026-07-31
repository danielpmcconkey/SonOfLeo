
open Context.Context
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteScalar
open Logger.Audit
open Tests.Helpers
open Utilities.AppError

(* The stage's first act is a TRUNCATE CASCADE over every ledger table. This asks the
 database it is about to truncate for its own name, over the same connection the stage
 will use, and refuses anything but dev. Do not soften this back into a reminder. *)
let private expectedDatabase = "sonofleo_dev"

let private context = create NoTransaction FetchOnly

let private actualDatabase =
  executeScalar (context |> getDatabaseTransaction) "select current_database()" [] stringUnboxing
  |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

if actualDatabase <> expectedDatabase then
  failwith
      $"Refusing to stage. appsettings.json resolves to database '{actualDatabase}', expected
'{expectedDatabase}'."

let fixture = TestDataFixture()
let data = fixture.Data
printfn $"Staged %d{data.totalAccounts} accounts, %d{data.totalFiscalPeriods} fiscal periods,
%d{data.totalJournalEntryHeaders} journal entries."
