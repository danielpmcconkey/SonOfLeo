# 02-J Discussion On Phase 2 Lows

2026-07-06

## What's this?

Our real-time discussion about lows was a batch discussion that was too difficult for me to separate out into their individual domains, so I'm clubbing it together here.

## Discussion



**[11:16:17 EDT] Hobson:**
That's the last medium. On to the lows — 17 of them. Want to keep the same pace, or batch-rule the ones that are clearly noise?

---

**[11:16:31 EDT] Dan:**
List the lows here

---

**[11:16:50 EDT] Hobson:**
1. **SD-06** — Balance/activity reporting primitives exist despite "no real reporting"
2. **SD-07** — REQ-NGUI-1.4 has no annotation, test, or waiver
3. **SS-DAL-03** — No waived-from-testing table in DAL.md
4. **AMB-DAL-04** — Asymmetric empty vs whitespace wording (DAL-1.15 vs 1.18)
5. **GAP-JE-4** — REQ-JE-5.4 withdrawal left secondary comment link effectively fixed
6. **AMB-JE-5** — Void atomicity unspecified (void marker + reason comment not stated as transactional)
7. **CLR-JE-6** — REQ-JE-1.29 says "Journal entry ID" should say "Journal entry line"
8. **AMB-JE-7** — REQ-JE-3.9 ordering direction unspecified (ascending/descending)
9. **RED-AC-1** — REQ-AC-1.19 / 1.19.1 duplicate
10. **REF-AC-1** — REQ-AC-2.20.1 dangling reference (copy-paste artifact)
11. **AMB-AC-3** — "inactive" synonym creates tri-state ambiguity (not-yet-begun / active / deactivated)
12. **MON-4** — Conversion "validate all section 1" includes unvalidatable MON-1.1
13. **MON-5** — "Sort order" should say "positional order"
14. **MON-6** — Batch conversion atomicity unspecified
15. **WAV-FP-1** — Period key null waiver uses value-type reasoning for a string
16. **TERM-1** — "UI domain types" terminology inconsistent with Definitions.md
17. **CQ-6** — Doctrines validateThenConstruct vs value-object create naming gap
18. **CQ-7** — Doctrines says "create" is anti-pattern, Naming says use "create"
19. **CQ-8** — Money.md split residual "one of the parts" looser than REQ-MON-2.4.5 "first share"

---

**[11:50:54 EDT] Dan:**

## Overruled

4. AMB-DAL-04 — Asymmetric empty vs whitespace wording (DAL-1.15 vs 1.18)
   So what? Add an action item to the skill. This isn't a grammar lesson. This might be part of the "reasonable map (or bot)" clause. Anyone reading knows the intent.

8. AMB-JE-7 — REQ-JE-3.9 ordering direction unspecified (ascending/descending)
   We also don't state how wet water should be. Unless you can find a single database architecture that defaults to descending sort order. Another "reasonable man (or bot)" call out here.

15. WAV-FP-1 — Period key null waiver uses value-type reasoning for a string
    kindly ask your auditor what "NOT NULL" means in DbMigrations/202606201243-CreateFiscalPeriodTable.sql line 8. Then put an action item to update the skill such that these jackasses learn the difference between prose, database null, and F# None.

19. CQ-8 — Money.md split residual "one of the parts" looser than REQ-MON-2.4.5 "first share"
    yes. requirements are allowed to be more strict than their underlying conventions. Add an action item to put that in the skill somewhere

## Actioned

2. SD-07 — REQ-NGUI-1.4 has no annotation, test, or waiver
   I added "| REQ-NGUI-1.4 | You can't test a negative and it's also quite clear by the interface contracts that codes are present | Dan, 2026-07-06 |" to the waiver list

7. CLR-JE-6 — REQ-JE-1.29 says "Journal entry ID" should say "Journal entry line"
   fixed. Good catch

10. REF-AC-1 — REQ-AC-2.20.1 dangling reference (copy-paste artifact)
    2.20.1 has been deleted. As it was never a req in and of itself, I assume that's not an impacting decision. We'll find out next audit.

