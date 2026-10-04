# resolved-findings-staleness-auditor

## STALE-DAL-EFFICACY — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, DAL-EFFICACY; Specs/Behavioral/DataAccessLayer.md (Waived table, REQ-DAL-2.2); Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:163,175,186,195
- **Summary:** DAL-EFFICACY still lists REQ-DAL-2.2 as waived and carries a temporary clause about audit 2026-10-03a. That transition has finished: REQ-DAL-2.2 is now tested.
- **Resolution:** dan-decides

The ruling says: "REQ-DAL-1.14–1.20, 2.1–2.3 and 3.1–3.7 (including 3.2.1) are waived from testing or unenforceable ... (audit 2026-10-03a moves REQ-DAL-2.2 to tested once its tests are cited)". In the current DataAccessLayer.md, REQ-DAL-2.2 is in neither the Waived table (it lists 1.14–1.20, 2.1, 2.3, 3.1, 3.2, 3.4, 3.5, 3.7) nor the Unenforceable table (3.2.1, 3.3, 3.6). DalTests.fs has four tests named for REQ-DAL-2.2 (lines 163, 175, 186, 195). So the condition in the parenthetical has been met, and the range "2.1–2.3" now names a tested ID as waived. The "check the Waived table, not this list" hedge keeps this from causing harm today. But the ledger now contradicts the spec, and the parenthetical belongs to an audit phase that has passed. A smaller point: "validated transitively through the domain tests" does not describe the Unenforceable IDs (3.2.1, 3.3, 3.6), which bind humans. The range 3.1–3.7 also takes in 3.2.2, which has been withdrawn.

**Action:** Rewrite the ruling as: "The DAL IDs in DataAccessLayer.md's Waived table (currently REQ-DAL-1.14–1.20, 2.1, 2.3, 3.1, 3.2, 3.4, 3.5, 3.7) are validated transitively through the domain tests that exercise the DAL, and its Unenforceable IDs (3.2.1, 3.3, 3.6) bind humans, not code. Do not flag missing efficacy findings for those IDs. Tested DAL IDs (currently REQ-DAL-2.2 and 2.4) are in scope for test-efficacy passes. The spec's tables are authoritative over this list. This ruling covers test-efficacy only; it does not suppress spec-quality, ambiguity, or contradiction audits against DataAccessLayer.md." Remove the audit-2026-10-03a parenthetical.

**Why:** A precedent entry that names a tested requirement as waived could lead a test-efficacy auditor to skip the REQ-DAL-2.2 tests in DalTests.fs. The ledger should match the spec's current waiver tables rather than an audit plan that has already run.

---

## STALE-IDIOM-JE-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, IDIOM-JE-1 and the 'How to read this file' retired definition; Tests/Tests.Integrated/CrossDomainOrchestration/AccountBalance.fs:170
- **Summary:** IDIOM-JE-1 is marked retired, yet its note says the reasoning still applies to sign tests re-cited to REQ-AC-3.13. That contradicts the file's rule that retired entries are not precedent, and the re-cite has already happened.
- **Resolution:** dan-decides

The header defines retired as: "Do not cite it as precedent, either to suppress a finding or to support one." IDIOM-JE-1 is marked retired (2026-10-03), but its Retired note says: "If a sign-direction test is re-cited to REQ-AC-3.13, the reasoning below still applies to it." That condition is now met. AccountBalance.fs:170, ``REQ-AC-3.13 net balance is positive in normal-balance orientation``, uses `Assert.True(... netBalance |> Money.amount > zero)` for both the expense and the revenue account. An auditor cannot tell whether the `> zero` assertion is protected. The status says it is not; the note says it is. The ruling's original premise was also tied to the withdrawn REQ-JE-3.6 suite ("The rest of the 3.6 suite asserts correct amounts"). Under REQ-AC-3.13 the spec states the net-balance direction exactly. The neighbouring tests in AccountCreateActivityBalance.fs (lines 647 and 663) already assert exact net balances for debit-normal and credit-normal accounts, so the premise needs restating against the AC-3.13 suite rather than carrying over.

**Action:** Choose one. (a) Retire cleanly: delete the sentence "If a sign-direction test is re-cited to REQ-AC-3.13, the reasoning below still applies to it", so the AC-3.13 sign test is judged fresh against current specs. (b) Rule on the new test: add a new active overruled entry (for example IDIOM-AC-3.13-SIGN) scoped to AccountBalance.fs ``REQ-AC-3.13 net balance is positive in normal-balance orientation``, stating that the `> zero` assertion is acceptable because the AC-3.13 tests at AccountCreateActivityBalance.fs:647/663 assert exact net balances. IDIOM-JE-1 then stays purely historical.

**Why:** The ledger only works if its statuses are binary: a retired entry either suppresses a finding or it does not. A retired entry that still claims to suppress through its note leaves auditors in the same uncertainty the ledger exists to remove.

---

## STALE-DEC-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, DEC-1; CompoundedLearnings/articles/coding/*.md ('Source: the retired Conventions/... (removed 2026-07-30)'); CompoundedLearnings/articles/audit-conduct/conventions-without-reqs.md
- **Summary:** DEC-1 rules on "convention docs". Those were removed on 2026-07-30 and replaced by CompoundedLearnings articles, so the ruling's subject no longer exists under that name.
- **Resolution:** dan-decides

