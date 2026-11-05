using System.Net.Http.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Client.Contracts.Requests;

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

    /// <summary>
    /// The driver app's stop, sent again after the node restarted (section 19.3): the app never
    /// heard the first answer, so it sends the same command. The session recovers the command's
    /// id with its events and answers with the version of the first stop, recording nothing.
    /// </summary>
    [Fact]
    public async Task Given_a_stop_sent_again_after_a_restart_then_the_same_version()
    {
        var json = ChargingClientJsonContext.Default;
        var session = Guid.CreateVersion7();
        var stop = new StopChargingRequest(Guid.CreateVersion7());
        var route = $"/app/v1/sessions/{session}/stop";
        CommandAcceptedDto? first;
        await using (var before = new OhmlineNode(postgres))
        {
            var http = before.CreateClient();
            await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct);
            (await http.PostAsJsonAsync("/sessions", Sessions.Start(session), Ct))
                .EnsureSuccessStatusCode();
            var stopped = await http.PostAsJsonAsync(route, stop, json.StopChargingRequest, Ct);
            first = await stopped.Content.ReadFromJsonAsync(json.CommandAcceptedDto, Ct);
        }

        await using var after = new OhmlineNode(postgres);
        var again = await after.CreateClient()
            .PostAsJsonAsync(route, stop, json.StopChargingRequest, Ct);

        first.ShouldBe(new CommandAcceptedDto(stop.CommandId, 2));
        (await again.Content.ReadFromJsonAsync(json.CommandAcceptedDto, Ct)).ShouldBe(first);
    }
}
