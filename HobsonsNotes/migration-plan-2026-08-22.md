# Migration Plan — LeoBloom → SonOfLeo — 2026-08-22

Dan's outline for how the cutover happens. Not a spec — a plan to
refine when the time comes.

## Sequence

1. **Moratorium on new data in LeoBloom.** No new JEs, no imports, no
   obligation transitions. The ledger freezes.

2. **SQL dump of the Chart of Accounts from LeoBloom.** Hobson parses
   the results and uses the SonOfLeo CLI to create each account — the
   first real exercise of those CLI features.

3. **Take trial balances from LeoBloom** at each month-end from January
   2026 through whichever EOM the migration starts. These are the
   comparison targets.

4. **Hobson reviews his import scripts and creates classification rules
   in SonOfLeo.** Not a mechanical copy — the classification engine is
   redesigned (structured FieldMatch DU, priority-ranked, JSONB). This
   is real translation work, worth its own session.

5. **Hobson archives the OG importer scripts.** Kept for posterity and
   rollback ("oh shit" moment), not deleted.

6. **Hobson writes new importer scripts** that convert the data Dan
   drops into the import data directory into SonOfLeo's JSONL raw
   staging format. These are the bespoke parsers referenced in the
   wakeup — the bridge between Dan's source files and the base staging
   format.

7. **Run January's data through the new pipeline.** New parsers →
   SonOfLeo raw ingestion → classification → posting.

8. **Take a trial balance from SonOfLeo's January.** Compare against
   LeoBloom's January trial balance. Classification rules have changed,
   so it won't match perfectly — but the comparison tells us what's
   working vs what isn't.

9. **Advance to February when January is clean.** Repeat through each
   month until caught up with LeoBloom.

10. **Run one month side-by-side** (both systems processing the same
    live data). Insurance — may not be necessary if the catch-up months
    are clean.

11. **Cut over.** LeoBloom retires for ledger/obligation operations.
    Portfolio stays in `leobloom_prod` temporarily per the migration
    roadmap.

## Things the plan doesn't cover yet

- **Obligations.** The nine agreements and their instance history are a
  separate data structure. Probably set up fresh in SonOfLeo rather than
  migrated — historical instance linkage isn't worth preserving if JE
  IDs change. Design session needed.

- **COA renumbering.** The trial balance sorts by account code, but some
  children sort before their parents (e.g. 5280 under 5300). Should
  happen before migration, not after — renumber in LeoBloom, then
  migrate the clean numbering.

- **Merchant rules.** LeoBloom's `stage.merchant_rules` map to
  SonOfLeo's classification rules but the shape is different. Covered
  by step 4 but deserves explicit attention.

- **Portfolio.** Stays in `leobloom_prod` per the roadmap. Net worth
  report bridges both databases during transition. Migrates later.

## Dependencies

- Data-ingestion audit remediation complete
- CashFlow/obligations domain built in SonOfLeo
- COA renumbering done
- Bespoke parsers written (step 6)

---

*Note to self. No work starts from this without Dan's go.*
