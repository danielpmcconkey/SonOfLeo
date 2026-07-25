# Tests.Integrated.Model

These are a 1:1 map to Src/Model functions

## What belongs here

- Happy paths of each public Model function
- Guards against failures of type validation that are wholly contained within a given Model domain. Ex: enforcing valid Account type / subtype combinations

## What doesn't belong here

- Tests that enforce conversions between primitives and Model component types. Those are handled in Tests.Isolated.
- Tests that don't rely on the database (those belong in Tests.Isolated)