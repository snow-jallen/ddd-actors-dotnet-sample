using System.Net.Http.Json;
using Npgsql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The node needs no right to change the schema (section 20.8): logged in as a member of the
/// role the migrator grants, <c>ohmline_app</c>, it writes events and snapshots, recovers, and
/// its projections keep their offsets, on tables it could never have created.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class LeastPrivilegeTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_node_logged_in_with_the_app_role_then_it_writes_and_projects()
    {
        var asNode = await LoginAsync();
        var chargePoint = Chargers.Next();

        await using var node = new OhmlineNode(postgres, asNode);
        var http = node.CreateClient();
        (await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync(
                $"/charge-points/{chargePoint}/connectors/1/reservations",
                Chargers.Reservation(),
                Ct))
            .EnsureSuccessStatusCode();

        var status = await http.GetFromJsonAsync<Status>($"/charge-points/{chargePoint}", Ct);
        status.ShouldNotBeNull().Connectors[0].State.ShouldBe("reserved");
        (await LiveBoardOffsetAsync()).ShouldBeGreaterThan(0);
    }

    /// <summary>A login of the node's own, in the role the migrator keeps up to date.</summary>
    private async Task<string> LoginAsync()
    {
        var login = $"node_{Guid.NewGuid():N}";
        await using var superuser = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = superuser.CreateCommand(
            $"CREATE ROLE {login} LOGIN PASSWORD 'node' IN ROLE ohmline_app"))
        {
            await create.ExecuteNonQueryAsync(Ct);
        }

        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Username = login,
            Password = "node",
        }.ConnectionString;
    }

    /// <summary>The site live board's offset on the charging tag, once it has moved.</summary>
    private async Task<long> LiveBoardOffsetAsync()
    {
        await using var database = NpgsqlDataSource.Create(postgres.ConnectionString);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            await using var read = database.CreateCommand("""
                SELECT last_offset FROM read_models.projection_offsets
                WHERE projection_id = 'site-live-board-v1' AND tag = 'charging'
                """);
            var offset = await read.ExecuteScalarAsync(Ct) as long? ?? 0;
            if (offset > 0 || DateTimeOffset.UtcNow > deadline)
            {
                return offset;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }
}
