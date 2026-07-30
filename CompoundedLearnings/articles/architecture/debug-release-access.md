# Debug/Release Access Rules

**Source:** the retired Conventions/BuildAndEnvironment.md (removed 2026-07-30)

Build configuration determines which database an executable may access. This is a hard boundary, not a suggestion.

## Rules

- Any executable in **debug** mode may NEVER access the production database — read or write
- Only executables in **release** mode may access production — read or write

## Agent restrictions

No agent is ever allowed to:

- Manipulate application config files (appsettings, launchSettings, etc.)
- Manipulate environment variables
- Modify DAL functions that are part of the connection string chain

These boundaries exist so that an agent cannot accidentally (or through hallucination) bridge the gap between debug and production. Dan configures these by hand.
