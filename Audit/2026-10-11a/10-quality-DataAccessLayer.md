# spec-quality-DataAccessLayer

## AMB-DAL-SCOPE-1 — ambiguity
- **Location:** Specs/Behavioral/DataAccessLayer.md, Scope paragraph (line 5), REQ-DAL-1.20 (line 28), REQ-DAL-3.2 Waived row (line 68); LeoBloomMigrator/ (Program.fs, LeoBloomMigrator.fsproj, appsettings.json); SonOfLeo.slnx
- **Summary:** The DAL spec's scope rules don't say where LeoBloomMigrator fits. It is a second, hand-run "migrator" executable added 2026-10-05. It calls the DAL, sits in the solution, has one prod-named env var, and references Npgsql directly outside the boundary check.
- **Resolution:** dan-decides

The Scope paragraph (2026-10-03) says §1 and §2 govern "the data access layer and everything that calls it". It exempts "the database migration tool", which "is run by hand and manages its own connections". It also says the Npgsql boundary check behind REQ-DAL-3.2 "covers application and test source, not the migration tool". REQ-DAL-1.20 (reworded 2026-10-05) then lists the DAL-calling executables and how each handles environments: the operator CLI and report CLI (one env var per Debug and Release configuration), the integration test suite (single environment, test DB) and the development data stager (single environment, dev DB).

Commit 48e7e4c on 2026-10-05, after the last audit, added LeoBloomMigrator. It is listed in SonOfLeo.slnx, so it is part of "the system" under Definitions.md. Its Program.fs opens App.DataAccessLayer.DbTransaction and App.DataAccessLayer.ExecuteReader and reads through the DAL, so it does not manage its own connections the way DbMigration/Migrator does. That Migrator project references only Npgsql and Configuration, not App.DataAccessLayer.

LeoBloomMigrator's appsettings.json has a single ConnectionStringEnvVar "leo_to_son_prod". It points at the LeoBloom database (its queries use LeoBloom's ledger.account.parent_id and ledger.journal_entry_reference), and nothing selects it by build configuration. LeoBloomMigrator.fsproj also has direct PackageReferences to Npgsql and Npgsql.NodaTime. Checks/check-npgsql.sh scans only Src and Tests, so this project falls outside the check named in the REQ-DAL-3.2 waiver.

Two reasonable readers would classify this tool differently:
- (a) It is "a migration tool" by name and by how it is run (by hand, a one-off transplant). Then it would be exempt from §1 and §2 and from the Npgsql boundary check, even though it uses the DAL.
- (b) It calls the DAL, so §1 and §2 bind it under "everything that calls it". Then REQ-DAL-1.20's list of executables leaves its environment rule unstated: it is neither an interface executable nor the test suite or stager. Its direct Npgsql reference would also sit outside the enforcement mechanism that the REQ-DAL-3.2 waiver relies on.

The spec does not say which reading is right. Dan's vision statement calls the current importers scaffolding, so this may be a deliberate temporary tool. The spec still names neither it nor the category it belongs to.

**Action:** Dan decides how LeoBloomMigrator is classified, then the spec records it. One option: add it to the Scope paragraph's exemptions next to the migration tool, as a one-off transplant tool. The other: add it to REQ-DAL-1.20's list as a single-environment DAL caller, and say whether the Npgsql boundary check (and therefore the REQ-DAL-3.2 waiver reason) covers it.

**Why:** The Scope paragraph and REQ-DAL-1.20 were written so that auditors and LLMs can tell which DAL rules bind which executable. An executable that calls the DAL but fits none of the listed categories leaves REQ-DAL-1.14–1.20 and 3.2 unresolved for it. It also leaves the REQ-DAL-3.2 waiver ("enforced by ... the Npgsql boundary check") resting on a check that does not see the one DAL caller that references Npgsql directly. Without a stated classification, the next audit cannot judge whether this tool complies.

---