DEC-1 (2026-07-06) reads: "Convention docs hold prose guidance and design rationale ... When a convention encodes a testable rule, it gets extracted to a REQ ID in the behavioral spec (Money.md established this pattern)." The Conventions/ directory is gone. For example, money-type-enforcement.md, money-arithmetic-boundaries.md, descriptive-naming.md and temporal-arithmetic.md each say "Source: the retired Conventions/... (removed 2026-07-30)". The current audit-conduct catalog states the same principle in terms of "learnings" (conventions-without-reqs.md: "Operational rules and domain guidance can exist as learnings without a corresponding REQ ID"; requirements-stricter-than-conventions.md: "Learnings are general. Requirements are specific."). "Money.md" in the ruling is also ambiguous now: the only Money.md left is Specs/Behavioral/Money.md, and the convention it pointed to is gone. The substance still holds; only the subject's name is out of date.

**Action:** Rewrite the ruling as: "Learnings (CompoundedLearnings articles, which replaced the Conventions/ docs on 2026-07-30) hold prose guidance and design rationale. Behavioral specs hold REQ-labeled testable requirements. When a learning encodes a testable rule for a built domain, it gets extracted to a REQ ID (e.g. the Money arithmetic rules → REQ-MON-2.7/2.7.1). A 'must' in a learning and a 'must' in a REQ serve different purposes and do not conflict." Update the Scope line to match. Alternatively, retire DEC-1 as absorbed by the conventions-without-reqs and requirements-stricter-than-conventions articles.

**Why:** A ruling whose subject is a retired document category has a scope auditors cannot match against current files. An auditor might decide it no longer covers learnings and re-raise the convention-versus-requirement 'must' question.

---

## STALE-AMB-13 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, AMB-13; Specs/Behavioral/Money.md REQ-MON-2.7, 2.7.1 and Waived table; CompoundedLearnings/articles/coding/money-arithmetic-boundaries.md; Src/Business.FinancialServices/Money.fs
- **Summary:** AMB-13's scope and ruling point to the retired Money convention ("The convention says 'can't do it'"). The prohibition now lives in REQ-MON-2.7/2.7.1 and the money-arithmetic-boundaries article.
- **Resolution:** dan-decides

AMB-13 (2026-06-13): Scope "Whether Money.md needs to clarify operator vs behavioral prohibition"; Ruling "The code already doesn't define * and / operators on Money. The convention says 'can't do it.'" Conventions/Money.md was removed on 2026-07-30 (money-arithmetic-boundaries.md: "Source: the retired Conventions/Money.md ... This article is now the single home for the rounding and allocation rules"). The behavioral prohibition is now REQ-MON-2.7, with REQ-MON-2.7.1 giving the conversion route. Money.md's Waived table entry for 2.7.1 (restated 2026-10-03) adds an enforcement-by-construction reason: "Money is a private record whose only operations are add, subtract, sum and split." The code fact still holds: Money.fs declares `type Money = private { amount: decimal }`, defines no * or / operators, and exposes only add, subtractVal1FromVal2, sumList, splitByN, comparisons and sign tests. The substance is sound, but the cited authority no longer exists.

**Action:** Rewrite as: Scope "Whether REQ-MON-2.7 needs to distinguish an operator prohibition from a behavioral one"; Ruling "Money is a private record (Money.fs) with no * or / operators. Its only operations are add, subtract, sum and split, so REQ-MON-2.7.1's convert-to-decimal route is the only path to multiplication or division (see Money.md Waived table, REQ-MON-2.7.1, and the money-arithmetic-boundaries learning). Nothing more to clarify."

**Why:** Anchoring the ruling to the live REQ IDs keeps it matchable and lets an auditor check it against current code. As written, it relies on a document a reader can no longer open.

---

## STALE-SS-3 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, SS-3; Specs/Behavioral/SystemWide.md:32
- **Summary:** SS-3 is titled for one SystemWide.md todo, but its ruling covers every todo anywhere and says todos "should not be evaluated in an audit as any sort of stand-alone directive". It could be read as excusing an active REQ that is left unenforced next to a todo.
- **Resolution:** dan-decides

The scope is "Whether `todo` comments must be in reference to an existing REQ", and the title points at a single SystemWide.md todo (still there at line 32: "todo: add a requirement for logging audit activities to an external log"). The ruling text is general: todos mean either a reminder or "I have intentionally not yet implemented something that would otherwise belong in that section", and "should not be evaluated in an audit as any sort of stand-alone directive." Not treating a todo as a requirement is clearly what Dan meant. The second clause can stretch further than the scope: an auditor who finds an active, non-waived REQ unenforced next to a `todo` in Src/ could read SS-3 as making that gap "intentional" and skip it. That would suppress a missing-enforcement finding, which is a different question from the one ruled on (whether todos must reference a REQ). I do not know whether Dan meant todos to excuse active-REQ gaps; the narrower title suggests not.

**Action:** Rewrite the ruling as: "A `todo` comment (in specs or source) is Dan's note-to-self or a marker of deliberately deferred work. It is not a requirement, need not reference a REQ ID, and its presence or wording is never a finding. It does not waive an active requirement: if an active, non-waived REQ is unenforced, that gap is judged on its own, with or without a nearby todo." Retitle it "todo Comments Are Not Directives". If Dan does mean todos to excuse active-REQ gaps, say so explicitly instead.

**Why:** A precedent should suppress only the question Dan actually ruled on. Reading this one broadly would let an unenforced requirement pass an audit because of an informal comment.

---

