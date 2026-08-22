# Batch B — test names for approval (steps 5–6 complete)

Drafted from `Specs/Behavioral/ClassificationRuleCrud.md` and `DataIngestion.md` §5 only.
`Src/ModelOrchestrator/ClassificationOrchestration.fs` has not been opened.
Graded once by an independent agent; every finding below 88 was worked.

**32 names covering 19 requirements.** Function names are placeholders where I don't know the
real one — the only orchestrator name I have is `fetchRulesFiltered`, which Dan gave me. At step 8
I may correct a subject to the actual function name. That changes how a test reaches the
behavior, never what it claims.

---

## Isolated — `Tests.Isolated/Model/DataIngestion` (Form 1)

`FieldMatchChainEvaluation.fs`

1. `REQ-CR-2.8 an empty field match chain evaluates to false`

`ClassificationRuleMatching.fs`

2. `REQ-CR-2.9 a rule with an empty rule groups list evaluates to false`

---

## Integrated — `Tests.Integrated/ModelOrchestrator/ClassificationRuleCrud.fs` (new file)

### Create — §4

3. `REQ-CR-4.1 REQ-CR-4.5 create returns the new rule bearing an id, a created_at and modified_at that are populated and equal, and the name, code, priority, and rule groups it was given`
4. `REQ-CR-4.5 a created rule fetched back from the database carries the same rule groups, field matches, and amount patterns it was created with`
5. `REQ-CR-4.4 a newly created rule is active, both in the value create returns and in the row fetched back`
6. `REQ-CR-4.3 REQ-CR-1.5 create returns an account-not-found error when codeAtMatch doesn't match an account code in the ledger`
7. `REQ-CR-4.6 REQ-CR-1.7 create returns a validation error when the rule groups list is empty`
8. `REQ-CR-4.7 REQ-CR-1.12 create returns a validation error when a field match chain is empty` — Theory: empty chainOne in the only group, empty chainOne in the second of two groups, empty chainTwo

### Read — §5

9. `REQ-CR-5.1 fetch by id returns the one rule bearing that id with its rule groups, field matches, and amount patterns intact`
10. `REQ-CR-5.2 fetch by name returns the rule whose name matches exactly and not one whose name merely contains it`
11. `REQ-CR-5.3 fetchRulesFiltered by id returns exactly the one rule bearing that id`
12. `REQ-CR-5.3 fetchRulesFiltered by name fragment returns every rule whose name contains the fragment and no others`
13. `REQ-CR-5.3 fetchRulesFiltered by codeAtMatch returns every rule assigned that exact code and none assigned a code merely containing it`
14. `REQ-CR-5.3 fetchRulesFiltered by source pattern fragment returns the rules whose rule group bodies carry that pattern and no others`
15. `REQ-CR-5.3 fetchRulesFiltered with activeOnly true omits the inactive rule that its other filters would otherwise have returned`
16. `REQ-CR-5.3 fetchRulesFiltered with activeOnly false returns that inactive rule alongside the active ones its other filters match`
17. `REQ-CR-5.3 fetchRulesFiltered given both a name fragment and a code returns only the rules satisfying both, not the union`
18. `REQ-CR-5.3 fetchRulesFiltered with every filter omitted returns every rule in the table`
19. `REQ-CR-5.4 fetchRulesFiltered sorted ascending returns rules in increasing order of the named key, and sorted descending returns the exact reverse` — Theory: account code, priority
20. `REQ-CR-5.4 fetchRulesFiltered sorted by priority ascending places no rule before one of lower priority, and places rules tied at the same priority adjacent to each other`

### Update — §6

