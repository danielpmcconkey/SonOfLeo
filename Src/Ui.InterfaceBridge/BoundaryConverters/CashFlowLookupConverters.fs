module Ui.InterfaceBridge.BoundaryConverters.CashFlowLookupConverters

open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer
open App.DataAccessLayer.DalError
open App.Session
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowError
open Business.FinancialServices.CashFlow.CashFlowComponent

let ``convert [AgreementNameString] to [MasterAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<MasterAgreementId, IAppError> =
    result {
        // see if the string represents a valid name first, then look up the trimmed name it gives
        let! name = nameString |> AgreementName.create
        return!
            name
            |> MasterAgreement.nameToId context
            |> whenNoRows (CashflowAgreementNameDoesntMatchId nameString)
    }

let ``convert [MasterAgreementId] to [AgreementNameString]``
    (context: Context.Context)
    (masterAgreementId: MasterAgreementId)
    : Result<string, IAppError> =
    masterAgreementId
    |> MasterAgreement.idToName context
    |> Result.map AgreementName.value

let ``convert [PaymentAgreementNameString] to [PaymentAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<PaymentAgreementId, IAppError> =
    result {
        // see if the string represents a valid name first, then look up the trimmed name it gives
        let! name = nameString |> PaymentAgreementName.create
        return!
            name
            |> PaymentAgreement.nameToId context
            |> whenNoRows (CashflowPaymentAgreementNameDoesntMatchId nameString)
    }

let ``convert [PaymentAgreementNameString option] to [PaymentAgreementId option]``
    (context: Context.Context)
    (nameStringOption: string option)
    : Result<PaymentAgreementId option, IAppError> =
    nameStringOption
    |> convertOptionToDesiredTypeWithFallibleConverter (
        ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context)

let ``convert [PaymentAgreementId] to [PaymentAgreementNameString]``
    (context: Context.Context)
    (paymentAgreementId: PaymentAgreementId)
    : Result<string, IAppError> =
    paymentAgreementId
    |> PaymentAgreement.idToName context
    |> Result.map PaymentAgreementName.value

let ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]``
    (context: Context.Context)
    (paymentAgreementIdOption: PaymentAgreementId option)
    : Result<string option, IAppError> =
    paymentAgreementIdOption
    |> convertOptionToDesiredTypeWithFallibleConverter
        (``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context)
