# spec-audit-data-ingestion

## AMB-STG-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md, REQ-STG-5.6 / REQ-STG-5.7
- **Summary:** No precedence rule when classification produces both NoMatch and Conflict on different lines of the same entry.
- **Resolution:** fix-spec

REQ-STG-5.6 says: 'When multiple rules match with equal priority for a line with null account_code, the staged entry's status is set to Conflict.' REQ-STG-5.7 says: 'When no rule matches a line with null account_code, the staged entry's status is set to NoMatch.' Both requirements set the entry-level status from per-line conditions. For a multi-line entry where line A has no match and line B has a tied match, both requirements apply simultaneously and prescribe different statuses. The transition table in REQ-STG-4.6 confirms both transitions are possible from Ingested ('at least one line has no rule match' and 'at least one line has multiple rule matches at equal priority') but an entry can only hold one status. The code resolves this with an explicit precedence (Conflict wins over NoMatch, per StageEntryOrchestration.fs lines 331-334), but the spec text provides no such precedence rule.

**Action:** Add a precedence statement to section 5, e.g. after REQ-STG-5.7: 'When classification of a single entry produces both Conflict and NoMatch outcomes on different lines, Conflict takes precedence.' Alternatively, a new sub-requirement under 5.6 or 5.7 that explicitly states the evaluation order.

**Why:** Two reasonable developers reading only the spec would implement different status outcomes for this scenario. One might prioritize Conflict (matches exist but are ambiguous), another might prioritize NoMatch (no matches at all). The code has already picked a side, but the spec should declare it so the test suite can verify the intended behavior rather than the incidental implementation.

---

## IE-STG-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/DataIngestion.md, REQ-STG-5.5
- **Summary:** REQ-STG-5.5 omits recording classification_rule_id on the staged line, unlike the parallel REQ-STG-5.4.
- **Resolution:** fix-spec

REQ-STG-5.4 prescribes two actions for the single-match case: 'the classifier assigns the rule's account code to the line and records the classification_rule_id on the staged line.' REQ-STG-5.5 prescribes only one action for the clear-winner case: 'the classifier assigns the highest-priority rule's account code.' The recording of classification_rule_id is not mentioned. REQ-STG-2.16 defines the field's purpose ('identifies the classification rule that assigned the account_code'), and the code correctly records the rule ID in both cases (ClassificationOrchestration.fs updateLineWithMatch is called for both OneMatch and ManyMatchesClearWinner at lines 186-187). The spec's asymmetry between two parallel requirements describing the same update operation is an omission.

**Action:** Amend REQ-STG-5.5 to: 'When multiple rules match and one has strictly higher priority, the classifier assigns the highest-priority rule's account code to the line and records the classification_rule_id on the staged line.'

**Why:** Parallel requirements that describe the same operation (assign winning rule's code to a line) should prescribe the same set of actions. A developer reading 5.5 in isolation sees no instruction to record classification_rule_id, whereas 5.4 explicitly includes it. While most developers would infer the intent from REQ-STG-2.16 and the 5.4 pattern, the inconsistency is a spec quality issue that could silently drop diagnostic data if taken literally.

---

## CON-STG-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md, REQ-STG-9.4
- **Summary:** REQ-STG-9.4 claims staged line account codes are FK-constrained against the chart of accounts, but the schema intentionally has no such FK.
- **Resolution:** fix-spec

REQ-STG-9.4 states: 'Invalid non-null codes cannot occur: the chart of accounts is FK-constrained.' The migration DbMigrations/202608081415-CreateStageSchemaAndTables.sql defines ingestion.staged_entry_line.code as varchar(10) with NO FK constraint and an explicit comment: 'note this intentionally doesn't enforce the reference as account codes can change over time and they aren't the primary key' (line 59). The schema design deliberately omits the FK because account codes are mutable (not PKs). The classification_rule.code_at_match column DOES have a FK to ledger.account.code (per 202608110820-ModifyClassificationRule.sql line 13), which prevents classifier-assigned codes from becoming stale. But parser-assigned codes enter staged_entry_line.code directly via REQ-STG-3.7 application-level validation with no FK guarantee. The spec's blanket FK claim does not hold for parser-assigned codes and contradicts the schema's documented design intent.

**Action:** Revise the last sentence of REQ-STG-9.4 to accurately describe the actual guards. For example: 'Invalid non-null codes are prevented by ingestion-time validation (REQ-STG-3.7) for parser-assigned codes and by the classification rule's FK constraint for classifier-assigned codes. An account code that becomes invalid between ingestion and posting would fail resolution at this step.'

**Why:** The spec makes a verifiably false architectural claim. A developer reading 'cannot occur: FK-constrained' might reasonably skip error handling for invalid non-null codes at posting time. The actual guard is application-level validation at ingestion (REQ-STG-3.7), not a DB constraint on the staged line table. If an account code were to become invalid between ingestion and posting (e.g., account deactivated or code changed), the system needs to handle it rather than assume it away.

---

