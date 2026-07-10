# Panel: F#/DDD Idiom Review (Fable 5)

12 findings (3 high, 5 medium, 4 low)

## FSDDD-01 — HIGH (types)
**Location:** Src/Model/Ledger/JournalEntryComment.fs:187-194
**Summary:** updateComment passes the COMMENT's uniqueId where the primary journal entry ID belongs, so REQ-JE-1.53 is unenforced on the update path — a live bug caused by bare-Guid primitive obsession.
**Detail:** validatePrimaryAndSecondaryRelationship's first parameter is 'primaryJournalEntryId: Guid'. In updateComment, the SetTo branch does 'x |> validatePrimaryAndSecondaryRelationship uniqueId' — but 'uniqueId' there is the comment row's primary key. The check therefore compares the new secondary JE against the comment's own ID, which can never match. No test catches this.
**Suggested Action:** Immediate fix: fetch the comment first and validate against its primaryJournalEntryId. Durable fix: introduce distinct wrapped ID types (CommentId vs JournalEntryId) so this call no longer compiles.
**Why:** Every string is wrapped (AccountCode, CommentText) but every Guid is bare. The one place two different kinds of Guid met in one function, they got crossed. 'Make illegal states unrepresentable' includes 'make wrong arguments untypeable.'
**Owner:** fix-code
[Dan]I fixed this in Src/Model/Ledger/JournalEntryComment.fs[/Dan]

## FSDDD-02 — HIGH (module-boundaries)
**Location:** Src/Model/Ledger/JournalEntryComponent.fs:45-55, JournalEntryLine.fs:37-57, JournalEntryExternalReference.fs:58-73, JournalEntryComment.fs:57-75
**Summary:** The journaling slice's validateThenConstruct functions perform database I/O, contradicting Doctrines section 1's layering and imposing hidden read amplification.
**Detail:** JournalEntryLine.validateThenConstruct calls validateAccount (Account.fetchById round trip). JournalEntryHeader.validateThenConstruct calls EntryDate.create (two queries). Comment and ExternalReference constructors fetch headers. Consequences: (1) every reconstitution re-proves world-state facts the DB's FKs guarantee; (2) create-path duplication (account fetched twice); (3) constructors are impure so Journaling has almost no isolated unit tests. Contrast Account slice where validateThenConstruct is pure.
**Suggested Action:** Move existence/open-period checks out of validateThenConstruct into operation functions. On read paths, trust FK integrity.
**Why:** Keep a pure core and an impure shell. A constructor that takes DbTransaction option has become an I/O operation. The doctrine already states the right rule; Account follows it and Journaling does not.
**Owner:** dan-decides
[Dan]This needs a deeper architecture discussion. Add an action item to discuss whether / how we standardize entity-level module functions. Also create a separate action item for discussing domain-type validation on read.[/Dan]

## FSDDD-03 — MEDIUM (composition)
**Location:** Src/Utilities/DAL.fs:161, Src/Model/Ledger/FiscalPeriod.fs:147-150
**Summary:** Errors are prose, not data: fetchIdByKey dispatches on the exact string 'Resultant rows didn't match expectation' to detect not-found.
**Detail:** If anyone rewords validateNumRows' message, the friendly not-found message silently degrades. Result<'T, string> is fine at the CLI boundary but mid-railway it erases the one distinction the code needs.
**Suggested Action:** Give the DAL a small error DU (NoRows | UnexpectedRowCount | DbFailure of string) and map to strings at the CLI edge.
**Why:** Railway-oriented programming's payoff is that errors become values the compiler can see. A string error is a value the compiler cannot distinguish.
**Owner:** dan-decides
[Dan]create an action item for me to design a more robust error system (a DU or an error code dict or a full custom Error type)[/Dan]

