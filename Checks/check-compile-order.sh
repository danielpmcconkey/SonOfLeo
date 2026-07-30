#!/usr/bin/env bash
# Enforces: the .fsproj <Compile Include> list is hand-maintained.
# This verifies MEMBERSHIP both ways — every .fs on disk is declared, every declared
# file exists. Compile ORDER itself is verified by `dotnet build`; this catches the
# file that was created on disk but never added, or declared but deleted.
set -u
cd "$(dirname "$0")/.."

status=0
for proj in Src/*/*.fsproj Tests/*/*.fsproj; do
    dir=$(dirname "$proj")
    declared=$(grep -o 'Compile Include="[^"]*"' "$proj" | sed 's/.*="//; s/"$//' | tr '\\' '/')
    ondisk=$(cd "$dir" && find . -name '*.fs' -not -path './obj/*' -not -path './bin/*' | sed 's|^\./||')

    for f in $ondisk; do
        if ! grep -qxF "$f" <<<"$declared"; then
            echo "$proj: $f exists on disk but has no <Compile Include> entry"
            status=1
        fi
    done
    for f in $declared; do
        if [[ ! -f "$dir/$f" ]]; then
            echo "$proj: <Compile Include> declares $f but it does not exist on disk"
            status=1
        fi
    done
done

exit $status
