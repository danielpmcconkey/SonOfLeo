#!/usr/bin/env bash
# Builds a throwaway sonofleo_test database on this machine's local PostgreSQL,
# for ephemeral environments (cloud sessions, containers) with no database of their own.
# Never point this at a real server: it drops and recreates sonofleo_test.
#
# Needs: PostgreSQL 16 installed locally, run as root (it starts the service and
# uses the postgres superuser over the local socket).
#
# Usage:  bash Tests/Tests.Integrated/setup-throwaway-test-db.sh
#         export SONOFLEO_TEST_CONNSTR="Host=localhost;Database=sonofleo_test;Username=sonofleo_test;Password=sonofleo_test"
#         dotnet test Tests/Tests.Integrated
set -euo pipefail
cd "$(dirname "$0")/../.."

pg_isready -q -h localhost || service postgresql start >/dev/null
for _ in {1..20}; do pg_isready -q -h localhost && break; sleep 0.5; done
pg_isready -q -h localhost || { echo 'PostgreSQL did not start.'; exit 1; }

psql_su() { su postgres -c "psql -v ON_ERROR_STOP=1 -q $*"; }

# Roles the migration scripts grant to. Local, throwaway credentials only.
for role in sonofleo_test sonofleo_migrator leobloom_hobson; do
    psql_su "-c \"do \\\$\\\$ begin
        if not exists (select from pg_roles where rolname = '$role') then
            create role $role login password '$role';
        end if; end \\\$\\\$\""
done

psql_su "-c 'drop database if exists sonofleo_test'"
psql_su "-c 'create database sonofleo_test owner sonofleo_test'"

# Every schema script after the one-off CreateDatabase script, in timestamp order.
for f in DbMigration/Scripts/*.sql; do
    [[ "$(basename "$f")" == *-CreateDatabase.sql ]] && continue
    sed 's/{ENV}/test/g' "$f" | su postgres -c "psql -v ON_ERROR_STOP=1 -q -d sonofleo_test" >/dev/null ||
        { echo "Failed applying $f"; exit 1; }
done

echo 'sonofleo_test is ready. Set:'
echo '  export SONOFLEO_TEST_CONNSTR="Host=localhost;Database=sonofleo_test;Username=sonofleo_test;Password=sonofleo_test"'
