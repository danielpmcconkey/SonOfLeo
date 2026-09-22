module App.Utility.File

open System.IO
open App.Utility.IAppError
open App.Utility.UtilityError

let createFullPath
    (baseDir: string)
    (fileName: string)
    : Result<string, IAppError> =
    try
        Path.Combine(baseDir, fileName) |> Ok
    with ex ->
        Error(FileIoError ex)

let confirmDirectoryExists
    (dir: string)
    : Result<unit, IAppError> =
    try
        match dir |> Directory.Exists with
        | true -> Ok ()
        | false -> Error(FileIoDirectoryDoesntExist dir)
    with ex ->
        Error(FileIoError ex)
        
let confirmFileExists
    (fullFilePath: string)
    : Result<unit, IAppError> =
    try
        match fullFilePath |> File.Exists with
        | true -> Ok ()
        | false -> Error(FileIoFileDoesntExist fullFilePath)
    with ex ->
        Error(FileIoError ex)

let readTextFileLines
    (fullPath: string)
    : Result<string list, IAppError> =
    try 
        File.ReadAllLines(fullPath)
        |> Array.toList
        |> Ok
    with ex ->
        Error(FileIoError ex)

let writeTextFile
    (fullPath: string)
    (text: string)
    : Result<unit, IAppError> =
    try 
        File.WriteAllText(fullPath, text)
        Ok ()
    with ex ->
        Error(FileIoError ex)

let moveFile
    (oldPath: string)
    (newPath: string)
    : Result<unit, IAppError> =
    try 
        File.Move(oldPath, newPath)
        Ok ()
    with ex ->
        Error(FileIoError ex)
