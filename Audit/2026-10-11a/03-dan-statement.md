# Dan's Statement of Position

## last audit

At the end of last audit, we'd just implemented the first positions slice, on top of cash flow, data ingestions, and the standard ledger (accounts, fiscal periods, and journal entries). After that audit, we ruled on each point and have fixed everything we dispositioned as needing fixing.

## Positions slice 2

We just implemented Positions slice 2, which readies the data for my Monte Carlo simulation. It adds three things. First, the open tax lots each institution reports on a taxable holding, recorded verbatim, so the simulator can work out real gains and long- vs short-term treatment. Second, account activity (contributions, purchases, sales, dividends and so on) as the institution reported it, with a roll-forward that checks the units moved between two snapshots. Third, pre-ledger balances, so net worth reaches back before the ledger began, with guards that stop a new fiscal period or ledger link from stranding them. Nothing derives lots or cost from activity; that's the simulator's job. Every active requirement is tested; error cases that only reject loudly are untested by design. All UI routes and reports are wired in.
