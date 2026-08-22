module InterfaceBridge.InterfaceContracts.ReportsContracts

open NodaTime

type ReportAsOf = { asOf: LocalDate }

type OutputPathInput = {
    baseDir: string
    interpolateAsOf: bool // if true, write the as-of in YYYY.MM.DD format between the fileName and file extension
    fileName: string
}
type OutputPathReturn = { fullyQualifiedPath: string }
    
type OutputSpecifier =
    | DataOnly
    | Report of OutputPathInput

type TrialBalanceInput = { asOf: ReportAsOf; reportOutput: OutputSpecifier }

type TrialBalanceReturnRow =
    { accountCode: string
      accountName: string
      generation: int
      totalCredits: decimal
      totalDebits: decimal
      netBalance: decimal }

type TrialBalanceReturn = 
    | DataOnly of TrialBalanceReturnRow list
    | Report of OutputPathReturn
    

