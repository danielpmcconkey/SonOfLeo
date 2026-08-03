# JournalEntryCrud Auditor

## CON-JE-1 — contradiction
- **Location:** Specs/Behavioral/JournalEntryCrud.md, REQ-JE-3.9.1 vs REQ-JE-3.9.3
- **Summary:** REQ-JE-3.9.1 asserts a fixed ordering by entry date; REQ-JE-3.9.3 gives the caller a choice of ordering, making 3.9.1 false when account code is chosen.
- **Resolution:** fix-spec

REQ-JE-3.9.1 (line 104) states: 'The result is ordered by entry date.' This is an unqualified assertion — the result IS ordered by entry date. REQ-JE-3.9.3 (line 105) states: 'The result can be ordered by entry date or account code at the caller's choosing.' When the caller chooses account-code ordering under 3.9.3, the assertion in 3.9.1 that 'the result is ordered by entry date' becomes false. Both are active requirements with test coverage (Tests.Integrated/ModelOrchestrator/AccountActivity.fs confirms both), so neither is dead letter. The likely history: 3.9.1 was written first with a fixed ordering, and 3.9.3 was added later to make ordering caller-choosable, but 3.9.1's ordering clause was not amended.

**Action:** Remove the sentence 'The result is ordered by entry date.' from REQ-JE-3.9.1, since REQ-JE-3.9.3 now governs ordering. Alternatively, reword it to establish entry date as the default when no sort is specified.

**Why:** Two developers reading these requirements independently would implement conflicting behavior: one would hardcode entry-date ordering (following 3.9.1), the other would make it caller-selectable (following 3.9.3).

---

## STALE-JE-1 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md, REQ-JE-3.4 (line 95)
- **Summary:** REQ-JE-3.4's inline note claims no test cites this requirement, but a test does exist.
- **Resolution:** fix-spec

REQ-JE-3.4 contains the note: 'No test currently cites this requirement; the capability is exercised through JE-3.9.' However, Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs:679 contains a test named 'REQ-JE-3.4 FetchLinesByAccount validates input as valid types' that explicitly cites this requirement. The note is factually stale and could mislead a future auditor or developer into believing this requirement is unverified and should be waived.

**Action:** Remove the stale note from REQ-JE-3.4 ('Note: this requirement is retained alongside JE-3.9 ... No test currently cites this requirement; the capability is exercised through JE-3.9.'). The requirement stands on its own and is tested.

**Why:** A stale note about test coverage creates a false impression that the requirement is unverified, which could lead to unnecessary waiver discussions or duplicated test effort.

---

## STALE-JE-2 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md, REQ-JE-3.9.3 (line 105)
- **Summary:** REQ-JE-3.9.3 exhaustively enumerates two sort options (entry date, account code), but the code and tests support a third (amount) under the same REQ ID.
- **Resolution:** fix-spec

REQ-JE-3.9.3 states: 'The result can be ordered by entry date or account code at the caller's choosing.' The phrasing 'entry date or account code' reads as an exhaustive enumeration. However, Tests.Integrated/ModelOrchestrator/AccountActivity.fs:216 contains a test named 'REQ-JE-3.9.3 fetchFiltered sort by amount' that exercises AmountAsc/AmountDesc sort orders under the same REQ ID. The spec text does not mention amount as a sort option, yet the code implements it and the test cites this requirement for it. The spec has fallen behind the implementation.

**Action:** Update REQ-JE-3.9.3 to include amount as a sort option: 'The result can be ordered by entry date, account code, or amount at the caller's choosing.'

**Why:** A spec requirement that enumerates options exhaustively but omits an implemented and tested option is stale. A developer reading the spec would not know amount sorting is available; a future auditor might flag the amount tests as unspecced.

---

## STALE-JE-3 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md, REQ-JE-2.4 (line 77)
- **Summary:** REQ-JE-2.4 cross-references REQ-SYS-2.1.1 as its authority for pre-write rejection, but the check requires database state, placing it under REQ-SYS-2.1.2.
- **Resolution:** fix-spec

REQ-JE-2.4 states: 'the system must reject any line whose account code does not resolve to an existing account (before any database write, per REQ-SYS-2.1.1).' REQ-SYS-2.1.1 covers 'Rejections determinable from the entity's own properties' — checks that need no database lookup. Account code resolution requires reading the account table to confirm the code maps to an existing account, which is a database-state check. REQ-SYS-2.1.2 covers 'Rejections requiring database state' and permits them to 'fall through to database constraints.' REQ-JE-2.4 chooses to be stricter than 2.1.2 allows (doing the check before writing rather than relying on the FK constraint), which is legitimate, but it cites the wrong SYS requirement as justification. For comparison, REQ-JE-2.6 and 2.7 (period existence and open-state checks) are also database-state rejections but correctly omit any REQ-SYS-2.1.1 citation.

**Action:** Remove the parenthetical '(before any database write, per REQ-SYS-2.1.1)' from REQ-JE-2.4. The requirement is self-standing — it says 'must reject' which already implies pre-persist validation. If a cross-reference is desired, cite REQ-SYS-2.1.2 and note the deliberate choice not to fall through to constraints.

**Why:** A cross-reference to the wrong SYS requirement could mislead a future developer or auditor about the scope of REQ-SYS-2.1.1, or about which validation checks are considered property-level vs database-state checks.

---
