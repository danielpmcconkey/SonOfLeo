#!/usr/bin/env bash
# Enforces the three groups inside Positions. Investments and real estate are peers: neither may
# open or name a module of the other. Balance accounts sit below real estate: a real-estate file
# may name a balance module (a Property's mortgage), but a balance file may name neither an
# investment nor a real-estate module, and an investment file may not name a balance module.
# Only the ledger-account lookup in PositionsLedgerLinks and the types in PositionsComponent,
# PositionsError and PositionsAuditableAction are shared.
# Files in no list (NetWorth, the report writers, converters and routes) combine the groups
# legitimately and are not checked. Every file in the Positions project must be in one of
# the lists or in the shared allowlist, so a new module cannot slip past the rule unlisted.
set -u
cd "$(dirname "$0")/.."

realEstate='Property Valuation RealEstateOrchestration'
investment='DimensionValue Security InvestmentAccount Holding AccountSnapshotHeader AccountSnapshotLine
DimensionValueOrchestration SecurityOrchestration InvestmentAccountOrchestration
HoldingOrchestration AccountSnapshotOrchestration HoldingsAsOf InvestmentWealthHistory
AccountSnapshotLot InvestmentActivity InvestmentActivityCoverage InvestmentActivityOrchestration UnitRollForward'
balance='BalanceAccount BalanceAccountBalance BalanceAccountOrchestration'

# Shared by both peers.
shared='PositionsComponent PositionsError PositionsAuditableAction'

positionsProject='Src/Business.FinancialServices.Positions'

dirs='Src/Business.FinancialServices.Positions Src/Business.CrossDomainOrchestration'

status=0
scan() {
    local files="$1" names="$2"
    local pattern
    pattern="(^\s*open\s+[A-Za-z0-9_.]*\.($(tr -s ' \n' '|' <<<"$names" | sed 's/|$//'))\s*$)|\b($(tr -s ' \n' '|' <<<"$names" | sed 's/|$//'))\."
    for f in $files; do
        for d in $dirs; do
            [[ -f "$d/$f.fs" ]] || continue
            hits=$(grep -nE "$pattern" "$d/$f.fs" | grep -vE '^[0-9]+:\s*//')
            if [[ -n "$hits" ]]; then
                sed "s|^|$d/$f.fs:|" <<<"$hits"
                status=1
            fi
        done
    done
}

for path in "$positionsProject"/*.fs; do
    module=$(basename "$path" .fs)
    if ! grep -qw "$module" <<<"$realEstate $investment $balance $shared"; then
        echo "$path: in none of the real-estate, investment and balance lists, nor shared"
        status=1
    fi
done

scan "$realEstate" "$investment"
scan "$investment" "$realEstate $balance"
scan "$balance" "$investment $realEstate"

if [[ $status -ne 0 ]]; then
    echo 'Investments and real estate are peers: neither may open or name a module of the other. Real estate may name a balance module; balance modules name neither peer, and investments name no balance module. Every Positions module is listed in one group, or shared.'
fi
exit $status
