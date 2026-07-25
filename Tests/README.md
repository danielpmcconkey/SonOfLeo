# Tests

## Hierarchy of testing layers

The various tests are stratified into layers that roughly follow the trajectory of core Model type tests -> user interface tests. The idea is that you test every happy path at each layer, but test every failure point only once, and at the lowest possible layer. Example, if you've already tested that the ModelOrchestrator.AccountBalance.fetchByAccountIdList excludes voided journal entries in its calculations, you should not also test that same concept in the UI functions that call that fetch.

Listed highest to lowest:
1. Tests.Integrated.SonOfLeoCli
2. Tests.Integrated.InterfaceBridge
3. Tests.Integrated.ModelOrchestrator
4. Tests.Integrated.Model
5. Tests.Isolated.Model

## Test data strategy

The test database is populated at test execution start with pre-conceived test data fixtures. The test database is also truncated at the end of every test execution. This is done whether you are running an individual test or the entire test suite.

### Test data advisories
- Do not ever architect a test that relies on persisted data in the test database.
- Test data fixtures are created with dates relative to the system date/time, so do not count on any statically dated test data.
- Do not ever write tests that assume the count of anything in the database is a constant (ex: number of accounts of type "Asset"). Always calculate expected counts (using a different means than what you're testing) before the assertion phase of the test.
- It is okay to create new fixture data, but your first thought should be to see if an existing fixture can suffice.
- When you do create a new fixture, you should think about how that fixture can be reused by other tests.
- Do not leave the database in a mutated state. Always roll back transactions or manually clean up any changes you made to the database.
- All tests--other than Tests.Isolated tests--run in series. This is needed so you can test certain operations that write data without the backstop of a database transaction.

### Test data creation by test type

- **Pure read operation tests**: rely fully on test data fixtures and create new fixtures if needed to support your test.
- **Creation tests in the CLI**: 
  - Generate new entities inside of your test, but only for the type of creation you're testing. Ex: To test an Account create with a specific parent account, you don't have to create the parent. You can use an existing fixture as the parent and only create the child entity.
  - Use manual clean-up methods to delete anything you added to the DB
  - Wrap it all in a try / with to ensure clean-up no matter what 
- **Creation tests in the InterfaceBridge, ModelOrchestrator, or Model**:
  - Generate new entities inside of your test, but only for the type of creation you're testing.
  - Use transactions to clean-up created data unless the function you're testing manages its own transactions
  - Use manual clean-up methods to delete anything where you couldn't use a transaction.
  - Wrap it all in a try / with to ensure clean-up no matter what
- **Update tests**:
  - Use the same strategy that you would for creation tests regarding data clean-up
  - It is okay to update an existing fixture if you can roll-back with a transaction (eg closing a fiscal period).
  - It is not okay to update an existing fixture if the update function manages its own transaction (eg voiding a journal entry).

## Bullshit test practices (unacceptable and deserving of ridicule)
- Do not test the same thing twice. Ex: "Test that invalid input fails" by using an empty string in the "source" field and then "Test that empty source string fails" further down the file.
- Do not test all possible failure vectors at all levels. All vectors should be tested, but only once, at their lowest possible level.
- Do not assert failure without asserting an exact error. The wrong error code may be "only unhelpful" but it could also be masking a deeper problem.
- Do not assert imprecise counts (number on Asset accounts > 2). I want to know that you know you should have 6 and expect exactly 6.
- Do not write tests unless you have a behavioral REQ to cite. If the code you are testing does something uncited by the REQs, stop and point that out. Likely an REQ needs to be added.

## Admirable test practices (encouraged and deserving of praise)
- Tests should strive to be atomic, testing only one behavior per test. There are times when it is reasonable to go against this guidance, but that decision should be weighed carefully. 
- Use XUnit's Theory construct to test multiple similar behaviors where you can.
- Test all public functions in a module (at least the happy path)
- Test all permutations of a function's parameters unless that parameter is a pure passthrough to a function tested at a lower-level. Ex, if a fetchByFiltered function allows you to filter on 4 different values, test all 4 independently.
- Asserting equality is preferred to asserting truth. You should know what you expect and you should assert the outcome is exactly what you expect.
