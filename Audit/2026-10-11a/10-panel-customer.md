# customer-hobson

## CUST-POS2-1 — customer-gap
- **Location:** Specs/Behavioral/Positions.md REQ-POS-12.6 (its 'Why a contribution may name no Security'), REQ-POS-14.2, REQ-POS-6.10, design note 'a snapshot is the whole account'; Src/Business.CrossDomainOrchestration/UnitRollForward.fs:69-76; Tests/Tests.Helpers/PositionsFixture.fs:400,412
- **Summary:** The spec has no consistent place for the cash held inside an investment account. If a parser records it as a Holding, the weekly roll-forward never comes out at zero. If a parser leaves it out, the snapshot and net worth miss that cash.
- **Resolution:** dan-decides

REQ-POS-12.6's rationale says 'money paid into an account's cash, rather than straight into a fund, moves no units of anything the account holds'. That treats account cash as something other than a Holding, and the fixture does the same ('Employer contribution to cash' and 'Interest on cash' name no Security, PositionsFixture.fs:400,412). But the design note says a snapshot is the whole account, and §1 and REQ-POS-6.10 name a money-market fund as a holding. A brokerage or HSA reports its settlement or core cash as exactly that: a position line with a quantity and a price of 1.00. A parser has two options, and both go wrong. (a) Record the core money-market fund as a Holding. Every Purchase, Sale, Fee, cash Contribution, Dividend, Interest and CapitalGainDistribution then moves its units, but no Activity names it, because institutions report the trade, not the settlement leg. UnitRollForward.fs:69-76 counts only Activities that name a holding and carry a quantity, so that fund's row shows a non-zero difference every week. REQ-POS-14.3 returns differences as data, so this noise sits beside, and hides, the real problems §14's preamble says the roll-forward exists to catch: an omitted holding, an unreported activity. (b) Leave the cash out. Then the snapshot's market value understates the account, and so do holdings-as-of (REQ-POS-8.1), net worth (REQ-RPT-8.2) and investment wealth history. The ledger cannot make up the difference, because the account's linked ledger account is excluded from net worth (REQ-RPT-8.2). Nothing in Positions §6, §12 or §14 says which option is intended. Each parser author will choose for themselves, and the weekly roll-forward check is only usable under one of the two.

**Action:** Dan decides how account cash is modelled. One option: it is a Holding (for example, a cash Security priced at 1.00), and §14 states that the roll-forward either excludes it or derives its settlement units from the money amounts of the other Activities. The other option: it is not a Holding, a snapshot carries the account's cash balance as its own reported figure, and REQ-RPT-8.2 counts it. Then amend REQ-POS-12.6's rationale and §14 to match.

**Why:** The roll-forward is the new weekly control Dan named in his statement. If it shows a difference on every taxable brokerage account every week, Hobson will learn to ignore it, and then it catches nothing. Leaving cash out instead makes the headline net worth figure wrong by the account's cash balance.

---

## CUST-POS2-2 — customer-gap
- **Location:** Specs/Behavioral/Positions.md §8 preamble, REQ-POS-8.1, REQ-POS-8.3, REQ-POS-6.10, REQ-POS-7.5; Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:190-205; Src/Ui.InterfaceBridge/InterfaceContracts/PositionsContracts.fs:188 (AccountSnapshotDateReturn)
- **Summary:** Holdings-as-of is the read the spec calls the planning engine's extract. It carries lots only from each account's latest snapshot, and REQ-POS-6.10 expects lots to be missing most weeks. So the extract usually has no lots, and no read shows which snapshot does have them.
- **Resolution:** dan-decides

§8 says holdings-as-of 'is the extract anything that plans from the data consumes'. REQ-POS-8.3 returns each line's Lots from the latest snapshot on or before the date, and HoldingsAsOf.fs:190-205 loads lots only for that snapshot's line ids. REQ-POS-6.10 says lots arrive 'not every week', and an empty list means 'not supplied'. Plan §6 also says not to carry lots forward. So on most dates, including the 'current financial state' extract that montecarlo-constraints-from-personalfinance.md C1 describes, every taxable line comes back with lots = []. To find the most recent lots, a consumer has to call AccountSnapshot ListDates (REQ-POS-7.5). Its return, AccountSnapshotDateReturn { snapshotDate; provenance }, does not say whether a snapshot carries lots. The consumer then has to call AccountSnapshot Fetch on each date, working backwards per account, until a line has lots. Dan's statement says lots are recorded 'so the simulator can work out real gains and long- vs short-term treatment'. The data is stored, but the read surface built for the simulator does not return it.

