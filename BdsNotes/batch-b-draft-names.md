# Batch B — test names for approval (steps 5–6 complete)

Drafted from `Specs/Behavioral/ClassificationRuleCrud.md` and `DataIngestion.md` §5 only.
`Src/ModelOrchestrator/ClassificationOrchestration.fs` has not been opened.
Graded once by an independent agent; every finding below 88 was worked.

**33 names covering 19 requirements.** Function names are placeholders where I don't know the
real one — the only orchestrator name I have is `fetchRulesFiltered`, which Dan gave me. At step 8
I may correct a subject to the actual function name. That changes how a test reaches the
behavior, never what it claims.

---

## Isolated — `Tests.Isolated/Model/DataIngestion` (Form 1)

`FieldMatchChainEvaluation.fs`

1. `REQ-CR-2.8 an empty field match chain evaluates to false rather than vacuously true`

`ClassificationRuleMatching.fs`

2. `REQ-CR-2.9 a rule with an empty rule groups list evaluates to false rather than vacuously true`

---

## Integrated — `Tests.Integrated/ModelOrchestrator/ClassificationRuleCrud.fs` (new file)

### Create — §4

3. `REQ-CR-4.1 REQ-CR-4.5 create returns the new rule bearing an id, a created_at and modified_at that are populated and equal, and the name, code, priority, and rule groups it was given`
4. `REQ-CR-4.5 a created rule fetched back from the database carries the same rule groups, field matches, and amount patterns it was created with`
5. `REQ-CR-4.4 a newly created rule is active, both in the value create returns and in the row fetched back`
6. `REQ-CR-4.3 REQ-CR-1.5 create returns an account-not-found error when codeAtMatch names no account in the chart of accounts`
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
28. `REQ-CR-6.3 REQ-CR-1.5 update returns an account-not-found error when the new codeAtMatch names no account in the chart of accounts`
29. `REQ-CR-6.4 update returns a validation error when the new rule groups list is empty`
30. `REQ-CR-6.4 update returns a validation error when a chain within the new rule groups is empty` — Theory: the only group's chainOne, the second group's chainOne, chainTwo
31. `REQ-CR-6.5 a successful update leaves modified_at later than the value it held before the update`

### Pipeline — `Tests.Integrated/ModelOrchestrator/StageEntryClassification.fs`

32. `REQ-STG-5.2 an entry with two null-code lines has both lines classified, not only the first`
33. `REQ-STG-5.2 a line is classified by the rule matching its own entry's description, not by one matching a sibling entry's`

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

## Still open

**Layer for the read tests (11–20).** If `fetchRulesFiltered` turns out to be a DAL function rather
than an orchestrator one, those move to `Tests.Integrated/DataAccessLayer`. I'll know at step 8;
flagging so the move isn't a surprise.
