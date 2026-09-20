module Ui.InterfaceBridge.BoundaryConverters.CashFlowLookupConverters

open App.Utility.AppError
open App.Utility.Result
open App.DataAccessLayer
open App.Session
open Business.FinancialServices.CashFlow.CashFlowComponent

let private fallibleConverterAgreementNameStringToMasterAgreementUuid context nameString =
    result {
        // see if the string represents a valid name first
        let! _ = nameString |> AgreementName.create
        // now see if it matches a master agreement ID
        return!
            match nameString
                  |> LookupCache.masterAgreementNameToId.fetch (context |> Context.getDatabaseTransaction) with
            | Ok x -> Ok x
            | Error(DalResultantRowsDidntMatchExpectation _) -> Error(CashflowAgreementNameDoesntMatchId nameString)
            | Error e -> Error e
    }

let ``convert [AgreementNameString] to [MasterAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<MasterAgreementId, AppError> =
    result {
        let! uuid = nameString |> fallibleConverterAgreementNameStringToMasterAgreementUuid context
        return uuid |> MasterAgreementId.fromGuid
    }

let ``convert [MasterAgreementId] to [AgreementNameString]``
    (context: Context.Context)
    (masterAgreementId: MasterAgreementId)
    : Result<string, AppError> =
    masterAgreementId
    |> MasterAgreementId.value
    |> LookupCache.masterAgreementIdToName.fetch (context |> Context.getDatabaseTransaction)

let private fallibleConverterPaymentAgreementNameStringToPaymentAgreementUuid context nameString =
    result {
        // see if the string represents a valid name first
        let! _ = nameString |> PaymentAgreementName.create
        // now see if it matches a payment agreement ID
        return!
            match nameString
                  |> LookupCache.paymentAgreementNameToId.fetch (context |> Context.getDatabaseTransaction) with
            | Ok x -> Ok x
            | Error(DalResultantRowsDidntMatchExpectation _) ->
                Error(CashflowPaymentAgreementNameDoesntMatchId nameString)
            | Error e -> Error e
    }

let ``convert [PaymentAgreementNameString] to [PaymentAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<PaymentAgreementId, AppError> =
    result {
        let! uuid = nameString |> fallibleConverterPaymentAgreementNameStringToPaymentAgreementUuid context
        return uuid |> PaymentAgreementId.fromGuid
    }

let ``convert [PaymentAgreementNameString option] to [PaymentAgreementId option]``
    (context: Context.Context)
    (nameStringOption: string option)
    : Result<PaymentAgreementId option, AppError> =
    nameStringOption
    |> convertOptionToDesiredTypeWithFallibleConverter (
        ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context)

let ``convert [PaymentAgreementId] to [PaymentAgreementNameString]``
    (context: Context.Context)
    (paymentAgreementId: PaymentAgreementId)
    : Result<string, AppError> =
    paymentAgreementId
    |> PaymentAgreementId.value
    |> LookupCache.paymentAgreementIdToName.fetch (context |> Context.getDatabaseTransaction)

let ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]``
    (context: Context.Context)
    (paymentAgreementIdOption: PaymentAgreementId option)
    : Result<string option, AppError> =
    paymentAgreementIdOption
    |> convertOptionToDesiredTypeWithFallibleConverter
        (``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context)
