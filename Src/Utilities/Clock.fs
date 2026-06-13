namespace Utilities.Clock

open System
open NodaTime

(*
 * note: this module is somewhat vestigial but I left it in here because writing
 * Clock.now is why faster than Instant.FromDateTimeOffset(DateTimeOffset.UtcNow)
 * all the time and I may decide that I want to build on having a centralized clock
 * some day
 *)

module Clock =
    let now () : Instant =
        Instant.FromDateTimeOffset(DateTimeOffset.UtcNow)