**Action:** Dan decides one of two options. (1) Amend REQ-POS-8.3 so each line also carries the Lots of the latest snapshot on or before the date that supplied lots for that Holding, together with that snapshot's date. That date shows how stale the lots are, and the lots are still not carried forward into storage. (2) Amend REQ-POS-7.5 so the snapshot-date listing says, for each date, whether any line carries lots. Then a consumer can find the right snapshot in one call.

**Why:** Lots exist so the simulator can compute real gains and long- vs short-term treatment from the current position. If the one extract the spec names for planning drops them on most weeks, every consumer has to build its own search backwards through snapshots, and C1's single as-of extract is no longer enough.

---

## CUST-POS2-3 — customer-gap
- **Location:** Specs/Behavioral/Positions.md design note 'activity is history, not a lot engine', REQ-POS-13.1, REQ-POS-13.2, REQ-POS-13.4, REQ-POS-13.5; DbMigration/Scripts/202610101010-CreateInvestmentActivityTable.sql
- **Summary:** The system does not keep the date ranges for which an account's activity was supplied. A period that was never supplied looks exactly like a period with no activity, so contributions-versus-growth comes out silently wrong for those periods.
- **Resolution:** dan-decides

REQ-POS-13.1 and 13.2 receive activity as a range per account: a begin date, an end date, and a replacement of everything in that range. The range itself is not stored. The only table is positions.investment_activity (account, date, ordinal, kind, ...). There is no record of covered ranges, REQ-POS-13.4's counts are returned once at write time, and REQ-POS-13.5 lists activities only. The design note says activity 'answers what the snapshots cannot: how much of an account's growth was money put in'. If Dan loads history for some years and not others, or a parser misses a month, a contributions total over that period reads as zero for the missing stretch, and the money put in shows up as market growth. The unit roll-forward does not catch all of this: a Contribution, Interest or Dividend paid into cash names no Security and moves no units (REQ-POS-12.6), so it never reaches §14. Elsewhere this slice is careful to keep 'not supplied' apart from 'none'. REQ-POS-6.10 says a line with no lots means not supplied, not no lots. REQ-RPT-8.7 says an account with no pre-ledger balance is 'unknown, not empty'. REQ-RPT-8.8 marks absent components so a figure 'must say so, not pass for complete'. Activity is the one new record type with no such distinction.

**Action:** Dan decides whether REQ-POS-13.2 should also keep each recorded range, merging ranges that overlap or touch, and whether REQ-POS-13.5's listing (or a new read) should return an account's covered ranges, or the gaps within a requested window. That would let a consumer tell 'no activity' from 'not supplied'.

**Why:** Contributions against growth is the main thing activity is for, and the retirement-planning engine needs it to separate savings rate from return. A missing stretch of history that reads as zero contributions overstates investment return without any warning. That is the kind of silent misstatement the pre-ledger and lots rules in this slice were written to prevent.

---

## CUST-POS2-4 — customer-gap
- **Location:** Specs/Behavioral/Positions.md REQ-POS-7.2, REQ-POS-7.3, REQ-POS-6.10; Src/Ui.InterfaceBridge/InterfaceContracts/PositionsContracts.fs:180 (RecordedAccountSnapshotReturn)
- **Summary:** Re-recording a date's snapshot without lots silently deletes the lots already recorded for that date. Under REQ-POS-6.10, though, a line with no lots means 'not supplied', not 'there are none'.
- **Resolution:** dan-decides

REQ-POS-7.2 replaces an existing snapshot 'entirely: its provenance, contribution basis and every line, with each line's Lots'. REQ-POS-6.10 says a line without Lots records that lots 'were not supplied for that line on that date. It does not mean the holding has no lots', and its rationale says institutions report lots separately from positions. Consider a normal Saturday. Hobson records an account's positions merged with that week's lot report. He then finds a misread price on one line and re-records the date from the positions export alone, using the usual weekly path, which has no lots. The replacement deletes every lot for that date. The re-sent snapshot made no claim about lots. It only did not supply them. REQ-POS-7.3's return, RecordedAccountSnapshotReturn { snapshot; replacedExisting }, shows replacedExisting = true and a snapshot without lots. Nothing says that lots were removed. A lot report can only be recovered by downloading it again on a later date. The lots for the original date are gone.

**Action:** Dan decides one of three options. (a) Keep the replace-entirely rule, but have REQ-POS-7.3's return say, per line, how many lots the replaced snapshot carried, so their removal is visible. (b) Amend REQ-POS-7.2 so a re-recorded line that supplies no Lots keeps the existing line's Lots when its quantity is unchanged (REQ-POS-6.12 still holds). (c) Confirm the current behaviour and record in REQ-POS-7.2 that a correction must re-supply the lots.

**Why:** Corrections by re-recording are routine (REQ-POS-7.2's rationale). The lot report is the one input Dan does not get every week, and the system silently discards it when a correction is made for an unrelated reason.

---

