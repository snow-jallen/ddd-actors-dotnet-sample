using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Akka.Persistence.Query;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.ReadModels.Projections;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// A projection links to the command that stored its event (section 17.7): a traced command's
/// event is stored in a trace envelope; the runner reads it, starts a trace of its own with a
/// link to the command's span, and hands the projection the event alone.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class ProjectionTraceTests(PostgresFixture postgres)
{
    private static readonly ActivitySource Source = new("Ohmline.Charging.ReadModels.Tests");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_traced_event_then_the_projection_runs_in_a_new_trace_linked_to_it()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = Listen(stopped);
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var charger = journal.System.ActorOf(
            ChargePointActor.Props(Charger, new FakeTimeProvider(Morning)));
        await journal.AskAsync<ChargePointStatus>(charger, new GetChargePointStatus(Charger), Ct);
        ActivityTraceId requestTrace;
        using (var request = Source.StartActivity("POST /charge-points")!)
        {
            requestTrace = request.TraceId;
            await charger.AskTraced<CommandReply>(Register(Charger), cancellationToken: Ct);
        }

        var probe = new TraceProbe();
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await using (Runner(journal, database, probe).Start(journal.System.Materializer()))
        {
            var seen = await probe.Traced.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

            seen.Event.ShouldBeOfType<ChargePointRegistered>();
            seen.Span.ShouldNotBeNull();
            seen.Span.Source.Name.ShouldBe(ChargingDiagnostics.Name);
            seen.Span.DisplayName.ShouldBe("process trace-probe");
            seen.Span.Kind.ShouldBe(ActivityKind.Consumer);
            seen.Span.ParentSpanId.ShouldBe(default);
            seen.Span.TraceId.ShouldNotBe(requestTrace);
            var decided = stopped.Single(span =>
                span.TraceId == requestTrace && span.DisplayName == "process ChargePointActor");
            var link = seen.Span.Links.ShouldHaveSingleItem().Context;
            link.TraceId.ShouldBe(requestTrace);
            link.SpanId.ShouldBe(decided.SpanId);
        }
    }

    private static ProjectionRunner Runner(
        MemoryJournal journal, NpgsqlDataSource database, IProjectionHandler projection) =>
        new(
            journal.ReadJournal,
            database,
            new NpgsqlOffsetStore(database),
            projection,
            ChargingTags.Charging,
            ProjectionRun.QuickBackoff,
            _ => { },
            journal.System.Log);

    private static ActivityListener Listen(ConcurrentQueue<Activity> stopped)
    {
        string[] sources = [Source.Name, ActorShellDiagnostics.Name, ChargingDiagnostics.Name];
        var listener = new ActivityListener
        {
            ShouldListenTo = source => sources.Contains(source.Name),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>Writes nothing; remembers the first traced event and the span it ran in.</summary>
    private sealed class TraceProbe : IProjectionHandler
    {
        public TaskCompletionSource<(object Event, Activity? Span)> Traced { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ProjectionId => "trace-probe";

        public IReadOnlyList<string> Tags => [ChargingTags.Charging];

        public ProjectionFailurePolicy FailurePolicy { get; } = new StopAfterRetries(0);

        public Task<ImmutableArray<LiveUpdate>> HandleAsync(
            EventEnvelope envelope,
            NpgsqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            if (Activity.Current is { } span)
            {
                Traced.TrySetResult((envelope.Event, span));
            }

            return Task.FromResult(ImmutableArray<LiveUpdate>.Empty);
        }
    }
}
