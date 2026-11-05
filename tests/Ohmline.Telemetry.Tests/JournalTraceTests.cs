using System.Diagnostics;
using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// The journal remembers which span decided each event (section 17.7): a traced command's
/// events are stored in trace envelopes, so a projection reading them days later can link to
/// that span; recovery opens the envelopes before any handler sees them.
/// </summary>
public sealed class JournalTraceTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Envelopes.Serializer), nameof(JournalTraceTests), output)
{
    private static readonly ActivitySource Source = new("Ohmline.Telemetry.Tests.Journal");

    [Fact]
    public async Task Given_a_traced_command_then_its_event_is_stored_with_its_deciding_span()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var (counter, _) = await StartedAsync("counter-1");
        using var request = Source.StartActivity("POST /counters/{id}")!;

        await counter.AskTraced<int>(new Add(5), RemainingOrDefault, Ct);

        var process = (await recorder.WaitForAsync(request.TraceId, "apply Added", count: 1))
            .Single(span => span.DisplayName == "process Counter");
        var stored = await StoredAsync("counter-1");
        stored[0].ShouldBe(new Added(0));
        var envelope = stored[1].ShouldBeOfType<TraceEnvelope>();
        envelope.Message.ShouldBe(new Added(5));
        envelope.TraceParent.ShouldBe(process.Id);
    }

    [Fact]
    public async Task Given_traced_events_in_the_journal_then_a_new_incarnation_recovers_them()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var (counter, _) = await StartedAsync("counter-2");
        using (Source.StartActivity("POST /counters/{id}"))
        {
            await counter.AskTraced<int>(new Add(5), RemainingOrDefault, Ct);
            await counter.AskTraced<int>(new Add(2), RemainingOrDefault, Ct);
        }

        await StopAsync(counter);

        var (_, recovered) = await StartedAsync("counter-2");
        recovered.ShouldBe(7);
    }

    [Fact]
    public async Task Given_a_plain_command_then_its_event_is_stored_plain()
    {
        var (counter, _) = await StartedAsync("counter-3");

        await counter.Ask<int>(new Add(5), RemainingOrDefault, Ct);

        (await StoredAsync("counter-3")).ShouldBe([new Added(0), new Added(5)]);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// A counter that has recovered, and its total: it stores an untraced <c>Added(0)</c> to
    /// say so. Unnamed: the name of an incarnation just stopped can still be taken.
    /// </summary>
    private async Task<(IActorRef Counter, int Total)> StartedAsync(string persistenceId)
    {
        var counter = Sys.ActorOf(
            Props.Create(() => new Counter(persistenceId)));
        return (counter, await counter.Ask<int>(new Add(0), RemainingOrDefault, Ct));
    }

    private async Task StopAsync(IActorRef actor)
    {
        Watch(actor);
        Sys.Stop(actor);
        await ExpectTerminatedAsync(actor, cancellationToken: Ct);
    }

    /// <summary>What the journal holds, as stored: an actor that recovers it and says so.</summary>
    private async Task<IReadOnlyList<object>> StoredAsync(string persistenceId)
    {
        var reader = Sys.ActorOf(Props.Create(() => new JournalReader(persistenceId)));
        var stored = await reader.Ask<JournalReader.Stored>(
            JournalReader.WhatIsStored.Instance, RemainingOrDefault, Ct);
        return stored.Events;
    }

    private sealed record Add(int Amount);

    private sealed record Added(int Amount);

    private sealed class Counter : TracedPersistentActor
    {
        private int _total;

        public Counter(string persistenceId)
        {
            PersistenceId = persistenceId;
            Recover<Added>(added => _total += added.Amount);
            Command<Add>(add => PersistTraced(
                new Added(add.Amount),
                added =>
                {
                    _total += added.Amount;
                    Sender.Tell(_total);
                }));
        }

        public override string PersistenceId { get; }
    }

    /// <summary>Recovers a persistence id with no shell in the way: the rows as stored.</summary>
    private sealed class JournalReader : ReceivePersistentActor
    {
        private readonly List<object> _stored = [];

        public JournalReader(string persistenceId)
        {
            PersistenceId = persistenceId;
            Recover<RecoveryCompleted>(_ => { });
            Recover<object>(_stored.Add);
            Command<WhatIsStored>(_ => Sender.Tell(new Stored([.. _stored])));
        }

        public override string PersistenceId { get; }

        public sealed record WhatIsStored
        {
            public static WhatIsStored Instance { get; } = new();
        }

        /// <summary>Local only: the rows as objects, envelopes and all.</summary>
        public sealed record Stored(IReadOnlyList<object> Events)
            : INoSerializationVerificationNeeded;
    }
}
