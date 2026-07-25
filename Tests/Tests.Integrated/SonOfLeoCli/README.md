# Tests.Integrated.SonOfLeoCli

Tests that actually invoke the CLI as an independent OS process

## What belongs here

- Tests that the CLI is able to route command line arguments and payloads correctly.
- Tests that ensure output is properly formed and formatted (not every interface contract). E.g.: do errors go to stderr and do they include the stack trace when needed.

## What doesn't belong here

- Tests of every route
- Tests of every interface contract
- Any test that could conceivably be carried out in a lower layer. Seriously, these tests are expensive. 