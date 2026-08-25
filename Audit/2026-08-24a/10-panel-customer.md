# Hobson (Customer Audit)

## CUST-STG-DESC-1 — customer-gap
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs (fetchFiltered, line ~605)
- **Summary:** Stage entry fetch description filter uses exact match, making merchant triage during the weekly routine impractical.
- **Resolution:** fix-code

The `Ingestion FetchStageEntryFiltered` route filters on description using SQL `=` (exact match): `stage_entry_description = @stage_entry_description`. In contrast, the functionally analogous `Account FetchActivity` route filters the same concept using `LIKE` with wildcards: `je.description like @description` with `%%{descVal}%%` (AccountActivity.fs line ~205). The classification rule filtered fetch also uses `LIKE` for its name filter (ClassificationOrchestration.fs line ~128).

During the Saturday routine's Phase 2 (categorize unknowns), the primary workflow is triaging staged entries by merchant name fragments -- raw FI descriptions are long institution-specific strings like 'AMAZON.COM*2H9KX4Z3 AMZN.COM/BILL WA' or 'INGLES MARKET #42 CONCORD NC'. The operator needs to find all entries from a merchant without knowing the exact string. The filter value passes through JournalEntryDescription.create (which only validates non-empty and max 1000 chars), so a partial term like 'AMAZON' is accepted -- but then the exact-match query returns zero results because no description equals 'AMAZON' exactly.

The workaround is fetching ALL entries (filtered only by status) and filtering client-side, which defeats the purpose of the filtered query. Every other exact-match filter on the stage entry fetch (fi_reference, ingestion_source, source_file) targets fields where exact matching is appropriate -- those are identifiers, not free-text search targets. Description is the exception.

**Action:** Change the description filter in StageEntryOrchestration.fetchFiltered from exact match to LIKE with wildcards, matching the pattern already used by AccountActivity.fetchFiltered for the same domain type.

**Why:** This filter is the operator's primary tool for reviewing staged data during the weekly import cycle. With exact match, Phase 2 merchant triage requires either raw SQL against the staging tables or fetching entire result sets and filtering client-side -- both of which bypass the CLI and undermine the 'no direct DB writes for ledger/obligation state' policy's corollary that the CLI should be the primary query path too.

---
