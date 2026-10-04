# test-efficacy-ClassificationRuleCrud

## EFF-CR-5.3-SRC — test-gap
- **Location:** REQ-CR-5.3; Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:401-421 and :814-842; Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsClassification.fs:328-344
- **Summary:** REQ-CR-5.3 limits the source-pattern filter to the text of Source field matches anywhere in the rule, but no test checks either half: that a rule whose Description pattern contains the fragment is left out, or that a Source match in chainTwo is found.
- **Resolution:** fix-test

REQ-CR-5.3 says the source filter is a "case-sensitive partial match against the text of any `Source` field match in the rule". The code implements this as ClassificationRule.sourcePatternLikePredicate (Src/Business.FinancialServices.Classification/ClassificationRule.fs:80-92). It has two parts: `fm.value ->> 'field' = 'Source'` restricts the search to Source matches, and `chainOne || COALESCE(chainTwo, '[]')` searches every chain of every group. Only three tests set `sourceLike`, and none of them reaches either part:
(1) ClassificationRuleCrud.fs:401 searches "TestSplitBank". Its expected set comes from rule names containing that text (line 408-411). No fixture rule has a Description pattern containing "TestSplitBank" (Tests.Helpers/TestDataStage.fs:770-940).
(2) ClassificationRuleCrud.fs:814, the SYS-1.4 source case. Both the containing rule and the decoy carry their text in a Source match in chainOne (lines 818-826).
(3) RevisedRequirementsClassification.fs:328. The marker sits only in a Source match in chainOne (lines 333-336).
Two mutations would therefore keep every test green:
- Dropping the `field = 'Source'` clause, so the filter matches Description patterns too. No test has a rule whose Description pattern contains the searched fragment.
- Dropping the chainTwo concatenation, so Source matches in a secondary chain are never searched. No test puts a Source match in chainTwo and searches for it.
The filter is cited, but only the "partial match on Source text in the first chain" part is exercised.

**Action:** Add one REQ-CR-5.3 source-filter test that creates three rules with a fresh tag: (a) the tag in a Source match in chainOne, (b) the tag in a Description match only, (c) the tag in a Source match in chainTwo of an Or group. Assert the filter returns exactly {a, c}.

**Why:** Smell test: if the predicate searched every pattern in the rule body, or only chainOne, the function would return well-shaped results that are wrong and every current test would still pass. A requirement that names the field ("any `Source` field match") is only covered when a test includes a decoy that differs from a match in nothing but the field, plus a match the shortcut would miss.

---

## EFF-CR-1.22-DAL — test-gap
- **Location:** REQ-CR-1.22; Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:180-217 (line 198) and :220-260 (line 246)
- **Summary:** Both REQ-CR-1.22 tests accept DalErrorDuringNonQueryExecution, which wraps any exception from any non-query statement, so a rename or insert that fails for an unrelated reason also passes as a name-uniqueness rejection.
- **Resolution:** fix-test

The two duplicate-name tests assert the failure with `isCorrectError r DalErrorDuringNonQueryExecution None` (lines 198 and 246). DalErrorDuringNonQueryExecution is `of exn` (Src/App.DataAccessLayer/DalError.fs:26), and ExecuteNonQuery.fs:38 raises it for any exception: a unique violation, a FK violation, a typo in a column name, a closed connection. isCorrectError only compares DomainName and CaseName (Tests.Helpers/SadPath.fs:60-68). It never looks at the wrapped exception, so the test never confirms that `classification_rule_rule_name_key` (DbMigration/Scripts/202609071135-CreateClassificationTables.sql:15) caused the failure. This is the Specimen 4 shape: the error arm is typed in form, but the case is a catch-all.
The follow-up assertions do not close the gap. A failing update test shows it: if the UPDATE statement broke whenever `classificationRuleNameUpdate` is SetTo (for example a bad column in the SET clause), the update would return DalErrorDuringNonQueryExecution, the incumbent would still be the only holder of the name (line 253-254), and the subject would keep its old name (line 256). Every assertion passes, yet uniqueness was never tested. The create test has the same weakness: any insert failure leaves the incumbent the sole holder (lines 207-213).
The REQ-CR-6.1 rename test elsewhere happens to catch a broken rename. Within these tests, though, the uniqueness rule is not what decides the verdict. REQ-SYS-2.1.2 does allow this rejection to fall through to a database constraint. That allows the DAL error, but it does not let the test leave unchecked which constraint fired.

**Action:** In both REQ-CR-1.22 tests, replace isCorrectError with a typed match on `Error (AsError (DalErrorDuringNonQueryExecution ex))`. Then assert the inner exception is a Npgsql.PostgresException with SqlState "23505" and ConstraintName "classification_rule_rule_name_key". Keep the Wrong-error and Ok escape arms.

**Why:** A typed error is only as precise as the case it names. When the case wraps an arbitrary exception, the test has to inspect what is wrapped, or it passes on any database failure, the same leak Specimen 4 records for raw DAL errors.

---

