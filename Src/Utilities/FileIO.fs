module Utilities.FileIO

open System.IO
open Utilities.AppError

let createFullPath
    (baseDir: string)
    (fileName: string)
    : Result<string, AppError> =
    try
        Path.Combine(baseDir, fileName) |> Ok
    with ex ->
        Error(FileIoError ex)

let confirmDirectoryExists
    (dir: string)
    : Result<unit, AppError> =
    try
        match dir |> Directory.Exists with
        | true -> Ok ()
        | false -> Error(FileIoDirectoryDoesntExist dir)
    with ex ->
        Error(FileIoError ex)
        
let confirmFileExists
    (fullFilePath: string)
    : Result<unit, AppError> =
    try
        match fullFilePath |> File.Exists with
        | true -> Ok ()
        | false -> Error(FileIoFileDoesntExist fullFilePath)
    with ex ->
        Error(FileIoError ex)

let readTextFileLines
    (fullPath: string)
    : Result<string list, AppError> =
    try 
        File.ReadAllLines(fullPath)
        |> Array.toList
        |> Ok
    with ex ->
        Error(FileIoError ex)

let writeTextFile
    (fullPath: string)
    (text: string)
    : Result<unit, AppError> =
    try 
        File.WriteAllText(fullPath, text)
        Ok ()
    with ex ->
        Error(FileIoError ex)

let moveFile
    (oldPath: string)
    (newPath: string)
    : Result<unit, AppError> =
    try 
        File.Move(oldPath, newPath)
        Ok ()
    with ex ->
        Error(FileIoError ex)