21. `REQ-CR-6.1 updating name with SetTo changes the name and leaves code, priority, rule groups, and isActive as they were`
22. `REQ-CR-6.1 updating codeAtMatch with SetTo changes the code and leaves name, priority, rule groups, and isActive as they were`
23. `REQ-CR-6.1 updating priority with SetTo changes the priority and leaves name, code, rule groups, and isActive as they were`
24. `REQ-CR-6.1 updating ruleGroups with SetTo leaves exactly the new rule groups with none of the old surviving, and leaves name, code, priority, and isActive as they were`
25. `REQ-CR-6.1 updating isActive with SetTo false deactivates the rule and leaves name, code, priority, and rule groups as they were`
26. `REQ-CR-6.1 updating isActive with SetTo true reactivates the inactive rule and leaves name, code, priority, and rule groups as they were`
27. `REQ-CR-6.2 an update with all five fields NoChange is rejected and leaves the stored rule, modified_at included, untouched`
28. `REQ-CR-6.3 REQ-CR-1.5 update returns an account-not-found error when the new codeAtMatch doesn't match an account code in the ledger`
29. `REQ-CR-6.4 update returns a validation error when the new rule groups list is empty`
30. `REQ-CR-6.4 update returns a validation error when a chain within the new rule groups is empty` — Theory: the only group's chainOne, the second group's chainOne, chainTwo
31. `REQ-CR-6.5 a successful update leaves modified_at later than the value it held before the update`
    - **Do not fight the clock.** `modified_at` comes from the context's initiation instant, not from
      the wall clock, so two operations in one context stamp identically and the test cannot pass by
      waiting. Advance it with `Src/Context/Context.fs` `updateInitiationInstant` between the create
      and the update. Dan flagged this at step 7; rediscovering it costs a lot of tokens.

### Pipeline — `Tests.Integrated/ModelOrchestrator/StageEntryClassification.fs`

32. `REQ-STG-5.2 an entry with two null-code lines matching different rules has each line assigned its own rule's code`
    - Needs a new **staged** archetype: every fixture entry today is one null Debit plus a
      parser-assigned Credit (`F-1270`), so no entry has ever had two null lines.

---

## What the grader changed

Worked every finding below 88. The pattern behind most of them: **"fails" is not an outcome.**
Six create/update validation names said only that the operation fails, which any exception
satisfies — including one that half-wrote a row. They now all take one shape, `<op> persists
nothing / leaves the stored rule unchanged and returns <error> when <trigger>`, and because they
are parallel, #30's missing Theory cases became visible. #8 had three chain shapes; its update-side
twin had none, against a requirement worded identically. It has them now.

The other substantive catches:

- **#27** — "fails as a no-op" didn't forbid a `modified_at` bump, which is precisely how a no-op
  rejection breaks. Now names it.
- **#20** — my original said the tied rules sit "below 100 and above 1000", which reads
  positionally backwards under an ascending sort. Either implementation satisfied it.
- **#24** — "replaces the whole rule body" permitted a merge or an append. Now says none of the old
  groups survive.
- **#13** — `codeAtMatch` is the one *exact* filter among the five. Nothing in my name denied a
  substring match.
- **#5** — "created without any isActive argument" implied there is a form where you pass one.
  There isn't, which is now `REQ-CR-4.8`. The condition was spurious.
- **#26** is new. Only deactivation was named; reactivation is the same act in the other direction
  and the new inactive fixture makes it free.

**Three of its four "uncovered" findings are false positives, and they're my fault** — I sent it
requirement text for `REQ-CR-2.7` and `1.12` that Batch A already covers (five tests, both
directions each), and its `REQ-STG-5.2` gap is `REQ-STG-5.3`, tested since August. Sending a
requirement I wasn't claiming to cover buys a confident report of a hole that isn't there. Next
batch gets only the requirements in scope.

The one real gap it found: `REQ-CR-4.5` demands the returned rule carry **timestamps**, and my #3
named the id and the given fields only — the first two things looked like the whole sentence.
Folded into #3 with the stronger claim that on create the two timestamps are equal.

**Where I overrode it:** #3's id clause. The grader wanted "server-generated id … not one the
caller passed." `REQ-CR-4.2` is waived on exactly that ground, so the name would be claiming more
than the unwaived half of the requirement supports.

---

## Fixture archetypes requested

Two. Shared state, so they get reviewed before they are written.

### A. An inactive rule matching source `TestSavings`

Required by 15, 16, 26, and the `activeOnly` vector Dan asked for by name. Nothing in the fixture
is inactive today, so `activeOnly` cannot currently be shown to do anything at all.

Pointing it at `TestSavings` is deliberate: that source exists precisely because it matches no
rule, and `REQ-STG-5.7 no rule match sets entry status to NoMatch` depends on that. If the
classifier's active filter regresses, this archetype makes that existing test go red on its own.
The fixture becomes a detector rather than a prop.

**Risk I'll check before writing:** any existing test that counts rules or counts matches against a
`TestBank`/`TestSavings` candidate could shift under it.

### B. A two-group rule with a `chainTwo` and an `Amount` field match

Required by 4, and it pays for itself twice:

- All five current fixtures are one group, one chain, `chainTwo = None`, and every field match is
  `Source` or `Description`. **No fixture rule has an `Amount` match at all.** No test anywhere has
  ever put a `Money` value out to JSONB and back.
