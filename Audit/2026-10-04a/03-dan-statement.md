# Dan's Statement of Position

## last audit

At the end of last audit, we'd just implemented the cash flow slice, which was on top of data ingestions and the standard ledger (accounts, fiscal periods, and journal entries). After that audit, we ruled on each point and have fixed everything we dispositioned as needing fixing.

## Positions

we just implemented the Positions slice 1, which is designed to track my investment positions (both real estate and securities). This slice implements all the CRUD operations for dealing with such data as well as new reports for me to view my overall financial health.

## Person

Additionally, to track account owners, we needed to add a Person type and associated components and crud functions. It is important to distinguish a Person (a human with a relation to an account) and a User (an actor in this system)

## UI routes

Every operation has its own CLI route, so this data can be entered and reported on from the command line. The parsers that turn institution downloads into snapshot payloads, and the import of historical data, live outside this repo and aren't part of the slice

## Excluded from slice 1 (deliberately)

Positions are recorded as whole-account snapshots of what each institution reported, exactly as reported. Purchases, sales, tax lots and realised gains are a later slice; only each holding's basis method is recorded now, because that slice depends on it.

## Architecture

The positions domain sits above cash flow in the hierarchy, but below classification. It should have its own Business fsproj and its own schema. The Person type was defined in Business.General as the concept is more general than the account owner of positions domain entities. (we don't track ledger account owners, but we may want to someday).

Inside of the positions domain, investments and real estate are unordered peers. Neither may reference the other, and net worth is the orchestration that combines them.

Quantity and Price are new exact-decimal types (supporting 6-decimal precision) that sit alongside Money and are never Money.

## Json

One change outside the slice: App.Utility.Json.fromJson, which every route shares, now names the field a payload actually left out.
