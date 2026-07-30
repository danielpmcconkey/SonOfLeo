# Tests.Integrated.InterfaceBridge

These tests are primarily here so that we don't have to invoke the CLI to test how the UI marshalls requests and unmarshalls responses.

## What belongs here

- Happy paths of each route
- Tests that involve ensuring that an invalid type doesn't get passed into Model or ModelOrchestration (example, a string that is too long to fit into a CommentText). This is the only place, other than the CLI tests that we even *could* test such things as it's impossible to create an invalid type.
- Tests that confirm the app handles the boundary conversions of UUIDs into and from AccountCode and FiscalPeriodKey types.

## What doesn't belong here

- Tests that guard against failure vectors in lower layers.
- Tests that don't rely on the database (those belong in Tests.Isolated)