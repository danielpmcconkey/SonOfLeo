#!/usr/bin/env bash
# Enforces: investments and real estate are peers. A real-estate file may not open or name an
# investment module, and an investment file may not open or name a real-estate module.
# Only the ledger-account lookup in PositionsLedgerLinks and the types in PositionsComponent,
# PositionsError and PositionsAuditableAction are shared.
# Files in neither list (NetWorth, the report writers, converters and routes) combine the
# peers legitimately and are not checked. Every file in the Positions project must be in one of
# the lists or in the shared allowlist, so a new module cannot slip past the rule unlisted.
set -u
cd "$(dirname "$0")/.."

realEstate='Property Valuation RealEstateOrchestration'
investment='DimensionValue Security InvestmentAccount Holding AccountSnapshotHeader AccountSnapshotLine
DimensionValueOrchestration SecurityOrchestration InvestmentAccountOrchestration
HoldingOrchestration AccountSnapshotOrchestration HoldingsAsOf InvestmentWealthHistory
AccountSnapshotLot InvestmentActivity InvestmentActivityOrchestration UnitRollForward'

# Shared by both peers. PreLedgerBalance is here only until the D40 cash-and-debt positions
# slice moves it out of the Positions project.
shared='PositionsComponent PositionsError PositionsAuditableAction PreLedgerBalance'

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
    if ! grep -qw "$module" <<<"$realEstate $investment $shared"; then
        echo "$path: in neither the real-estate nor the investment list, nor shared"
        status=1
    fi
done

scan "$realEstate" "$investment"
scan "$investment" "$realEstate"

if [[ $status -ne 0 ]]; then
    echo 'Investments and real estate are peers: neither may open or name a module of the other, and every Positions module is listed as one or the other, or shared.'
fi
exit $status
