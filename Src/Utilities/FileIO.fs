module Utilities.FileIO

open System.IO
open Utilities.AppError

let writeTextFile
    (baseDir: string)
    (fileName: string)
    (fileExtension: string)
    (text: string)
    : Result<string, AppError> =
    try 
        let path = Path.Combine(baseDir, $"{fileName}.{fileExtension}") 
        File.WriteAllText(path, text)
        Ok path
    with ex ->
        Error(FileIoError ex)
    