- That is the gap under the `REQ-CR-1.21` waiver. The waiver holds that an invalid amount cannot
  reach the pattern because the field is typed `Money` — but nobody has checked whether
  `reconstitute` rebuilds it through `Money.create` or straight off the JSON. This archetype puts a
  real amount through the round trip and #4 asserts it survives.

### C. A staged entry with two null-code lines matching two different rules

Required by #32, added at step 7. Every staged group in the fixture is one null Debit plus a
Credit the parser assigned to `F-1270`, so `REQ-STG-5.2`'s "**each** staged line whose account_code
is null" has never had more than one such line to be each of. A classifier that evaluated only the
first null line per entry would pass the entire existing suite.

Shape: one group whose Debit and Credit both arrive null and match different rules — one landing on
`F-5350`, the other on `F-5650`. Two different codes is the point; two lines both landing on the
same code would leave a whole-entry assignment indistinguishable from per-line assignment.

**Risk I'll check before writing:** `REQ-STG-5.8` sets an entry to `Classified` only when every line
has a code, so this entry becomes a second `Classified` one. Any existing test counting entries by
status will move.

### Not requested: a rule pointing at a closed account

`REQ-CR-4.3` and `6.3` require the code to resolve to an **existing** account. Neither says anything
about its state. A closed-account fixture would be a test with no requirement to cite. If the
intent is that a closed account should be rejected, that's a spec gap for Hobson, not a fixture
request.

---

## Rulings taken

1. **#32 dropped, no REQ written.** It would have asserted that a rejected update leaves the
   stored rule untouched. `REQ-JE-2.12` is the precedent, but a journal entry post spans a header,
   N lines, and references — a classification rule write is one row, where the database enforces
   non-persistence for free. Dan declined the precedent and I agree. The "persists no rule" clause
   the grader talked me into has come back out of #6, #7 and #8 for the same reason. **If step 8
   shows the SET clause going out before `codeAtMatch` is confirmed, I raise it then, with the code
   in hand.**
2. **`REQ-CR-4.4` split** (Hobson, `967cc67`). `4.4` is now the testable half and #5 covers it.
   The negative existence claim is `REQ-CR-4.8`, waived on the `7.1` rationale and visible in the
   waiver table instead of buried inside a requirement that traceability reads as covered.
   **`4.8` must never appear in a test name.** It is waived, and an ID under `Tests/` is a citation
   whether or not it annotates a test — naming it there would report the waived clause as covered
   and go stale. #5 cites `4.4` alone.

## Step 7 dispositions (Dan, 2026-08-21)

- **#1, #2** — "rather than vacuously true" cut. It is the reason the requirement exists, not part
  of the claim, and the spec's *Why* block already carries it.
- **#6, #28** — "names no account" read as the account *name*. `codeAtMatch` is a code. Reworded to
  Dan's phrasing.
- **#31** — clock warning recorded inline.
- **#32 rewritten.** Dan was right and the old name was wrong twice over. It said "classified" where
  the requirement says *evaluated*, and those differ: a line can be evaluated and match nothing.
  Entry-level "classified" is `REQ-STG-5.8`'s claim and depends on *every* line having a code, so
  the old name asserted something `5.2` does not promise. The clause `5.2` actually adds over `5.4`
  is the word **each** — every null line gets evaluated, not just the first — and the way to observe
  that is two null lines matching two different rules ending up with two different codes.
- **#33 dropped.** It was guarding cross-entry contamination — a line picking up the rule that
  matched a *neighbouring* entry's description. The existing pipeline tests already close that as a
  side effect: they run the whole batch and then select entries by description, so DoorDash landing
  on `F-5350` while Allstate goes to `Conflict` and TestSavings to `NoMatch` cannot all hold if the
  loop is reading the wrong entry's description. A dedicated test adds nothing.

## Still open

**Layer for the read tests (11–20).** If `fetchRulesFiltered` turns out to be a DAL function rather
than an orchestrator one, those move to `Tests.Integrated/DataAccessLayer`. I'll know at step 8;
flagging so the move isn't a surprise.

---

# Step 8 — first read of the Src

Read `ClassificationOrchestration.fs`, `ClassificationRule.fs`, `QueryParameters.fs`. Three findings,
then the housekeeping.

## 1. `REQ-CR-4.8` is violated, and name #5 is aimed at a behavior that doesn't exist

