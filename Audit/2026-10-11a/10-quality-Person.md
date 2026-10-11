# spec-quality-Person

_No findings._

## Reasoning

Scope: /home/claude/SonOfLeo/Specs/Behavioral/Person.md. It has not changed since 22af7f4, which applied the 2026-10-04a disposition. All three prior quality findings were applied: PER-DEL-1 became REQ-PER-3.1 with its waiver, the PER-TERM-1 design note now says "financial account or property", and PER-SD-1 was handled by rewording the intro. I checked the spec against each item on the list.

(1) Terms. The spec uses "Person" and "User" exactly as Specs/Definitions.md does in §Person and §User. "Calendar Date" in REQ-PER-1.4 matches the Date (calendar) definition and is spelled the same way as the Pre-ledger date definition.

(2) Internal contradictions. None. Uniqueness (REQ-PER-1.3) is what makes addressing a Person by name (2.2, 2.4) and giving owners by name sound. The no-delete rule (3.1) agrees with the design note.

(3) Cross-spec checks:
- REQ-PER-1.1 cites REQ-SYS-1.1 (trim) correctly, and REQ-PER-1.5 cites SYS-7.1 and SYS-3.4 correctly.
- REQ-PER-2.4 agrees with REQ-SYS-6.2 and 6.3.
- A both-fields-unchanged update in 2.2 is covered by SYS-6.1's "an unchanged value is not a no-op" note.
- SystemWide §4 leaves deletion policy to each domain, and Person.md decides it in 3.1.
- REQ-POS-4.5, 9.3, 11.3, 11.6 and 11.11 reference Persons the same way.
- Positions slice 2 (§§12–15: activity, roll-forward, pre-ledger balances) and its migrations add no Person reference, so nothing new reaches Person.

(4/5) Ambiguity and elaboration:
- The comparison rule in 1.3 (exact, case-sensitive, after trim) matches the DB UNIQUE(person_name) constraint, so precedent UNIQUE-DB applies to the loud rejection.
- I considered the collation of "ordered by name" in 2.3 and judged it below the reasonable-person bar. The code sorts by ordinal name (PersonOrchestration.fs:56).
- REQ-PER-1.5's "current date" is defined fully through SYS-3.4 and 7.1.

(6) The Withdrawn table is empty, so there is nothing to judge.

(7) The waiver on REQ-PER-3.1 (a negative existence claim, enforced by code review and adversarial audit) is the same reasoning as the approved REQ-AC-5.1 waiver. It holds: there is no delete function or route in Src/Business.General/Person*.fs, PersonOrchestration.fs or the Person routes. The DB grant of DELETE to the app role matches REQ-PER-3.1, which forbids only a user interface for deleting.

Three-state rule: of 10 active REQs, REQ-PER-3.1 is waived, and REQ-PER-1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3 and 2.4 are each cited by at least one test across Tests.Isolated/Model/General/Person.fs, PersonMaintenance.fs, PersonRoutes.fs, InvestmentAccountMaintenance.fs and PropertyMaintenance.fs.

Dan's statement this run says nothing about Person, so there is no statement-delta.
