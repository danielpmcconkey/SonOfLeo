module App.Utility.AppError

open System
open NodaTime

type AppError = // todo: turn AppError into an interface and remove upper level domain knowledge
    
    /// TestingError is NEVER to be used in the Src directory. It is only here to facilitate automated testing. Such as
    /// when I need to assert that somewthing was supposed to fail but it doesn't.
    | TestingError of string
    

    


    | CliUnknownCommand of string * string
    
    
    
    
    
    
    | InterfaceBridgeConversionFailure of string * string * string * string
    
    
    
    | ReportingUnknownReportName of string

module AppError =
    let toMessage =
        function

            | TestingError message -> message
            
            
            | CliUnknownCommand(domain, verb) -> $"Unknown command: {domain} {verb}"
            
            
            | InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError) -> $"Failed conversion in InterfaceBridge. Original type: {originalType}. Desired type: {desiredType}. Original value: {originalValue}. Additional details: {childError}"
                                        
            | ReportingUnknownReportName name -> $"Unknown report: {name}."

