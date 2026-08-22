# Testing

Test writing doctrine, fixture rules, and coverage accounting. The canonical test
standard is `Tests/README.md`; the TestWriter skill (`Skills/TestWriter/`) is the procedure
for applying it. Articles here capture judgment learned beyond both.

| Concept | Article | Read when... |
|---|---|---|
| Bullshit-test specimens | `../Skills/TestWriter/references/bullshit-test-specimens.md` | Before writing any test assertions — real purged tests showing what passes CI while verifying nothing, and the rewrites |
| A failure vector is a user interaction | `articles/testing/failure-vector-is-a-user-interaction.md` | Counting failure vectors to decide a test's layer — and before deleting or waiving a route-level validation case as redundant with a constructor test |
| Mutation proves liveness, not aim | `articles/testing/mutation-proves-liveness-not-aim.md` | Finishing a mutation pass and about to report a test proven — and whenever a test name states a specific scenario ("exactly one", "already closed", "empty") that the fixture has to actually produce |
