using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.Diagnostics;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// The shape of whole traces, as the node's OpenTelemetry SDK exports them (section 17.8): a
/// request over HTTP, through a shard region, into an actor and its writes, all one trace; a
/// projection in a trace of its own, linked to the span that decided its event; and a journey
/// that tells other contexts' actors what to do, within the request's trace.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TraceShapeTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static int _next = Random.Shared.Next(1000, 9000);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_command_over_HTTP_then_one_trace_runs_through_the_actor_to_its_write()
    {
        await using var node = new TracedNode(postgres);
        var http = node.CreateClient();
        var trace = ActivityTraceId.CreateRandom();
        var chargePoint = NextChargePoint();

        var registered = await SendAsync(http, trace, "/charge-points", Registration(chargePoint));

        registered.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var spans = await SpansAsync(node, trace, "persisted ChargePointActor");
        var request = spans.Single(span => span.Kind == ActivityKind.Server);
        var process = spans.Single(span => span.DisplayName == "process ChargePointActor");
        var persist = spans.Single(span => span.DisplayName == "persist ChargePointRegistered");
        var apply = spans.Single(span => span.DisplayName == "apply ChargePointRegistered");
        var persisted = spans.Single(span => span.DisplayName == "persisted ChargePointActor");
        request.DisplayName.ShouldBe("POST /charge-points/"); // The group's route, "/".
        process.ParentSpanId.ShouldBe(request.SpanId);
        process.Kind.ShouldBe(ActivityKind.Consumer);
        process.Source.Name.ShouldBe(ActorShellDiagnostics.Name);
        new[] { persist, apply, persisted }
            .ShouldAllBe(span => span.ParentSpanId == process.SpanId);
        persist.GetTagItem("akka.persistence.id").ShouldBe($"charge-point-{chargePoint}");
        (await JournalRowAsync($"charge-point-{chargePoint}"))
            .ShouldBe((TraceEnvelopeSerializer.Id, TraceEnvelopeSerializer.EnvelopeManifest));
    }

    [Fact]
    public async Task Given_a_stored_event_then_a_projection_links_to_the_span_that_decided_it()
    {
        await using var node = new TracedNode(postgres);
        var http = node.CreateClient();
        var trace = ActivityTraceId.CreateRandom();

        await SendAsync(http, trace, "/charge-points", Registration(NextChargePoint()));

        var decided = (await SpansAsync(node, trace, "process ChargePointActor"))
            .Single(span => span.DisplayName == "process ChargePointActor");
        var projected = await LinkedAsync(node, decided);
        projected.Source.Name.ShouldBe(ChargingDiagnostics.Name);
        projected.Kind.ShouldBe(ActivityKind.Consumer);
        projected.DisplayName.ShouldStartWith("process ");
        projected.ParentSpanId.ShouldBe(default);
        projected.TraceId.ShouldNotBe(trace);
    }

    [Fact]
    public async Task Given_a_journey_then_what_it_tells_other_actors_stays_in_the_request_trace()
    {
        await using var node = new TracedNode(postgres);
        var http = node.CreateClient();
        var (connector, token) = await SetUpAsync(http);
        var trace = ActivityTraceId.CreateRandom();

        var started = await SendAsync(
            http, trace, "/journeys", new { tokenUid = token, connectorId = connector });

        started.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var journeyId = started.Headers.Location!.ToString()["/journeys/".Length..];
        var spans = await SpansAsync(node, trace, "process IdTokenActor");
        var request = spans.Single(span => span.Kind == ActivityKind.Server);
        var asked = spans.Single(span => span.DisplayName == "process ChargePointActor");
        var journey = spans.Single(span => span.DisplayName == "process ChargingJourneyActor");
        var persisted = spans.Single(span => span.DisplayName == "persisted ChargingJourneyActor");
        var told = spans.Single(span => span.DisplayName == "process IdTokenActor");
        asked.ParentSpanId.ShouldBe(request.SpanId);
        journey.ParentSpanId.ShouldBe(request.SpanId);
        persisted.ParentSpanId.ShouldBe(journey.SpanId);
        told.ParentSpanId.ShouldBe(persisted.SpanId);
        journey.GetTagItem("messaging.message.conversation_id").ShouldBe(journeyId);
    }

    /// <summary>The request, sent as if from a caller whose span is in the given trace.</summary>
    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient http, ActivityTraceId trace, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("traceparent", $"00-{trace}-{ActivitySpanId.CreateRandom()}-01");
        return await http.SendAsync(request, Ct);
    }

    /// <summary>The trace's spans, once one named <paramref name="last"/> has ended.</summary>
    internal static async Task<IReadOnlyList<Activity>> SpansAsync(
        TracedNode node, ActivityTraceId trace, string last)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var spans = node.Exported.Where(span => span.TraceId == trace).ToList();
            if (EndedWithItsAncestors(spans, last) || DateTimeOffset.UtcNow > deadline)
            {
                spans.ShouldContain(span => span.DisplayName == last);
                return spans;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    /// <summary>
    /// Whether the span named <paramref name="last"/> and every span above it, up to the request
    /// or the trace's root, have ended. A parent can end after its child: a span that tells
    /// another actor ends when its handler returns, and that actor may already be done.
    /// </summary>
    private static bool EndedWithItsAncestors(List<Activity> spans, string last)
    {
        var current = spans.FirstOrDefault(span => span.DisplayName == last);
        while (current is { Kind: not ActivityKind.Server } && current.ParentSpanId != default)
        {
            var parent = current.ParentSpanId;
            current = spans.FirstOrDefault(span => span.SpanId == parent);
        }

        return current is not null;
    }

    /// <summary>The first exported span with a link to <paramref name="span"/>.</summary>
    private static async Task<Activity> LinkedAsync(TracedNode node, Activity span)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var linked = node.Exported.FirstOrDefault(exported => exported.Links.Any(link =>
                link.Context.TraceId == span.TraceId && link.Context.SpanId == span.SpanId));
            if (linked is not null || DateTimeOffset.UtcNow > deadline)
            {
                return linked.ShouldNotBeNull();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    /// <summary>How the journal stored the persistence id's first event.</summary>
    private async Task<(int Serializer, string Manifest)> JournalRowAsync(string persistenceId)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var select = new NpgsqlCommand(
            "SELECT identifier, manifest FROM journal " +
            "WHERE persistence_id = @id ORDER BY sequence_number LIMIT 1",
            connection);
        select.Parameters.AddWithValue("id", persistenceId);
        await using var row = await select.ExecuteReaderAsync(Ct);
        (await row.ReadAsync(Ct)).ShouldBeTrue();
        return (row.GetInt32(0), row.GetString(1));
    }

    /// <summary>A charger, a site and a token of the test's own: the database is shared.</summary>
    internal static async Task<(string Connector, string Token)> SetUpAsync(HttpClient http)
    {
        var chargePoint = NextChargePoint();
        var site = $"trc-{chargePoint[^4..]}";
        var connector = $"{chargePoint}/1";
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..7]);
        var registration = new
        {
            chargePointId = chargePoint, siteId = site, connectorMaxPowersKw = new[] { 22m },
        };
        (await http.PostAsJsonAsync("/charge-points", registration, Ct)).EnsureSuccessStatusCode();
        var siteRequest = new
        {
            siteId = site,
            gridLimitKw = 200m,
            safetyMarginKw = 10m,
            timeZoneId = "America/Denver",
            connectors = new[]
            {
                new
                {
                    connectorId = connector, minPowerKw = 5m, maxPowerKw = 22m, priority = "public",
                },
            },
        };
        (await http.PostAsJsonAsync("/sites", siteRequest, Ct)).EnsureSuccessStatusCode();
        var card = new { tokenUid = token, driver = "drv-trc0001" };
        (await http.PostAsJsonAsync("/tokens", card, Ct)).EnsureSuccessStatusCode();
        return (connector, token);
    }

    private static object Registration(string chargePointId) => new
    {
        chargePointId,
        siteId = "slc-depot",
        connectorMaxPowersKw = new[] { 22m, 22m },
    };

    private static string NextChargePoint() =>
        $"OHM-TRC-{Interlocked.Increment(ref _next):0000}";
}
