using System.Net;
using System.Net.Http.Json;

namespace Ohmline.IntegrationTests;

/// <summary>Charge points over HTTP, through the node's shard regions, into PostgreSQL.</summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class ChargePointApiTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_registered_charge_point_when_reserved_then_it_reads_reserved()
    {
        var chargePoint = Chargers.Next();
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();

        var registered = await http.PostAsJsonAsync(
            "/charge-points", Chargers.Registration(chargePoint), Ct);
        var reserved = await http.PostAsJsonAsync(
            $"/charge-points/{chargePoint}/connectors/1/reservations",
            Chargers.Reservation(),
            Ct);

        registered.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        registered.Headers.Location.ShouldBe(
            new Uri($"/charge-points/{chargePoint}", UriKind.Relative));
        (await reserved.Content.ReadFromJsonAsync<Accepted>(Ct)).ShouldBe(new Accepted(2));
        var status = await http.GetFromJsonAsync<Status>($"/charge-points/{chargePoint}", Ct);
        status.ShouldNotBeNull().Version.ShouldBe(2);
        status.Connectors[0].State.ShouldBe("reserved");
    }

    [Fact]
    public async Task Given_one_idempotency_key_when_a_reservation_is_sent_twice_then_once()
    {
        var chargePoint = Chargers.Next();
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct);
        var key = Guid.CreateVersion7().ToString();

        var first = await ReserveAsync(http, chargePoint, key);
        var second = await ReserveAsync(http, chargePoint, key);

        second.ShouldBe(first);
        second.ShouldBe(new Accepted(2));
    }

    [Fact]
    public async Task Given_a_registration_with_invalid_fields_then_422_names_them()
    {
        await using var node = new OhmlineNode(postgres);

        var answer = await node.CreateClient().PostAsJsonAsync(
            "/charge-points",
            new
            {
                chargePointId = "OHM-0042",
                siteId = "slc-depot",
                connectorMaxPowersKw = new[] { 22m },
            },
            Ct);

        answer.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await answer.Content.ReadAsStringAsync(Ct)).ShouldContain("chargePointId");
    }

    internal static async Task<Accepted?> ReserveAsync(
        HttpClient http, string chargePoint, string key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/charge-points/{chargePoint}/connectors/1/reservations")
        {
            Content = JsonContent.Create(Chargers.Reservation()),
        };
        request.Headers.Add("Idempotency-Key", key);
        var answer = await http.SendAsync(request, Ct);
        answer.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return await answer.Content.ReadFromJsonAsync<Accepted>(Ct);
    }
}

internal sealed record Accepted(long Version);

internal sealed record Status(string ChargePointId, long Version, ConnectorState[] Connectors);

internal sealed record ConnectorState(int Number, string State, string? ReservedFor);
