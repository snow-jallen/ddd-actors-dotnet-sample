using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using static Ohmline.Migrator.Tests.PostgresFixture;

namespace Ohmline.Migrator.Tests;

/// <summary>
/// Owning the journal schema (section 20.8): the migrator makes Ohmline's tables in an empty
/// database, records each script once, adopts tables the node made itself before there was a
/// migrator, and leaves the applications a role that may change rows but not the schema.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_empty_database_when_migrated_then_every_table_is_there()
    {
        var database = postgres.NewDatabase();

        var result = Migrations.Apply(MigrationTarget.Ohmline, database, NullLogger.Instance);

        result.Successful.ShouldBeTrue();
        (await ColumnAsync(database, Tables, Ct)).ShouldBe(
        [
            "public.journal",
            "public.schemaversions",
            "public.snapshot",
            "public.tags",
            "read_models.driver_app_cards",
            "read_models.driver_app_feed",
            "read_models.integration_outbox",
            "read_models.integration_relay_sessions",
            "read_models.projection_failures",
            "read_models.projection_offsets",
            "read_models.session_history",
            "read_models.site_live_board",
        ]);
        (await ColumnAsync(database, Applied, Ct)).ShouldBe(
        [
            "Ohmline.Migrator.Scripts.Ohmline.0001_akka_journal.sql",
            "Ohmline.Migrator.Scripts.Ohmline.0002_akka_snapshot.sql",
            "Ohmline.Migrator.Scripts.Ohmline.0003_tags.sql",
            "Ohmline.Migrator.Scripts.Ohmline.0004_read_models.sql",
        ]);
    }

    [Fact]
    public void Given_a_migrated_database_when_migrated_again_then_only_the_grants_run()
    {
        var database = postgres.NewDatabase();
        Migrations.Apply(MigrationTarget.Ohmline, database, NullLogger.Instance);

        var again = Migrations.Apply(MigrationTarget.Ohmline, database, NullLogger.Instance);

        again.Successful.ShouldBeTrue();
        again.Scripts.Select(script => script.Name)
            .ShouldBe(["Ohmline.Migrator.Grants.ohmline.sql"]);
        Migrations.Pending(MigrationTarget.Ohmline, database)
            .ShouldBe(["Ohmline.Migrator.Grants.ohmline.sql"]);
    }

    /// <summary>
    /// A deployment that ran before section 20.8 already has a journal, made by autoInitialize,
    /// full of events. The first migration adopts it: nothing is dropped or rewritten.
    /// </summary>
    [Fact]
    public async Task Given_a_journal_the_node_made_itself_when_migrated_then_its_rows_stay()
    {
        var database = postgres.NewDatabase();
        await ExecuteAsync(
            new NpgsqlConnectionStringBuilder(database) { Database = "postgres" }.ConnectionString,
            $"CREATE DATABASE {new NpgsqlConnectionStringBuilder(database).Database}",
            Ct);
        await ExecuteAsync(database, AutoInitializedJournal, Ct);

        var result = Migrations.Apply(MigrationTarget.Ohmline, database, NullLogger.Instance);

        result.Successful.ShouldBeTrue();
        (await ColumnAsync(database, "SELECT persistence_id FROM journal", Ct))
            .ShouldBe(["charge-point-OHM-SLC-0042"]);
    }

    [Fact]
    public async Task Given_the_node_s_role_then_it_may_write_rows_but_not_change_the_schema()
    {
        var database = postgres.NewDatabase();
        Migrations.Apply(MigrationTarget.Ohmline, database, NullLogger.Instance);
        var login = $"node_{Guid.NewGuid():N}";
        await ExecuteAsync(
            database, $"CREATE ROLE {login} LOGIN PASSWORD 'node' IN ROLE ohmline_app", Ct);
        var asNode = new NpgsqlConnectionStringBuilder(database)
        {
            Username = login,
            Password = "node",
        }.ConnectionString;

        await ExecuteAsync(asNode, """
            INSERT INTO read_models.projection_offsets (projection_id, tag, last_offset)
            VALUES ('session-history-v1', 'charging-session', 0)
            """, Ct);
        var refused = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync(asNode, "CREATE TABLE public.mistake (id int)", Ct));

        refused.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Given_billing_s_database_when_migrated_then_billing_s_tables_are_there()
    {
        var database = postgres.NewDatabase();

        Migrations.Apply(MigrationTarget.Billing, database, NullLogger.Instance)
            .Successful.ShouldBeTrue();

        (await ColumnAsync(database, Tables, Ct)).ShouldBe(
        [
            "billing.holds",
            "billing.inbox_position",
            "billing.invoices",
            "billing.outbox",
            "public.schemaversions",
        ]);
    }

    private const string Tables = """
        SELECT table_schema || '.' || table_name FROM information_schema.tables
        WHERE table_schema IN ('public', 'read_models', 'billing')
        ORDER BY 1
        """;

    private const string Applied = """
        SELECT scriptname FROM public.schemaversions ORDER BY schemaversionsid
        """;

    /// <summary>The journal as Akka.Persistence.Sql's autoInitialize made it before 20.8.</summary>
    private const string AutoInitializedJournal = """
        CREATE TABLE public.journal (
            ordering bigserial PRIMARY KEY, created bigint NOT NULL, deleted boolean NOT NULL,
            persistence_id text NOT NULL, sequence_number bigint NOT NULL, message bytea NOT NULL,
            manifest text, identifier integer, writer_uuid text);
        INSERT INTO public.journal (created, deleted, persistence_id, sequence_number, message)
        VALUES (0, false, 'charge-point-OHM-SLC-0042', 1, '\x7b7d');
        """;
}
