module Utilities.Clock

open System.Globalization
open NodaTime

let eastern = DateTimeZoneProviders.Tzdb.["America/New_York"]

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
    instant.InZone(eastern).ToString(format, CultureInfo.InvariantCulture)
