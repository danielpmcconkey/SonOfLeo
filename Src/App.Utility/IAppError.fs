module App.Utility.IAppError

type IAppError =
    abstract member ToMessage: unit -> string