13. MON-5 — "Sort order" should say "positional order"
    I changed the verbiage of 2.3.2 to "will preserve the sort / positional order" but is there any difference?

## Discuss

1. SD-06 — Balance/activity reporting primitives exist despite "no real reporting"
   I don't understand this.

3. SS-DAL-03 — No waived-from-testing table in DAL.md
   There is no file named DAL.md. And Specs/Behavioral/DataAccessLayer.md clearly has a waved from testing table. What went wrong here?

5. GAP-JE-4 — REQ-JE-5.4 withdrawal left secondary comment link effectively fixed
   How? The primary link is fixed in place. The secondary link is mutable. Or is this stating that we didn't specifically write a REQ to comment insist that a secondary link can be updated?

6. AMB-JE-5 — Void atomicity unspecified (void marker + reason comment not stated as transactional)
   I don't understand this

9. RED-AC-1 — REQ-AC-1.19 / 1.19.1 duplicate
   good catch. Add an action item for discerning the least damaging way to fix

11. AMB-AC-3 — "inactive" synonym creates tri-state ambiguity (not-yet-begun / active / deactivated)
    What's the ambiguity?

12. MON-4 — Conversion "validate all section 1" includes unvalidatable MON-1.1
    I don't understand

14. MON-6 — Batch conversion atomicity unspecified
    assuming you're talking about REQ-MON-2.3 and fromDecimalList in Money.fs, why would we need to? this isn't a write-to-persistence operation. what am I missing?

16. TERM-1 — "UI domain types" terminology inconsistent with Definitions.md
    Example please

17. CQ-6 — Doctrines validateThenConstruct vs value-object create naming gap
    please elaborate

18. CQ-7 — Doctrines says "create" is anti-pattern, Naming says use "create"
    I don't understand

---

**[11:51:42 EDT] Hobson:**
That's a lot. Let me update the overruled/actioned items first, then work through the discuss items.

---

**[11:51:45 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 31 | SD-07 | REQ-NGUI-1.4 has no annotation, test, or waiver | | |",
  "new_string": "| 31 | SD-07 | REQ-NGUI-1.4 has no annotation, test, or waiver | Dan added waiver | RESOLVED |"
}
```

---

**[11:51:53 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 33 | AMB-DAL-04 | Asymmetric empty vs whitespace wording (DAL-1.15 vs 1.18) | | |",
  "new_string": "| 33 | AMB-DAL-04 | Asymmetric empty vs whitespace wording (DAL-1.15 vs 1.18) | Reasonable person knows the intent | OVERRULED |"
}
```

---

