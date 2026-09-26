module Ui.InterfaceBridge.BoundaryConverters.CashFlowLookupConverters

open App.Utility.IAppError
open App.DataAccessLayer.DalError
open App.Utility.Result
open App.DataAccessLayer
open App.Session
open Business.FinancialServices.CashFlow.CashFlowError
open Business.FinancialServices.CashFlow.CashFlowComponent

let private fallibleConverterAgreementNameStringToMasterAgreementUuid context nameString =
    result {
        // see if the string represents a valid name first
        let! _ = nameString |> AgreementName.create
        // now see if it matches a master agreement ID
        return!
            nameString
            |> LookupCache.masterAgreementNameToId.fetch (context |> Context.getDatabaseTransaction)
            |> whenNoRows (CashflowAgreementNameDoesntMatchId nameString)
    }

let ``convert [AgreementNameString] to [MasterAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<MasterAgreementId, IAppError> =
    result {
        let! uuid = nameString |> fallibleConverterAgreementNameStringToMasterAgreementUuid context
        return uuid |> MasterAgreementId.fromGuid
    }

let ``convert [MasterAgreementId] to [AgreementNameString]``
    (context: Context.Context)
    (masterAgreementId: MasterAgreementId)
    : Result<string, IAppError> =
    masterAgreementId
    |> MasterAgreementId.value
    |> LookupCache.masterAgreementIdToName.fetch (context |> Context.getDatabaseTransaction)

let private fallibleConverterPaymentAgreementNameStringToPaymentAgreementUuid context nameString =
    result {
        // see if the string represents a valid name first
        let! _ = nameString |> PaymentAgreementName.create
        // now see if it matches a payment agreement ID
        return!
            nameString
            |> LookupCache.paymentAgreementNameToId.fetch (context |> Context.getDatabaseTransaction)
            |> whenNoRows (CashflowPaymentAgreementNameDoesntMatchId nameString)
    }

let ``convert [PaymentAgreementNameString] to [PaymentAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<PaymentAgreementId, IAppError> =
    result {
        let! uuid = nameString |> fallibleConverterPaymentAgreementNameStringToPaymentAgreementUuid context
        return uuid |> PaymentAgreementId.fromGuid
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
    |> PaymentAgreementId.value
    |> LookupCache.paymentAgreementIdToName.fetch (context |> Context.getDatabaseTransaction)

let ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]``
    (context: Context.Context)
    (paymentAgreementIdOption: PaymentAgreementId option)
    : Result<string option, IAppError> =
    paymentAgreementIdOption
    |> convertOptionToDesiredTypeWithFallibleConverter
        (``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context)
