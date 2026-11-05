using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Mobile.Core.Diagnostics;
using Ohmline.Mobile.Core.Outbox;
using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// One trace from a tap on the phone to the journal (section 19.8): the driver app's own
/// outbox and dispatcher send a start to a node in memory, and the node's OpenTelemetry SDK,
/// which listens to every <c>Ohmline.*</c> source in the process, exports the app's spans with
/// the server's. A send in the process that saw the tap is the tap's child; a send after the
/// app restarted is a trace of its own, linked to the tap.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TapToJournalTraceTests(PostgresFixture postgres, ITestOutputHelper output)
    : IDisposable
{
    private readonly string _database =
        Path.Combine(Path.GetTempPath(), $"ohmline-driver-trace-{Guid.NewGuid():N}.db3");

    private string ConnectionString => $"Data Source={_database}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_tap_then_one_trace_runs_from_the_tap_to_the_journeys_write()
    {
        await using var node = new TracedNode(postgres);
        var outbox = new SqliteOutbox(ConnectionString);
        var trace = await TapAsync(node, outbox);

        await SendQueuedAsync(node, outbox);

        var spans = await TraceShapeTests.SpansAsync(node, trace, "persisted ChargingJourneyActor");
        var tap = spans.Single(span => span.DisplayName == "tap start-charging");
        var send = spans.Single(span => span.DisplayName == "send start-charging");
        var request = spans.Single(span => span.Kind == ActivityKind.Server);
        var journey = spans.Single(span => span.DisplayName == "process ChargingJourneyActor");
        var persist = spans.Single(span => span.DisplayName == "persist JourneyStarted");
        Write(spans);
        new[] { tap, send }.ShouldAllBe(span => span.Source.Name == MobileDiagnostics.Name);
        send.ParentSpanId.ShouldBe(tap.SpanId);
        request.ParentSpanId.ShouldBe(send.SpanId);
        request.DisplayName.ShouldBe("POST /app/v1/journeys");
        journey.ParentSpanId.ShouldBe(request.SpanId);
        journey.Kind.ShouldBe(ActivityKind.Consumer);
        journey.Source.Name.ShouldBe(ActorShellDiagnostics.Name);
        persist.ParentSpanId.ShouldBe(journey.SpanId);
    }

    [Fact]
    public async Task Given_a_send_after_the_app_restarted_then_it_links_to_the_tap()
    {
        await using var node = new TracedNode(postgres);
        var trace = await TapAsync(node, new SqliteOutbox(ConnectionString));

        // A new process: a new outbox on the same file, which did not see the tap.
        var restarted = new SqliteOutbox(ConnectionString);
        await SendQueuedAsync(node, restarted);

        var tap = (await TraceShapeTests.SpansAsync(node, trace, "tap start-charging")).Single();
        var send = await LinkedAsync(node, tap);
        send.DisplayName.ShouldBe("send start-charging");
        send.ParentSpanId.ShouldBe(default);
        send.TraceId.ShouldNotBe(trace);
        var spans = await TraceShapeTests.SpansAsync(
            node, send.TraceId, "persisted ChargingJourneyActor");
        spans.Single(span => span.Kind == ActivityKind.Server).ParentSpanId.ShouldBe(send.SpanId);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_database + suffix);
        }
    }

    /// <summary>
    /// The driver taps start charging on a connector of the test's own: the command is in the
    /// outbox, with the tap's traceparent, and nothing is sent yet.
    /// </summary>
    private static async Task<ActivityTraceId> TapAsync(TracedNode node, SqliteOutbox outbox)
    {
        var (connector, token) = await TraceShapeTests.SetUpAsync(node.CreateClient());
        var request = new StartChargingRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), connector, token, 40m, "USD");
        using var tap = MobileDiagnostics.StartTap(OutboxKinds.StartCharging)!;
        await outbox.EnqueueAsync(
            OutboxCommand.StartCharging(request, tap.Id), DateTimeOffset.UtcNow, Ct);
        return tap.TraceId;
    }

    /// <summary>The app's dispatcher, online, drains the outbox once and stops.</summary>
    private static async Task SendQueuedAsync(TracedNode node, SqliteOutbox outbox)
    {
        var http = new HttpClient(new Propagating(node.Server.CreateHandler()))
        {
            BaseAddress = node.Server.BaseAddress,
        };
        var dispatcher = new OutboxDispatcher(
            outbox, new HttpCommandSender(http), new Online(), new Inline(), TimeProvider.System);
        dispatcher.Start();
        await dispatcher.FlushAsync();
        await dispatcher.StopAsync();
    }

    /// <summary>The trace in the test's output: each span's source, kind, name, parent.</summary>
    private void Write(IEnumerable<Activity> spans)
    {
        foreach (var span in spans.OrderBy(span => span.StartTimeUtc))
        {
            output.WriteLine(
                $"{span.Source.Name,-32} {span.Kind,-8} {span.DisplayName,-40} " +
                $"{span.SpanId} <- {span.ParentSpanId}");
        }
    }

    /// <summary>The first exported span with a link to <paramref name="span"/>.</summary>
    private static async Task<Activity> LinkedAsync(TracedNode node, Activity span)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
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

    /// <summary>
    /// What <see cref="HttpClient"/>'s own handler does on a phone: it puts the current span
    /// into the request's <c>traceparent</c>. The test server's in-memory handler does not.
    /// </summary>
    private sealed class Propagating(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            DistributedContextPropagator.Current.Inject(
                Activity.Current,
                request,
                static (carrier, name, value) =>
                    ((HttpRequestMessage)carrier!).Headers.TryAddWithoutValidation(name, value));
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class Online : IConnectivity
    {
        public bool IsOnline => true;

        public event EventHandler? ConnectivityChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class Inline : IAppDispatcher
    {
        public void Post(Action action) => action();
    }
}
