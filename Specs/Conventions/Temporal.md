# Temporal Values

## Application layer tooling

The system will rely on the NodaTime library as much as practical.

The application layer must use NodaTime's Instant type for operating with instants. No instantiation of standard dotnet DateTime or DateTimeOffset objects is allowed, excepting I/O libraries that require it.

Any modules that require such reliance must keep standard dotnet DateTime and DateTime offsets as close to the edge as practical 

## Persistence layer conventions
The database will persist all instances as timestamptz. No exceptions.

To ensure as much NodaTime compatibility, the application layer will use the Npgsql.NodaTime plugin so `timestamptz` maps to `Instant` end-to-end (avoiding the I/O edge described above).

The database will persist dates using the Postgres date type only.

The persistence layer may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.).

Required (non-nullable) temporal columns carry no defaults; a write that omits the value is rejected, never filled in by the database.

## Instants
The system must be able to reconstitute any instant type to seconds precision at minimum, meaning an accurate calculation of time span in seconds between any two instants must be correct. 

To satisfy some specific use cases, instants may, at times, be presented by the system to an interface layer with lesser precision than seconds, but a subsequent audit or query of the underlying value, must present the precise instant, to the second.

Certain entities will have instants defined whose underlying data is imported or otherwise derived from external systems that do not meet this standard (or don't expose their instant values in a way that meets this standard). In such cases, the system will reject such instances as invalid. 

The exception to this rejection rule is that, if the interface to said data is a component of this system, such middleware must convert the inbound data into this system's standard given assumptions defined in the requirements for those middleware components.

Temporal arithmetic with instances may never involve years or months as those periods will always require some reference to a calendar convention. 

Temporal arithmetic with instances involving days is discouraged. Feature designers should choose hours, minutes, or seconds where practical.

## Dates

Calendar arithmetic with dates may only ever involve years, months, or days.

## Calendar periods

Calendar periods will be defined as discriminated unions whose contents may vary depending on the domain to which they belong. 

Calendar periods are typically used to determine the expected date of a future event and will always use standard NodaTime library functions to determine those dates.