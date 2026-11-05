using System.Net.Http.Json;
using Npgsql;
using Ohmline.JournalInspect;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The PostgreSQL counterpart of the SQLite journal tour (section 20.1): what the node stores
/// for a charge point, read with <c>tools/Ohmline.JournalInspect</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class JournalInspectTests(PostgresFixture postgres, ITestOutputHelper output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_two_accepted_commands_then_the_journal_has_two_rows_in_order()
    {
        var chargePoint = Chargers.Next();
        await using (var node = new OhmlineNode(postgres))
        {
            var http = node.CreateClient();
            await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct);
            await http.PostAsJsonAsync(
                $"/charge-points/{chargePoint}/connectors/1/reservations",
                Chargers.Reservation(),
                Ct);
        }

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        var rows = await new JournalReader(connection)
            .EventsOfAsync($"charge-point-{chargePoint}", Ct);
        foreach (var row in rows)
        {
            output.WriteLine(RowFormat.Line(row));
        }

        rows.Select(row => row.SequenceNr).ShouldBe([1L, 2L]);
        rows.Select(row => StoredPayload.Read(row.SerializerId, row.Manifest, row.Payload))
            .Select(payload => (payload.SerializerId, payload.Manifest))
            .ShouldBe(
            [
                (9101, "charging.charge-point-registered.v1"),
                (9101, "charging.connector-reserved.v1"),
            ]);
    }

    /// <summary>
    /// The same columns as on SQLite (section 10.8), in PostgreSQL's types: the payload is
    /// <c>bytea</c>, <c>created</c> is still .NET ticks in a <c>bigint</c>. From section 20.8
    /// the migrator makes the table from Akka.Persistence.Sql's published DDL, which has a
    /// <c>tags</c> column that tag-table mode leaves empty.
    /// </summary>
    [Fact]
    public async Task The_journal_table_has_the_tour_s_columns_in_postgres_types()
    {
        await using (var node = new OhmlineNode(postgres))
        {
            await node.CreateClient().PostAsJsonAsync(
                "/charge-points", Chargers.Registration(Chargers.Next()), Ct);
        }

        await using var dataSource = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var columns = dataSource.CreateCommand("""
            SELECT column_name, data_type FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'journal'
            ORDER BY ordinal_position
            """);
        var found = new List<(string Name, string Type)>();
        await using (var reader = await columns.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                found.Add((reader.GetString(0), reader.GetString(1)));
                output.WriteLine($"{reader.GetString(0)} {reader.GetString(1)}");
            }
        }

        found.ShouldBe(
        [
            ("ordering", "bigint"),
            ("deleted", "boolean"),
            ("persistence_id", "text"),
            ("sequence_number", "bigint"),
            ("created", "bigint"),
            ("tags", "text"),
            ("message", "bytea"),
            ("identifier", "integer"),
            ("manifest", "text"),
            ("writer_uuid", "text"),
        ]);
    }
}
