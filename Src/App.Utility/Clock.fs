module App.Utility.Clock

open System.Globalization
open NodaTime
open App.Utility.IAppError
open App.Utility.UtilityError
open App.Utility.Config


let timeZoneSetting = "LocalizedTimeZone"

/// configuredTimeZone is the one zone every Instant is read in. There is no fallback: a missing or unrecognised setting
/// is an error, which the interfaces check before running any command.
let configuredTimeZone : Lazy<Result<DateTimeZone, IAppError>> =
    lazy
        (getConfigValue<string> timeZoneSetting
         |> Result.bind (fun zoneId ->
             match zoneId |> Option.ofObj |> Option.bind (DateTimeZoneProviders.Tzdb.GetZoneOrNull >> Option.ofObj) with
             | Some zone -> Ok zone
             | None -> Error(ConfigTimeZoneNotRecognised(timeZoneSetting, zoneId))))

/// timeZoneLocal is the configured zone. The interfaces confirm it at start-up, so the raise below is never reached by a
/// command; it is there so a caller that skips the check stops rather than using some other zone.
let timeZoneLocal () : DateTimeZone =
    match configuredTimeZone.Value with
    | Ok zone -> zone
    | Error e -> raise (System.InvalidOperationException(e.ToMessage()))


/// Clock.Now exists because the app layer creates time at a 1 * 10 ^ -7
/// precision but the persistence layer can only store at 1 * 10 ^ -6 precision.
/// We truncate here so we can more definitively test that "now" instances are
/// accurately persisted and reconstituted.
let now () : Instant =
    let raw = SystemClock.Instance.GetCurrentInstant()
    let ticks = raw.ToUnixTimeTicks()
    Instant.FromUnixTimeTicks(ticks - (ticks % 10L))

let instantToString
    (format: string)
    (instant: Instant)
    : string =
    instant.InZone(timeZoneLocal()).ToString(format, CultureInfo.InvariantCulture)
