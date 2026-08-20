# AccountCrud Efficacy Auditor

## AQ-AC-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs, lines 81-88 (REQ-AC-3.4)
- **Summary:** REQ-AC-3.4 FetchByCode happy path test contains zero value assertions (Specimen 7).
- **Resolution:** fix-test

The test cites REQ-AC-3.4 ("The system must be able to retrieve an Account record by the caller providing that record's account code string") but never inspects the returned data:

```fsharp
member _.``REQ-AC-3.4 Account FetchByCode happy path``() =
    let payload =
        { code = "F-1270" }
        |> toJson<AccountFetchByCodeInput>
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    match routeUiCommandForTesting "Account" "FetchByCode" [] payload with
    | Ok _ -> ()
    | Error e -> Assert.Fail(AppError.toMessage e)
```

The test proves the call returned Ok. It never deserializes the return payload, never checks the returned account's code, name, type, or any other property. Smell test: if FetchByCode returned a completely different account, or empty well-formed JSON, this test would still pass.

This is the only test at any layer that cites REQ-AC-3.4 for the happy path. There is no model-layer test for fetchByCode -- the route implementation (Src/InterfaceBridge/Routes/AccountRoutes.fs:84-92) composes code-to-ID lookup via LookupCache with fetchById. The fetchById function IS tested at the model layer (REQ-AC-3.3), but the code-to-ID-to-fetch composition that IS FetchByCode is only exercised here, and its result is discarded.

The companion Theory test (line 556, same file) properly tests the sad paths with typed error matching, but the happy path -- the one that proves the actual retrieval works -- is Specimen 7.

Incidental note: the REQ-NGUI-3.6 CLI test (Tests.Integrated/SonOfLeoCli/Program.fs:44-60) happens to use FetchByCode with code "F-1270" and asserts the returned name is "Money Market." This provides incidental coverage but is owned by a different requirement, uses a hardcoded expected name, and could be rewritten to use any route without affecting its own REQ's coverage.

**Action:** Add value assertions to the REQ-AC-3.4 happy path test: deserialize the returned AccountReturn and assert accountReturn.code equals the input code ("F-1270"). Compare with how REQ-AC-3.3 (Account.fs:298-306) asserts the returned ID matches the provided ID.

**Why:** REQ-AC-3.4 is the only code-based retrieval path in the Account domain. Its happy-path test currently proves plumbing (the route accepts the input and does not error) but not behavior (the correct account was returned). A bug in the LookupCache or code-to-ID translation would go undetected.

---
