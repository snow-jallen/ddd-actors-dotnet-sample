using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Npgsql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Many read models from one stream, on the node as it runs (section 14.6): one session at a
/// listed charge point lands in the session history, the site live board and the driver app's
/// cards, with the name from the listing's current view, and the app reads its cards, its feed
/// and its pushes under <c>/app/v1</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class DriverAppTests(PostgresFixture postgres)
{
    private const string DepotBays = "Salt Lake bus depot, public bays";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_one_session_then_it_has_a_row_in_all_three_read_models()
    {
        await using var node = new OhmlineNode(postgres);
        var (_, _, session) = await ChargeAsync(node, node.CreateClient());

        var history = await RowAsync(
            "SELECT status FROM read_models.session_history WHERE session_id = @id", session);
        var board = await RowAsync(
            "SELECT status FROM read_models.site_live_board WHERE session_id = @id", session);
        var card = await RowAsync(
            "SELECT charge_point_display_name FROM read_models.driver_app_cards " +
            "WHERE session_id = @id",
            session);

        history.ShouldBe("charging");
        board.ShouldBe("occupied");
        card.ShouldBe(DepotBays);
    }

    [Fact]
    public async Task Given_the_driver_app_then_its_cards_and_its_feed_show_the_session()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var (_, token, session) = await ChargeAsync(node, http);

        var cards = await http.GetFromJsonAsync<Cards>($"/app/v1/tokens/{token}/cards", Ct);
        var feed = await http.GetFromJsonAsync<Feed>($"/app/v1/tokens/{token}/feed?after=0", Ct);
        var nothingNew = await http.GetFromJsonAsync<Feed>(
            $"/app/v1/tokens/{token}/feed?after={cards!.Position}", Ct);

        var active = cards.Active.ShouldNotBeNull();
        active.SessionId.ShouldBe(session);
        active.ChargePointDisplayName.ShouldBe(DepotBays);
        active.Version.ShouldBe(1);
        cards.Recent.ShouldBeEmpty();
        feed.ShouldNotBeNull().Entries.ShouldHaveSingleItem().Card.ShouldBe(active);
        feed.Position.ShouldBe(cards.Position);
        nothingNew.ShouldBe(new Feed([], cards.Position));
    }

    [Fact]
    public async Task Given_an_app_in_its_tokens_group_when_the_session_stops_then_it_hears_it()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var (_, token, session) = await ChargeAsync(node, http);
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(node.Server.BaseAddress, "/hubs/charging"), options =>
            {
                options.HttpMessageHandlerFactory = _ => node.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        var pushed = Channel.CreateUnbounded<Entry>();
        hub.On<Entry>("driverCardChanged", entry => pushed.Writer.TryWrite(entry));
        await hub.StartAsync(Ct);
        await hub.InvokeAsync("JoinToken", token.ToLowerInvariant(), Ct);

        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();
        var entry = await pushed.Reader.ReadAsync(Ct).AsTask().WaitAsync(Patience, Ct);
        var caughtUp = await http.GetFromJsonAsync<Feed>(
            $"/app/v1/tokens/{token}/feed?after=0", Ct);

        entry.Card.SessionId.ShouldBe(session);
        entry.Card.Status.ShouldBe("ended");
        entry.Card.Version.ShouldBe(2);
        caughtUp.ShouldNotBeNull().Entries[^1].ShouldBe(entry); // The push is a feed entry.
    }

    [Fact]
    public async Task Given_a_page_size_beyond_500_then_the_feed_says_it_is_not_valid()
    {
        await using var node = new OhmlineNode(postgres);

        var answer = await node.CreateClient()
            .GetAsync("/app/v1/tokens/04A2B3C4D5E6F7/feed?after=0&limit=501", Ct);

        answer.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await answer.Content.ReadAsStringAsync(Ct)).ShouldContain("limit");
    }

    /// <summary>
    /// A listed charge point of the test's own, a driver token of its own, a car plugged in
    /// and charging: the session is in its history and has a card when this returns.
    /// </summary>
    private Task<(string ChargePoint, string Token, Guid Session)> ChargeAsync(
        OhmlineNode node, HttpClient http) =>
        ChargeAsync(node, http, postgres);

    /// <summary>
    /// The same for another test class (section 19.7): a session charging, with its card in
    /// the driver app's read model.
    /// </summary>
    internal static async Task<(string ChargePoint, string Token, Guid Session)> ChargeAsync(
        OhmlineNode node, HttpClient http, PostgresFixture postgres)
    {
        var (chargePoint, session) = (Chargers.Next(), Guid.CreateVersion7());
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..7]);
        (await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct))
            .EnsureSuccessStatusCode();
        var listing =
            new { displayName = DepotBays, accessInstructions = "Level -2", openingHours = "24/7" };
        (await http.PutAsJsonAsync($"/charge-points/{chargePoint}/listing", listing, Ct))
            .EnsureSuccessStatusCode();
        await Chargers.PlugInAsync(node, chargePoint, session, token);
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        var started = await http.PostAsJsonAsync(
            "/sessions", Sessions.Start(session, $"{chargePoint}/1", token), Ct);
        started.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        // Each read model is a stream of its own, and on a node that has just started they may
        // all still be starting: longer than read-your-writes waits for the history.
        await RowAsync(
            postgres,
            "SELECT status FROM read_models.session_history WHERE session_id = @id",
            session);
        await RowAsync(
            postgres,
            "SELECT status FROM read_models.driver_app_cards WHERE session_id = @id",
            session);
        return (chargePoint, token, session);
    }

    /// <summary>The first column of the row the query finds, once a projection wrote it.</summary>
    private Task<string> RowAsync(string sql, object id) => RowAsync(postgres, sql, id);

    private static async Task<string> RowAsync(PostgresFixture postgres, string sql, object id)
    {
        await using var database = NpgsqlDataSource.Create(postgres.ConnectionString);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            try
            {
                await using var select = database.CreateCommand(sql);
                select.Parameters.AddWithValue("id", id);
                if (await select.ExecuteScalarAsync(Ct) is string value)
                {
                    return value;
                }
            }
            catch (PostgresException missing) when (missing.SqlState == "42P01")
            {
                // The projections have not created their tables yet.
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No row for {id}: {sql}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    private sealed record Card(
        Guid SessionId,
        string TokenUid,
        string Status,
        string ConnectorId,
        string? ChargePointDisplayName,
        decimal EnergyKwh,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        long Version);

    private sealed record Cards(Card? Active, List<Card> Recent, long Position);

    private sealed record Entry(long Position, Card Card);

    private sealed record Feed(List<Entry> Entries, long Position)
    {
        public bool Equals(Feed? other) =>
            other is not null && Position == other.Position && Entries.SequenceEqual(other.Entries);

        public override int GetHashCode() => Position.GetHashCode();
    }
}
