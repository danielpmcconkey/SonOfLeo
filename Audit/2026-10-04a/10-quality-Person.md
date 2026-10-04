# spec-quality-Person

## PER-DEL-1 — enforcement-gap
- **Location:** Specs/Behavioral/Person.md, the **Deletion.** paragraph (line 17); compare REQ-AC-5.1 and SystemWide.md §4
- **Summary:** Person's ban on deletion is written as an unnumbered prose paragraph, so it falls outside the rule that every active requirement is tested, waived or unenforceable.
- **Resolution:** fix-spec

Person.md line 17 says: "**Deletion.** There is no means to delete a Person. Owners are referenced by investment accounts and property (Positions), and their history must keep resolving." This is a behavioural constraint on the system, not background. SystemWide.md §4 says: "Whether an entity's records may be hard-deleted is a domain-level decision, made in each entity's spec (for Accounts, see REQ-AC-5.1)." For Accounts, the same decision is a numbered requirement. REQ-AC-5.1 says "must not provide a user interface for hard-deleting an Account record", and AccountCrud.md lists it in a table with a reason and Dan's approval ("negative existence claim ... enforced by code review and periodic adversarial audit"). Person's version has no REQ ID, so the traceability audit cannot see it, and it can never be tested, waived or marked unenforceable. As it stands, a future Person delete route would break no ID. The schema also does not fully back the prose. Migration 202610041000 grants `SELECT, INSERT, UPDATE, DELETE ON TABLE general.person TO sonofleo_{ENV}`. The FKs from positions.investment_account_owner and positions.property_owner block deleting only a Person who is referenced. An unreferenced Person can still be deleted at the database level, and the spec says nothing either way.

**Action:** Give the deletion rule a REQ ID in Person.md (e.g. REQ-PER-2.5: "The system must not provide a means to delete a Person."), and put it in the Unenforceable or Waived table with a reason and approval, as REQ-AC-5.1 is.

**Why:** The three-state rule only works when every requirement has an ID. A rule written as prose next to the numbered requirements is invisible to the traceability gate. It is also the only rule in Person.md that protects the history Positions owner links depend on.

---

## PER-TERM-1 — contradiction
- **Location:** Specs/Behavioral/Person.md, design note "a Person is not a User" (line 7) vs Specs/Definitions.md §Person (line 55)
- **Summary:** Person.md's design note defines a Person as related to "a financial account" only, which is narrower than Definitions.md's "a financial account or property".
- **Resolution:** fix-spec

Definitions.md line 55: "A human with a relationship to a financial account or property the system tracks — an owner, for example." Person.md's design note: "A Person is a human with a relationship to a financial account (see Definitions)." It cites Definitions but leaves out property. Person.md's own intro ("accounts and property") and its Deletion paragraph ("investment accounts and property (Positions)") include property. So do REQ-POS-9.3 and 11.6, which give Property owners as Person names. Dan's statement this run has the same narrowing ("a Person (a human with a relation to an account)"). The higher authority, Definitions.md, includes property. The design note quotes it narrowly and says it is quoting it.

**Action:** Change the design note to "a human with a relationship to a financial account or property the system tracks (see Definitions)", or just point to the Definitions entry without restating it.

**Why:** Definitions.md exists so that terms that decide which requirements apply are defined once. A narrower paraphrase inside the domain spec could suggest that a Property owner is not a Person in the full sense. It is also lower authority restating higher authority inaccurately.

---

## PER-SD-1 — statement-delta
- **Location:** Specs/Behavioral/Person.md intro (line 3-5); DbMigration/Scripts/202610041000-CreateSchemaGeneralAndPerson.sql line 1
- **Summary:** Person.md says Person "sits beneath" all of the "more than one domain" that use it, but only the Positions domain uses Person today. Dan's statement gives a different rationale: Person is the more general concept.
- **Resolution:** dan-decides

Person.md intro: "A Person is a business concept that more than one domain leans on, so it sits beneath all of them." The migration comment says "every domain may lean on". In Src, the only things that use PersonId or Business.General.Person outside the Person component, its orchestration and its routes are Business.FinancialServices.Positions (InvestmentAccount.fs, Property.fs) and their orchestrations (InvestmentOrchestration, RealEstateOrchestration). Ledger, CashFlow, DataIngestion and Classification do not reference it. Dan's statement explains the placement differently: "The Person type was defined in Business.General as the concept is more general than the account owner of positions domain entities. (we don't track ledger account owners, but we may want to someday)." One could read "more than one domain" as meaning investments and real estate, but Dan's architecture section treats those as peers inside one positions domain. So the spec's rationale describes a present fact that does not hold, and Dan's describes a forward-looking choice. This is design-note prose, not a REQ, so no test or implementation is affected.

**Action:** Dan to decide whether the intro should say why Person is placed where it is ("more general than any one domain's owner concept; today only Positions references it") rather than claim more than one domain uses it now.

**Why:** A design note that misstates the current dependency graph can mislead future architecture decisions. For example, someone might assume Ledger already depends on Person when judging the impact of a change to it.

---

