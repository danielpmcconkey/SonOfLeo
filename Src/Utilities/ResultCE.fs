namespace Utilities

// computational expressions for more elegant results binding and mapping
module ResultCE =
    type ResultBuilder() =
      member _.Bind(result, f) = Result.bind f result
      member _.Return(value) = Ok value
      member _.ReturnFrom(result) = result
      member _.Zero() = Ok ()

    let result = ResultBuilder()

