Tests in this project require a live database connection and appsettings.Development.json.

In an environment with no database of its own (a cloud session, a container),
`setup-throwaway-test-db.sh` builds a local `sonofleo_test` from `DbMigration/Scripts/`
and prints the `SONOFLEO_TEST_CONNSTR` to export. It drops and recreates the database,
so run it only against a local, disposable PostgreSQL.