`createNewClassificationRule` takes `(isActive: bool)` as its last parameter and hands it straight to
`ClassificationRule.create`. So `createNewClassificationRule ctx name code prio groups false`
persists an inactive rule.

`REQ-CR-4.4` says new rules are always active. `REQ-CR-4.8` — split out and waived yesterday — says
the system must provide no mechanism to create one inactive. The mechanism is the fifth argument.

This guts name #5, `a newly created rule is active, both in the value create returns and in the row
fetched back`. To exercise create at all I must pass `isActive`, so the test asserts that the `true`
I passed came back as `true`. That is a hollow test under a good name — the exact specimen the
TestWriter references warn about. I will not write it as it stands.

**Dan's call, two routes:**

- **Src:** drop the parameter and hardcode `true` inside `createNewClassificationRule`. `4.4` and
  `4.8` both become true, #5 becomes a real test, and `updateClassificationRule` remains the only
  way to deactivate — which is what §6 and §7 already describe. The fixture helper
  `createClassificationRuleForTest` passes `true` at every call site today, so the blast radius is
  small. **This is my recommendation.**
- **Spec:** `4.4`/`4.8` are wrong and creating inactive rules is intended. Then #5 is deleted, not
  rewritten, and `4.4` needs rewording rather than a test.

I have not touched `Src/`.

## 2. The `codeAtMatch` filter looks broken, and #13 is the test that will find out

`fetchRulesFiltered` binds its account-code parameter as `@cr.code_at_match` — **the only parameter
name containing a dot anywhere in `Src/`.** Npgsql's placeholder parser stops an identifier at the
dot, so the query text reads a placeholder called `@cr` while the supplied parameter is named
`@cr.code_at_match`. Nothing matches.

That vector has never had a test, which is why it has survived. Expect #13 to go red on first run.
**When it does, it is a Src bug, not a test bug** — recording that here so step 10 doesn't relitigate it.

Exact diff, `Src/ModelOrchestrator/ClassificationOrchestration.fs` lines 131–132:

```
-                  ("and cr.code_at_match = @cr.code_at_match",
-                   { name = "@cr.code_at_match"; value = CharString(x |> AccountCode.value) }))
+                  ("and cr.code_at_match = @code_at_match",
+                   { name = "@code_at_match"; value = CharString(x |> AccountCode.value) }))
```

## 3. The `REQ-CR-1.21` waiver is right for the wrong reason

The waiver reads: *"The field is typed Money, which is validated at construction — an invalid value
cannot reach the pattern."*

`ClassificationRule.reconstitute` builds the rule body with
`ruleGroupsStr |> fromJson<ClassificationRuleGroup list>`. That materialises the whole tree —
`Money` included — by deserialisation. `Money` is `private { amount: decimal }`, and a private
constructor is no obstacle to a reflection-based deserialiser. **`Money.create` is never called on
the read path.** Being typed `Money` guarantees nothing about a value that arrives from JSONB.

The conclusion still holds, but on a different footing: the only writer of that column is
`Json.toJson` over a value that already came through `Money.create`, so nothing invalid gets in.
That is a guarantee about the *write path*, not about the type — and it is weaker, because it breaks
the moment a migration, a manual fix, or a future importer writes that column.

Recommend rewording the waiver to say what actually protects it. Not a test: a test that inserted
a sub-cent amount into the JSONB and asserted a failure would fail, because reconstitution accepts
it. That is a defect report, not a requirement.

## Housekeeping — approved names confirmed against the code

- **#32 stays dropped, confirmed.** `updateClassificationRule` runs `confirmAccountCode` and
  `confirmRuleGroups` before it builds the SET clause, and create validates before
  `insertNewToDb`. Nothing writes ahead of validation. Dan's step-7 call was right.
- **#3 is exactly right.** Create takes one `instant` from the context and passes it as both
  `createdAt` and `modifiedAt`, so "populated and equal" is the true claim.
- **#27 is provable.** The no-op check sits before `executeNonQuery`, so `modified_at` genuinely
  does not move.
- **#16 is right.** `activeOnly = false` emits no clause at all, so active and inactive both return.
- **Layer question closed.** The DAL is generic plumbing — no per-entity fetches — so
  `fetchRulesFiltered` is orchestrator-level and names 11–20 stay in
  `Tests.Integrated/ModelOrchestrator`.
- **Ordering note for #6:** `confirmRuleGroups` runs before `confirmAccountCode`, so the
  bad-code test must supply valid rule groups or it gets the wrong error.
