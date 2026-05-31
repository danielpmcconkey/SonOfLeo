# Open Questions

Things we haven't decided yet. Resolve before they become problems.

## Project scaffolding

- .NET version. Target latest stable F# (likely .NET 9 or 10 by current
  date, confirm at project init).
- Project structure: single fsproj? Multiple projects (Domain, DAL, CLI)
  from day one? Recommendation: single project until pain forces split.
- Test framework: xUnit + FsUnit? Expecto? Whatever LeoBloom uses, or
  switch?
- Dependency injection / composition root: how do we wire IO at the
  edges? Manual composition vs. a framework.

## BDD tooling

- Does the existing BDD framework port directly to SonOfLeo? Or do we
  re-host the scenarios in something more F#-native?
- TickSpec is the F# BDD option worth evaluating. Worth comparing
  against current setup before committing.

## Database access

- DAL approach: raw ADO.NET with hand-written queries? Dapper? An F#
  data access library (Donald, FSharp.Data.Npgsql)?
- Connection lifecycle and transaction boundaries — decide before first
  IO code.

## CLI shape

- LeoBloom went CLI-first (per Dan's standing decision). SonOfLeo
  inherits that. Argu for argument parsing? Spectre.Console? Plain
  System.CommandLine?

## Slice ordering

- Which BDD scenario is slice #1?
- What's the smallest end-to-end behavior that exercises domain + DAL
  + CLI?

## Repo hygiene

- Branching strategy. Worth defining before first commit so we don't
  inherit chaos.
- Commit cadence. Dan likes to vet every change; small atomic commits
  fit that better than big batches.