## FSDDD-04 — MEDIUM (composition)
**Location:** Src/Model/LookupCache.fs:11-12, Src/ModelOrchestrator/JournalEntryCreation.fs:135,150-156, JournalEntryVoiding.fs:64,73-79
**Summary:** Result.defaultWith failwith punches exception holes in Result-typed functions, and the hand-rolled commit/rollback bracket is duplicated and not exception-safe.
**Detail:** (1) LookupCache's loadAll failwith runs at module init — dead DB = TypeInitializationException. (2) Orchestrators use createDbTransaction() |> Result.defaultWith failwith inside Result-returning functions. (3) The rollback/commit block is copy-pasted and if anything throws, neither rollback nor Dispose runs.
**Suggested Action:** Add a withTransaction bracket combinator to DAL. Make LookupCache lazy-per-fetch returning Result.
**Why:** A function whose signature says Result makes a promise. failwith inside it is a signature that lies.
**Owner:** fix-code
[Dan]This is multiple findings in one:
1. LookupCache runs at init. I don't think 1 matters at all (for now). But I've also never liked the idea. It solves the problem of needing to constantly translate between UUIDs and human-readable identifiers. But some of our architecture discussions may render this moot. Set up an action item to discuss this *after* our greater architecture discussion

2. exceptions inside the railroad won't get rolled back. I don't see how that wouldn't get rolled back. The railroad *will* return either an Error or an Ok, no? Anything that can throw an exception will have that turned into an Error, if I understand correctly. Though I'm also intrigued by the withTransaction construct the reviewer mentions. Set up an action item to review how transactions work with respect to orchestrated write ops.

3. the defaultWith failwith on the transaction create is an anti-pattern. Sure. But I don't want to pussyfoot around such an error. It points to a huge issue in code if that can't succeed. I want it to fail loudly.
[/Dan]

 

## FSDDD-05 — HIGH (correctness)
**Location:** Src/ModelOrchestrator/AccountBalance.fs:50-73
**Summary:** Accounts whose only journal activity is voided vanish from balance results entirely instead of reporting zero. Also the Debit/Credit axis is derived from data instead of the domain type.
**Detail:** The WHERE je.voided_at is null after LEFT JOINs discards matched-then-filtered rows for all-voided accounts. An account with NO lines survives (null-extended row passes) — inconsistent. The line_types CTE derives Debit/Credit from whatever data exists; on empty table, nothing returns.
**Suggested Action:** Move void filter to ON clause, drop line_types CTE, let F# tryFind/defaultValue supply the axis from the DU.
**Why:** When a closed set (Debit|Credit) is encoded in a type, deriving it from data reintroduces open-world uncertainty.
**Owner:** fix-code
[Dan] I have changed the query significantly, moving the null handling into an explicit case for the sum. That's cleaner in my book. Also, I did pull the line types directly from the DU now, though it's astronomically unlikely that we'll ever need to change a DU of Debit / Credit. Check my work[/Dan] 

## FSDDD-06 — MEDIUM (idiom)
**Location:** Src/ModelOrchestrator/AccountActivity.fs:90-102
**Summary:** Eight Option.get calls smuggle partiality into a Result-returning function.
**Detail:** When line_id is Some, the code assumes all other line columns are Some and rips them open with Option.get. Sound today but if the query shape drifts, the failure is an InvalidOperationException bypassing the railway.
**Suggested Action:** Match on the tuple of options once.
**Why:** Option.get is the C# accent — it treats Option like Nullable.Value.
**Owner:** fix-code
[Dan]I don't know what the fuck this guy is saying. "smuggle partiality into a Result-returning function". Was that nonsense in your training data? when line id is Some, it means there's a journal ID. All fields after are either standard primitives or options. Those that are non-optional are "ripped open" using Option.get. Those that are optional are left as options. What's the problem with that?[/Dan]