**[11:52:00 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 36 | CLR-JE-6 | REQ-JE-1.29 mis-subjected (\"entry ID\" should say \"line\") | | |",
  "new_string": "| 36 | CLR-JE-6 | REQ-JE-1.29 mis-subjected (\"entry ID\" should say \"line\") | Dan fixed | RESOLVED |"
}
```

---

**[11:52:07 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 37 | AMB-JE-7 | REQ-JE-3.9 ordering direction unspecified | | |",
  "new_string": "| 37 | AMB-JE-7 | REQ-JE-3.9 ordering direction unspecified | Databases default to ascending. Reasonable person call | OVERRULED |"
}
```

---

**[11:52:15 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 39 | REF-AC-1 | REQ-AC-2.20.1 dangling reference (copy-paste artifact) | | |",
  "new_string": "| 39 | REF-AC-1 | REQ-AC-2.20.1 dangling reference (copy-paste artifact) | Dan deleted 2.20.1 | RESOLVED |"
}
```

---

**[11:52:21 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 42 | MON-5 | \"Sort order\" should say \"positional order\" | | |",
  "new_string": "| 42 | MON-5 | \"Sort order\" should say \"positional order\" | Dan updated to \"sort / positional order\" | RESOLVED |"
}
```

---

**[11:52:29 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 44 | WAV-FP-1 | Period key null waiver uses value-type reasoning for a string | | |",
  "new_string": "| 44 | WAV-FP-1 | Period key null waiver uses value-type reasoning for a string | DB column is NOT NULL — auditor didn't check the migration | OVERRULED |"
}
```

---

**[11:52:35 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 48 | CQ-8 | Money.md split residual looser than REQ-MON-2.4.5 | | |",
  "new_string": "| 48 | CQ-8 | Money.md split residual looser than REQ-MON-2.4.5 | Requirements are allowed to be stricter than conventions | OVERRULED |"
}
```

---

**[11:52:45 EDT] Hobson:**
Now — on MON-5, no, there's no meaningful difference between "sort order" and "positional order" when the input is a list. A list has a fixed sequence; "sort / positional order" makes it unambiguous.

Skill action items for the overrules:

---

**[11:52:52 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 27a | — | Audit skill: conventions flagged for missing REQ IDs when no domain exists to receive them | Update prompts: conventions can exist as prose without REQ IDs when the business domain they apply to hasn't been specced yet. REQ extraction happens when the domain is built, not before | CONFIRMED |",
  "new_string": "| 27a | — | Audit skill: conventions flagged for missing REQ IDs when no domain exists to receive them | Update prompts: conventions can exist as prose without REQ IDs when the business domain they apply to hasn't been specced yet. REQ extraction happens when the domain is built, not before | CONFIRMED |\n| 33a | — | Audit skill: auditors must check migrations/schema before questioning waiver soundness | Update prompts: before flagging a waiver as unsound, verify against the DB schema (NOT NULL constraints, types) and the F# type system — not just the prose | CONFIRMED |\n| 48a | — | Audit skill: requirements may be stricter than conventions | Update prompts: a behavioral requirement that is narrower than its underlying convention is not a contradiction — requirements elaborate conventions for specific domains | CONFIRMED |"
}
```

---

**[11:53:33 EDT] Hobson:**
Now the discuss items:

---

**SD-06:** Your statement said "no real reporting." The agent noted that `AccountBalance` (FetchBalances) and `AccountActivity` (FetchActivity) exist as CLI verbs. They're per-account read primitives, not trial balances or period reports. The agent agreed your statement holds at the report level — it's just saying the boundary between "done" and "not started" is softer than stated. Calibration note, not a real finding. Overrule?

**SS-DAL-03:** You're right, DataAccessLayer.md has a Waived table now — but you added it *this session* when we extracted REQ-DAL-3.7. The auditor ran before that. Finding was valid at audit time, now resolved.

**GAP-JE-4:** Yes, that's exactly it. REQ-JE-5.4 was withdrawn as "too restrictive" — it fixed both primary and secondary comment links. The withdrawal implies secondary should be re-pointable. But no active REQ provides an operation to change the secondary link — so it's still effectively write-once, contradicting the withdrawal's stated intent. Question for you: *should* the secondary link be re-pointable, or was the withdrawal just about the primary?

**AMB-JE-5:** When you void an entry, two things happen atomically (or should): set `voided_at` and create the reason comment (REQ-JE-4.4). Posting has explicit atomicity requirements (REQ-JE-2.11/2.12 — all-or-nothing). Void doesn't. If the void marker is set but the comment insert fails, you have a voided entry with no reason. Question: is the void operation wrapped in a transaction in your code?

**RED-AC-1:** Logged.

**AMB-AC-3:** REQ-AC-1.48 says `"deactivated" (or "inactive")`. REQ-AC-1.50 defines "active" using both boundaries. An account before its active_begin is *not active* (fails 1.50) but also *not deactivated* (fails 1.48 — no active_end set). So there are three states: not-yet-begun, active, deactivated. The "(or inactive)" synonym invites reading "inactive" as "not active," which would wrongly include not-yet-begun accounts. Impact is low since "inactive" appears only in that one definition.

**MON-4:** REQ-MON-2.2.1 says "validate all requirements from section 1" when converting a decimal to Money. Section 1 includes REQ-MON-1.1 (Money is USD). A raw decimal has no currency — you can't validate 1.1 from a number. And 1.1 is itself waived as untestable. The fix would be "validate REQ-MON-1.2 through 1.4" instead of "all of section 1."

**MON-6:** You're right — `fromDecimalList` isn't a persistence operation. The question is whether one invalid decimal in the list fails the whole call or just drops that element. But looking at the code, `List.map fromDecimal` would propagate the first error through the Result railway. The behavior is determined by the implementation, and it's the obvious one. Overrule?

**TERM-1:** NGUI spec line 3 says "does not apply to any graphical user interface." But section 2 is titled "UI domain types" and REQ-NGUI-1.4 says "The user interface must never..." Definitions.md uses "Interface" / "Interface layer" as the pinned terms, never "UI." The concern is that "UI" reads as graphical to most people, which contradicts the doc's own scope.

**CQ-6:** Doctrines says "every entity type has exactly one private function called `validateThenConstruct`" and "no record literals may appear anywhere outside `validateThenConstruct`." But value objects like AccountName and Money use `create` / `fromDecimal` as their constructors, not `validateThenConstruct`. Doctrines never says value objects are exempt from the rule. The gap is that a reader can't tell from Doctrines alone whether `AccountName.create` should actually be named `validateThenConstruct`.

**CQ-7:** Same neighborhood. Doctrines section 2 says "`constructNewAndSaveToDbUsingParentCode` is preferred over `create`" — holding up `create` as the bad, vague name. Naming.md says "Does the type wrap the input? Use `create`." So `create` is simultaneously the anti-pattern example (Doctrines) and the sanctioned verb (Naming). The reconciliation is that Doctrines is talking about orchestration functions and Naming is talking about value-object constructors, but neither doc says so.

---

**[11:53:33 EDT] Hobson:**
Now the discuss items:

---

**SD-06:** Your statement said "no real reporting." The agent noted that `AccountBalance` (FetchBalances) and `AccountActivity` (FetchActivity) exist as CLI verbs. They're per-account read primitives, not trial balances or period reports. The agent agreed your statement holds at the report level — it's just saying the boundary between "done" and "not started" is softer than stated. Calibration note, not a real finding. Overrule?

**SS-DAL-03:** You're right, DataAccessLayer.md has a Waived table now — but you added it *this session* when we extracted REQ-DAL-3.7. The auditor ran before that. Finding was valid at audit time, now resolved.

**GAP-JE-4:** Yes, that's exactly it. REQ-JE-5.4 was withdrawn as "too restrictive" — it fixed both primary and secondary comment links. The withdrawal implies secondary should be re-pointable. But no active REQ provides an operation to change the secondary link — so it's still effectively write-once, contradicting the withdrawal's stated intent. Question for you: *should* the secondary link be re-pointable, or was the withdrawal just about the primary?

**AMB-JE-5:** When you void an entry, two things happen atomically (or should): set `voided_at` and create the reason comment (REQ-JE-4.4). Posting has explicit atomicity requirements (REQ-JE-2.11/2.12 — all-or-nothing). Void doesn't. If the void marker is set but the comment insert fails, you have a voided entry with no reason. Question: is the void operation wrapped in a transaction in your code?

**RED-AC-1:** Logged.

**AMB-AC-3:** REQ-AC-1.48 says `"deactivated" (or "inactive")`. REQ-AC-1.50 defines "active" using both boundaries. An account before its active_begin is *not active* (fails 1.50) but also *not deactivated* (fails 1.48 — no active_end set). So there are three states: not-yet-begun, active, deactivated. The "(or inactive)" synonym invites reading "inactive" as "not active," which would wrongly include not-yet-begun accounts. Impact is low since "inactive" appears only in that one definition.

**MON-4:** REQ-MON-2.2.1 says "validate all requirements from section 1" when converting a decimal to Money. Section 1 includes REQ-MON-1.1 (Money is USD). A raw decimal has no currency — you can't validate 1.1 from a number. And 1.1 is itself waived as untestable. The fix would be "validate REQ-MON-1.2 through 1.4" instead of "all of section 1."

**MON-6:** You're right — `fromDecimalList` isn't a persistence operation. The question is whether one invalid decimal in the list fails the whole call or just drops that element. But looking at the code, `List.map fromDecimal` would propagate the first error through the Result railway. The behavior is determined by the implementation, and it's the obvious one. Overrule?

**TERM-1:** NGUI spec line 3 says "does not apply to any graphical user interface." But section 2 is titled "UI domain types" and REQ-NGUI-1.4 says "The user interface must never..." Definitions.md uses "Interface" / "Interface layer" as the pinned terms, never "UI." The concern is that "UI" reads as graphical to most people, which contradicts the doc's own scope.

**CQ-6:** Doctrines says "every entity type has exactly one private function called `validateThenConstruct`" and "no record literals may appear anywhere outside `validateThenConstruct`." But value objects like AccountName and Money use `create` / `fromDecimal` as their constructors, not `validateThenConstruct`. Doctrines never says value objects are exempt from the rule. The gap is that a reader can't tell from Doctrines alone whether `AccountName.create` should actually be named `validateThenConstruct`.

**CQ-7:** Same neighborhood. Doctrines section 2 says "`constructNewAndSaveToDbUsingParentCode` is preferred over `create`" — holding up `create` as the bad, vague name. Naming.md says "Does the type wrap the input? Use `create`." So `create` is simultaneously the anti-pattern example (Doctrines) and the sanctioned verb (Naming). The reconciliation is that Doctrines is talking about orchestration functions and Naming is talking about value-object constructors, but neither doc says so.

---

**[12:26:11 EDT] Dan:**

SD-06. whatever. I also said, in that same prompt "Front end CLI exposes a few quality of life functions for retrieving useful Account and JE data". Overruled and I can't even think of a way to keep these guys from wasting my time / tokens on this sort of shit in the future. How does this citation make my life or this app better?

SS-DAL-03. Ah, so...sustained and acted on?

GAP-JE-4. The withdrawal was specifically because I wanted the secondary to be repointable. I added this
- **REQ-JE-1.56** Comment secondary journal entry ID may be updated to be pointed at a different JE or to no JE

AMB-JE-5 voidJournalEntryOrchestration creates a transaction and rolls back on any Error.

AMB-AC-3
The real problem is taht 1.48 and 1.50 are fighting

        - **REQ-AC-1.48** An Account record is considered "deactivated" (or "inactive") relative to a reference Calendar Date when its "active end" is non-null and is earlier than that reference date (the active-end date itself is still active — the boundary is inclusive).
        - **REQ-AC-1.50** An Account record is considered "active" relative to a reference Calendar Date when its "active begin" is earlier than or equal to that reference date AND (its "active end" is null OR its "active end" is later than or equal to that reference date). Both boundaries are inclusive.
    I think that 1.50 is correct and 1.48 should go away. Add an action item to discuss with you on a clean context

MON-4 Alright! we found a real unenforceable req. MON-1.1. Add an action item to add it to an unenforceable table. I just edited 2.2.1 to except 1.1

MON-6 overrule

TERM-1 Non-graphical UI is a subset of UI in much the same way that CLI and TLI are. One would think that was obvious. Definitions.md defines things for the UI layer, of which NGUI is a component. Specs/Behavioral/NonGraphicalInterface.md line 3 very clearly says "this section does not apply to any graphical user interface interactions". And REQ-NGUI-1.4 very clearly is underneath heading 1 "General non-graphical interaction". So...what idiot thinks that leaves the door open for ambiguity? It may be a req that will be restated elsewhere when we do have other interface types. It may be moved to a more general UI reqs section once we have another UI. But for now, what's the ambiguity?

CQ-6
"Doctrines never says value objects are exempt from the rule." Except that it does say "entity" where definitions.md actually defines what an entity is.

    ## Entity (as a variety of record)
    A record type the system creates or mutates at runtime on behalf of the user. 

    Granted, we do say "(as a variety of record)" in the definition and we don't say anything to reference definitions in the doctrine. But...seeing as how Doctrines.md is really the scribblings that will shape the eventual SonOfLeo:Coding skill, I'm not going to over-perscribe here. I want that doc to be read as X is generally a best practice, but not a rule. 
    
    So I proabably want to re-frame my doctrine but today is not that day

CQ-7 same as CQ-6

---

**[12:26:25 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 30 | SD-06 | Balance/activity reporting primitives exist despite \"no real reporting\" | | |",
  "new_string": "| 30 | SD-06 | Balance/activity reporting primitives exist despite \"no real reporting\" | Dan's statement already covered this (\"quality of life functions\") | OVERRULED |"
}
```

---

**[12:26:32 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 32 | SS-DAL-03 | No waived-from-testing table in DAL.md | | |",
  "new_string": "| 32 | SS-DAL-03 | No waived-from-testing table in DAL.md | Valid at audit time — Dan added the table this session when REQ-DAL-3.7 was extracted | RESOLVED |"
}
```

---

**[12:26:38 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 34 | GAP-JE-4 | REQ-JE-5.4 withdrawal left secondary link effectively fixed | | |",
  "new_string": "| 34 | GAP-JE-4 | REQ-JE-5.4 withdrawal left secondary link effectively fixed | Dan added REQ-JE-1.56 — secondary link is now explicitly repointable | RESOLVED |"
}
```

---

**[12:26:45 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 35 | AMB-JE-5 | Void atomicity unspecified | | |",
  "new_string": "| 35 | AMB-JE-5 | Void atomicity unspecified | Code already wraps void in a transaction with rollback on Error | OVERRULED |"
}
```

---

**[12:26:52 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 38 | RED-AC-1 | REQ-AC-1.19 / 1.19.1 duplicate | | |",
  "new_string": "| 38 | RED-AC-1 | REQ-AC-1.19 / 1.19.1 duplicate | Determine least damaging way to consolidate | CONFIRMED |"
}
```

---

**[12:26:59 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 40 | AMB-AC-3 | \"inactive\" synonym creates tri-state ambiguity | | |",
  "new_string": "| 40 | AMB-AC-3 | \"inactive\" synonym creates tri-state ambiguity | Real issue is 1.48 and 1.50 fighting. 1.50 is correct, 1.48 may need to go. Discuss in clean context | CONFIRMED |"
}
```

---

**[12:27:07 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 41 | MON-4 | Conversion \"validate all section 1\" includes unvalidatable MON-1.1 | | |",
  "new_string": "| 41 | MON-4 | Conversion \"validate all section 1\" includes unvalidatable MON-1.1 | Dan edited 2.2.1 to except 1.1. MON-1.1 is genuinely unenforceable — add to an unenforceable table when those are created (see CQ-5) | CONFIRMED |"
}
```

---

**[12:27:13 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 43 | MON-6 | Batch conversion atomicity unspecified | | |",
  "new_string": "| 43 | MON-6 | Batch conversion atomicity unspecified | Not a persistence operation — behavior is obvious from the code | OVERRULED |"
}
```

---

**[12:27:20 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 45 | TERM-1 | \"UI domain types\" terminology inconsistent with Definitions.md | | |",
  "new_string": "| 45 | TERM-1 | \"UI domain types\" terminology inconsistent with Definitions.md | NGUI is a subset of UI. Document clearly scopes itself at line 3 | OVERRULED |"
}
```

---

**[12:27:28 EDT] Hobson tool: Edit**
```json
{
  "replace_all": false,
  "file_path": "Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md",
  "old_string": "| 46 | CQ-6 | Doctrines validateThenConstruct vs value-object create naming gap | | |\n| 47 | CQ-7 | Doctrines says \"create\" is anti-pattern, Naming says use \"create\" | | |",
  "new_string": "| 46 | CQ-6 | Doctrines validateThenConstruct vs value-object create naming gap | Doctrines says \"entity\" which Definitions.md defines. Doctrines needs reframing eventually but not today | DEFERRED |\n| 47 | CQ-7 | Doctrines says \"create\" is anti-pattern, Naming says use \"create\" | Same as CQ-6 — Doctrines reframing | DEFERRED |"
}
```

---

**[12:27:39 EDT] Hobson:**
Phase 2 complete. 48 items reviewed:
- **Resolved:** 14 (fixed in session)
- **Overruled:** 19
- **Confirmed (action items):** 11
- **Deferred:** 2

Plus 6 skill-improvement action items logged for future audit runs.

Ready for Phase 3 (code truthfulness) when you are, or do you want to call it here and pick up next session?

---


## Dan note after the fact

- at 11:16, I say "looking at Traceability.md, I see the concern." I don't see that file anywhere. Did it get deleted?
