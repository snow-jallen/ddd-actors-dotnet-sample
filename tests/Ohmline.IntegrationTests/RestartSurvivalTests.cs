using System.Net.Http.Json;

namespace Ohmline.IntegrationTests;

/// <summary>
/// State survives a restart: a node is disposed, which runs coordinated shutdown, and a new
/// node on the same database recovers every aggregate from the journal when it is next asked.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class RestartSurvivalTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_reservation_when_the_node_restarts_then_it_is_still_there()
    {
        var chargePoint = Chargers.Next();
        await using (var before = new OhmlineNode(postgres))
        {
            var http = before.CreateClient();
            await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct);
            await http.PostAsJsonAsync(
                $"/charge-points/{chargePoint}/connectors/1/reservations",
                Chargers.Reservation(),
                Ct);
        }

        await using var after = new OhmlineNode(postgres);
        var status = await after.CreateClient()
            .GetFromJsonAsync<Status>($"/charge-points/{chargePoint}", Ct);

        status.ShouldNotBeNull().Version.ShouldBe(2);
        status.Connectors[0].State.ShouldBe("reserved");
        status.Connectors[0].ReservedFor.ShouldBe("04A2B3C4D5E6F7");
    }

    [Fact]
    public async Task Given_a_retry_after_a_restart_then_the_journal_has_its_first_answer()
    {
        var chargePoint = Chargers.Next();
        var key = Guid.CreateVersion7().ToString();
        Accepted? first;
        await using (var before = new OhmlineNode(postgres))
        {
            var http = before.CreateClient();
            await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct);
            first = await ChargePointApiTests.ReserveAsync(http, chargePoint, key);
        }

        await using var after = new OhmlineNode(postgres);
        var retried =
            await ChargePointApiTests.ReserveAsync(after.CreateClient(), chargePoint, key);

        retried.ShouldBe(first);
    }
}
