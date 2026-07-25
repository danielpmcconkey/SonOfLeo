# Tests.Integrated.ModelOrchestrator

These are a 1:1 map to Src/ModelOrchestrator functions

## What belongs here

- Happy paths of each public ModelOrchestrator function
- Guards against failures of type validation that are intrinsic to composite objects (eg: a journal entry header must have 2 or more journal entry lines.)
- Guards that enforce the existence of related entities (e.g.: the parentId provided at account creation is a real account record)
- Guards that enforce the state of related entities (e.g.: the fiscal period is still open before voiding a journal entry)

## What doesn't belong here

- Guards against failure vectors that can be surfaced in the Model
- Tests that don't rely on the database (those belong in Tests.Isolated)