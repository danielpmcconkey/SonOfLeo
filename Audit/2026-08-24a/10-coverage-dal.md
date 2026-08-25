# code-outward-dal

## MR-DAL-TRAN-1 — missing-requirement
- **Location:** Src/DataAccessLayer/DbTransaction.fs; Specs/Behavioral/DataAccessLayer.md
- **Summary:** The entire transaction management subsystem in DbTransaction.fs has zero REQ coverage in DataAccessLayer.md.
- **Resolution:** fix-spec

DataAccessLayer.md has 19 active requirements covering connection strings (1.x), query execution (2.x), and architecture (3.x). There are zero requirements covering the transaction lifecycle: createDbTransaction (line 40), commit (line 73), rollback (line 76), runWithAutoCompleteTransaction (line 82), createNoTransaction (line 108), or the TransactionNeed DU (line 11). These are public functions and types used throughout the codebase -- every data-modifying route flows through runCommandRouteAndAutoCompleteTransaction (InterfaceBridge/CommandRoute.fs lines 29-32) which delegates to runWithAutoCompleteTransaction, and every read route uses createNoTransaction via Context.create NoTransaction.

Other specs reference transactions as capabilities they depend on (REQ-JE-2.11: 'atomically in a single database transaction'; REQ-STG-8.2: 'database transaction that is rolled back'; REQ-RPT-2.6: 'No database transaction is required'), but the DAL spec never defines what the transaction subsystem must do.

runWithAutoCompleteTransaction has specific error-handling design decisions with no specification: (1) when the business function errors AND rollback errors, the rollback error takes precedence and the business error is lost (lines 96-98); (2) when the function throws an exception, the rollback result is silently ignored (line 104). These error-precedence choices affect debugging and error reporting.

Dan's statement identifies auto-commit transactions as load-bearing for data integrity: 'If one write fails and the other succeeds, and the calling route doesn't use an auto-commit transaction, our data will be in a bad state.' The resolved finding DAL-EFFICACY explicitly does not suppress spec-quality findings: 'This ruling covers test-efficacy only -- it does not suppress spec-quality, ambiguity, or contradiction audits against DataAccessLayer.md.'

**Action:** Add a section to DataAccessLayer.md (e.g. section 4, Transaction management) with requirements covering: transaction creation; commit and rollback; the auto-complete pattern (commit on function success, rollback on function error or exception); and the error-precedence behavior when both the business operation and the transaction operation fail.

**Why:** The transaction subsystem is the atomicity guarantee for every write operation in the system. Dan explicitly relies on it for data integrity. Without requirements, there is no contract for what the auto-complete mechanism must do, no specification of error-precedence behavior, and no basis against which to write dedicated transaction tests. A future developer modifying this code has no spec to validate against.

---
