open System
open System.IO
open Microsoft.Extensions.Configuration
open Npgsql

let private config =
    ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional = false)
        .Build()

let private readPassword () =
    printf "Password: "
    let mutable password = ""
    let mutable reading = true
    while reading do
        let key = Console.ReadKey(intercept = true)
        if key.Key = ConsoleKey.Enter then
            reading <- false
            printfn ""
        elif key.Key = ConsoleKey.Backspace then
            if password.Length > 0 then
                password <- password.[..password.Length - 2]
                printf "\b \b"
        else
            password <- password + string key.KeyChar
            printf "*"
    password

let private appendPassword (connStr: string) (password: string) =
    $"{connStr};Password={password}"

let private sqlDir =
    let programDir = AppContext.BaseDirectory
    let rec findRepoRoot (dir: string) =
        if Directory.Exists(Path.Combine(dir, ".git")) then dir
        elif Path.GetPathRoot(dir) = dir then failwith "Could not locate repo root from program directory"
        else findRepoRoot (Directory.GetParent(dir).FullName)
    findRepoRoot programDir

type DmlRecord = {
    uniqueId: Guid
    upFile: string
    createdAt: DateTimeOffset
}

let private fetchRegisteredMigrations (conn: NpgsqlConnection) =
    use cmd = new NpgsqlCommand("select unique_id, up_file, created_at from migration.dml order by created_at", conn)
    use reader = cmd.ExecuteReader()
    let mutable rows = []
    while reader.Read() do
        rows <- { uniqueId = reader.GetGuid(0)
                  upFile = reader.GetString(1)
                  createdAt = reader.GetFieldValue<DateTimeOffset>(2) } :: rows
    rows |> List.rev

let private fetchAppliedIds (conn: NpgsqlConnection) (envId: int) =
    use cmd = new NpgsqlCommand(
        "select dml_id from migration.history where env_id = @env_id and action_type_id = 1", conn)
    cmd.Parameters.AddWithValue("@env_id", envId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable ids = Set.empty
    while reader.Read() do
        ids <- ids |> Set.add (reader.GetGuid(0))
    ids

let private registerFile (conn: NpgsqlConnection) (repoRelativePath: string) =
    let id = Guid.NewGuid()
    let now = DateTimeOffset.UtcNow
    use cmd = new NpgsqlCommand(
        "insert into migration.dml (unique_id, up_file, created_at) values (@id, @up_file, @created_at)", conn)
    cmd.Parameters.AddWithValue("@id", id) |> ignore
    cmd.Parameters.AddWithValue("@up_file", repoRelativePath) |> ignore
    cmd.Parameters.AddWithValue("@created_at", now) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    { uniqueId = id; upFile = repoRelativePath; createdAt = now }

let private recordHistory (conn: NpgsqlConnection) (dmlId: Guid) (envId: int) =
    use cmd = new NpgsqlCommand(
        "insert into migration.history (unique_id, dml_id, env_id, action_type_id, run_at) values (@id, @dml_id, @env_id, 1, @run_at)", conn)
    cmd.Parameters.AddWithValue("@id", Guid.NewGuid()) |> ignore
    cmd.Parameters.AddWithValue("@dml_id", dmlId) |> ignore
    cmd.Parameters.AddWithValue("@env_id", envId) |> ignore
    cmd.Parameters.AddWithValue("@run_at", DateTimeOffset.UtcNow) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private discoverSqlFiles () =
    let dir = Path.Combine(sqlDir, "DbMigration")
    if not (Directory.Exists dir) then failwith $"SQL directory not found: {dir}"
    Directory.GetFiles(dir, "*.sql")
    |> Array.map (fun fullPath ->
        let relativePath = Path.GetRelativePath(sqlDir, fullPath).Replace('\\', '/')
        relativePath)
    |> Array.sort

[<EntryPoint>]
let main _ =
    printfn "SonOfLeo Migrator"
    printfn ""
    printfn "  1. dev"
    printfn "  2. test"
    printfn "  3. prod"
    printfn ""
    printf "Select environment: "

    let envName, envId =
        match Console.ReadLine().Trim() with
        | "1" | "dev" -> "dev", 1
        | "2" | "test" -> "test", 2
        | "3" | "prod" -> "prod", 3
        | other ->
            printfn $"Unknown selection: {other}"
            exit 1

    let targetConnStrBase = config.[$"Connections:{envName}"]
    let migConnStrBase = config.["MigrationDb"]

    if String.IsNullOrWhiteSpace targetConnStrBase then
        printfn $"No connection string configured for '{envName}' in appsettings.json"
        exit 1

    printfn ""
    let password = readPassword ()
    printfn ""

    let targetConnStr = appendPassword targetConnStrBase password
    let migConnStr = appendPassword migConnStrBase password

    printfn $"Targeting: {envName}"
    printfn ""

    use migConn = new NpgsqlConnection(migConnStr)
    migConn.Open()

    let filesOnDisk = discoverSqlFiles ()
    let registered = fetchRegisteredMigrations migConn
    let registeredPaths = registered |> List.map (fun r -> r.upFile) |> Set.ofList

    let newFiles = filesOnDisk |> Array.filter (fun f -> not (registeredPaths.Contains f))
    for f in newFiles do
        printfn $"  Registering: {f}"
        registerFile migConn f |> ignore

    let allMigrations = fetchRegisteredMigrations migConn
    let applied = fetchAppliedIds migConn envId
    let pending = allMigrations |> List.filter (fun m -> not (applied.Contains m.uniqueId))

    if pending.IsEmpty then
        printfn "Nothing to apply."
        0
    else
        printfn $"{pending.Length} migration(s) pending."
        use targetConn = new NpgsqlConnection(targetConnStr)
        targetConn.Open()

        let mutable failed = false
        for migration in pending do
            if not failed then
                let fullPath = Path.Combine(sqlDir, migration.upFile)
                if not (File.Exists fullPath) then
                    printfn $"  ERROR: file not found: {fullPath}"
                    failed <- true
                else
                    let sql = File.ReadAllText(fullPath).Replace("{ENV}", envName)
                    printfn $"  Applying: {migration.upFile}"
                    try
                        use cmd = new NpgsqlCommand(sql, targetConn)
                        cmd.ExecuteNonQuery() |> ignore
                        recordHistory migConn migration.uniqueId envId
                        printfn $"  OK"
                    with ex ->
                        printfn $"  FAILED: {ex.Message}"
                        failed <- true

        if failed then
            printfn "Stopped on first failure."
            1
        else
            printfn "All migrations applied."
            0
