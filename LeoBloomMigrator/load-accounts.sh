#!/usr/bin/env bash
# Feeds each account JSON in the drop dir, in filename order, to the SonOfLeo prod CLI
# as `Account Create`. Stops at the first failure: files are ordered parents-first, so a
# failed parent would cascade into its children.
set -euo pipefail

DROP_DIR="${1:-/mnt/media/BusinessRecords/LeoBloomOps/SonOfLeoPlanning/InsertsFromLeo}"
CLI="/media/dan/fdrive/codeprojects/SonOfLeo/Src/Ui.OperatorCli/bin/Release/net10.0/Ui.OperatorCli"

[[ -x "$CLI" ]] || { echo "CLI not found: $CLI" >&2; exit 1; }
[[ -n "${SONOFLEO_PROD_CONNSTR:-}" ]] || { echo "SONOFLEO_PROD_CONNSTR is not set" >&2; exit 1; }

shopt -s nullglob
files=("$DROP_DIR"/account-*.json)
(( ${#files[@]} )) || { echo "No account-*.json in $DROP_DIR" >&2; exit 1; }

echo "Loading ${#files[@]} accounts from $DROP_DIR"
count=0
for f in "${files[@]}"; do
    name="$(basename "$f")"
    if out="$("$CLI" Account Create < "$f" 2>&1)"; then
        count=$((count + 1))
        echo "OK   $name"
    else
        echo "FAIL $name" >&2
        echo "$out" >&2
        echo "Stopped after $count of ${#files[@]}. Fix $name and rerun from it (already-created accounts will fail as duplicates)." >&2
        exit 1
    fi
done
echo "Done: $count accounts created."
