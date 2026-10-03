# Dan's Statement of Position

# where we are

## last audit

At the end of last audit, we'd just implemented data ingestions on top of the standard ledger (accounts, fiscal periods, and journal entries).

## cash flow
We have since implemented a major slice of functionality called cash flow. This roughly approximates the "obligations" functionality from the original LeoBloom app. It tracks master agreements, payment agreements, instances of those payment agreements, invoices, and payments. Classification rules link staged entry lines to payment agreements, and matching then turns those links into Payments against open invoices. A Payment is created pointing at its staged line, and gains its journal entry line linkage when the staged entry eventually posts.

This forced us to re-think the relationship between classification and the rest of the business functions. Prior to this slice, classification was part of the data ingestion domain. It now stands as its own domain, servicing both data ingestions and cash flow. I also refactored a few minor capabilities. For example, ActivityPeriod was previously part of the Account domain. It's now been moved into a general domain to serve as a component for both Accounts and cash flow agreements.

## saturday readiness

This slice also forced us to really think through the future saturday process. We planned out a future state machine that would run smaller bits of the saturday routine. So that caused us to split out the old monolithic function that staged FI exports, deduplicated them, and classified them in one pass. We created new pre-posting process (and new reports to support it).

## refactor

There were multiple refactors during the course of the cash-flow slice. I landed on a much more structured hierarchy of application tiers, split out into their separate fsproj. This was to make it more difficult for agent developers to put components in the wrong places. This hierarchy is elaborated on in Architecture/SonOfLeo.archimate (new to this slice) as well as multiple architecture principles. Additionally I broke out the monolithic error and auditable actions into domain-specific implementations of an interface. This was to further my principle that no lower tier should have any insight into an upper tier's domain.

## DB migrator

We wrote a database migration tool to make it easier to manage DML/DDL across environments. As part of this, we consolidated old sql scripts to make it easier to see "already settled" domain structures. We also used this as an opportunity to change the ownership of database tables and schemas to the migrator and disallow the standard application users any create or alter abilities. 

## dev process

All of the refactoring was in support of a transition to fully agentic development. Dan no longer writes code in this app. Nor does Dan review the code unless there's a strong reason to do so. All dev (Src and Tests) are done by Opus 5.5 now and this audit is Dan's primary back-stop. Hobson also reviews code as needed and we have about 1600 tests to check against malfunctioning code. But this audit is what gives me the assurance to maintain a fully agentic workflow
