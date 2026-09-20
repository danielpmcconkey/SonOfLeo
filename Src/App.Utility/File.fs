module App.Utility.File

open System.IO
open App.Utility.UtilityError

let createFullPath
    (baseDir: string)
    (fileName: string)
    : Result<string, UtilityError> =
    try
        Path.Combine(baseDir, fileName) |> Ok
    with ex ->
        Error(FileIoError ex)

let confirmDirectoryExists
    (dir: string)
    : Result<unit, UtilityError> =
    try
        match dir |> Directory.Exists with
        | true -> Ok ()
        | false -> Error(FileIoDirectoryDoesntExist dir)
    with ex ->
        Error(FileIoError ex)
        
let confirmFileExists
    (fullFilePath: string)
    : Result<unit, UtilityError> =
    try
        match fullFilePath |> File.Exists with
        | true -> Ok ()
        | false -> Error(FileIoFileDoesntExist fullFilePath)
    with ex ->
        Error(FileIoError ex)

let readTextFileLines
    (fullPath: string)
    : Result<string list, UtilityError> =
    try 
        File.ReadAllLines(fullPath)
        |> Array.toList
        |> Ok
    with ex ->
        Error(FileIoError ex)

let writeTextFile
    (fullPath: string)
    (text: string)
    : Result<unit, UtilityError> =
    try 
        File.WriteAllText(fullPath, text)
        Ok ()
    with ex ->
        Error(FileIoError ex)

let moveFile
    (oldPath: string)
    (newPath: string)
    : Result<unit, UtilityError> =
    try 
        File.Move(oldPath, newPath)
        Ok ()
    with ex ->
        Error(FileIoError ex)