## FSDDD-07 — MEDIUM (types)
**Location:** Src/ModelOrchestrator/JournalEntryCreation.fs:131-156 and :160-170
**Summary:** JournalEntry composite has two record-literal construction sites instead of one blessed constructor, and neither checks that components belong to the header.
**Detail:** orchestrateCreation builds the record literal directly; constructFromPreValidatedComponents builds a second. Neither verifies cross-component journalEntryId agreement.
**Suggested Action:** Make constructFromPreValidatedComponents the single constructor, add the journalEntryId-agreement check.
**Why:** Two construction sites means every future invariant must be added twice.
**Owner:** fix-code
[Dan]I only partly agree with this. First off, the claim that "Neither verifies cross-component journalEntryId agreement" is demonstrably false. orchestrateCreation creates validHeader then uses that header's ID in every subsequent component creation. Next, orchestrateCreation is very specifically not a typical constructor as other domain types have. That's because a Journal Entry can only be thought of as a composite of entities. No other domain type has that constraint, so JE needs something special. Next constructFromPreValidatedComponents exists for a similar reason. There is no pure constructor that constructs from primitives. The processes that use this function are composite functions themselves that rely on the individual components doing their own validation. Yet we still need something to validate the entire JE when seen as a whole (does it have at least 2 lines?). Where I do agree is that we probably have some name changes we need to make and, depending on how we address the concern about revalidating on read, this may change everything. Add this to both the action item around standardizing entity-level module function names as well as the one around domain validation on read.[/Dan]

## FSDDD-08 — MEDIUM (module-boundaries)
**Location:** Src/Model/LookupCache.fs:37-87
**Summary:** LookupCache hardcodes ledger.account and ledger.fiscal_period SQL outside the owning domain modules.
**Detail:** The two-layer decision gives each domain module its entity end-to-end. LookupCache breaks that: it selects from both tables with hardcoded column names. A column rename now has three touch points.
**Suggested Action:** Have each domain expose narrow lookups; let LookupCache be a domain-agnostic memoizer over loader functions.
**Why:** Module boundaries are about where knowledge lives. Partial application does the work DI containers do in C#.
**Owner:** fix-code
[Dan]overruled. LookupCache only exists because we have rules around converting those specific values. But also, how the fuck would moving the query make anything less brittle?[/Dan]

## FSDDD-09 — LOW (ddd)
**Location:** Src/Model/Ledger/Account.fs:252-267
**Summary:** confirmAccountIsValidAndActive reimplements the is-active rule that isActive already owns.
**Detail:** Two definitions of one domain rule can drift. The duplicate's guard is ordering-dependent.
**Suggested Action:** Rewrite using the existing isActive predicate; build richer error messages from the predicate's inputs.
**Why:** A domain rule should have one name and one definition.
**Owner:** fix-code
[Dan]Fair, the old way was giving more useful error messages, but it already had forked the logic. I updated both places. check my work[/Dan]

## FSDDD-10 — LOW (idiom)
**Location:** Src/Utilities/DAL.fs:337-366, Src/ModelOrchestrator/AccountDeactivation.fs:95-99
**Summary:** executeScalar returns boxed Object, forcing callers into unsafe ':?>' casts.
**Suggested Action:** Make it generic: executeScalar<'T> with a safe type test inside DAL.
**Why:** ':?>' is Option.get for types — it asserts what the type system should be proving.
**Owner:** fix-code
[Dan]fixed by moving all unboxing into the DAL[/Dan]

## FSDDD-11 — LOW (correctness)
**Location:** Src/Utilities/DAL.fs:61,106,115,126,191,332,364
**Summary:** Every DAL exception handler interpolates ex.StackTrace but never ex.Message — errors carry where but not what.
**Suggested Action:** Use ex.Message (or ex.ToString() for both).
**Why:** A conversion that drops the payload defeats its purpose.
**Owner:** fix-code
[Dan]fixed check my work[/Dan]

## FSDDD-12 — LOW (idiom)
**Location:** Src/Model/Money.fs:7-9
**Summary:** MoneyRecord/MoneyModule uses C#-style name-collision suffixes; everywhere else uses type X + companion module X.
**Suggested Action:** Rename to type Money / module Money.
**Why:** The domain concept is Money; the code should say Money.
**Owner:** fix-code
[Dan]fixed check my work[/Dan]