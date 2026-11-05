using System.Collections.Concurrent;
using System.Diagnostics;
using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// Stash, persist and the double count (section 17.6). While a write is in flight, a persistent
/// actor stashes the commands that arrive, envelope and all, and each comes through the shell
/// twice. The persist callbacks run after the command's span has ended. The shell keeps the
/// count right and the trace whole.
/// </summary>
public sealed class StashAndPersistTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Envelopes.Serializer), nameof(StashAndPersistTests), output)
{
    private static readonly ActivitySource Source = new("Ohmline.Telemetry.Tests.Stash");

    [Fact]
    public async Task Given_commands_that_wait_behind_a_write_then_each_is_handled_once()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var (meter, handled) = await StartedAsync();
        var gate = new JournalGate();
        await Journal.OnWrite.SetInterceptorAsync(gate);

        using var request = Source.StartActivity("POST /meters/readings")!;
        foreach (var kwh in new[] { 1, 2, 3 })
        {
            meter.TellTraced(new Read(kwh), TestActor);
        }

        // The first command's write is held: the other two wait in the stash meanwhile.
        await AwaitAssertAsync(
            () => recorder.Trace(request.TraceId)
                .Count(span => span.DisplayName == "stash Meter").ShouldBeGreaterThanOrEqualTo(2),
            cancellationToken: Ct);
        gate.Open();
        await ExpectMsgAsync<Total>(total => total.Kwh == 1, cancellationToken: Ct);
        await ExpectMsgAsync<Total>(total => total.Kwh == 3, cancellationToken: Ct);
        await ExpectMsgAsync<Total>(total => total.Kwh == 6, cancellationToken: Ct);

        handled.Where(name => name == nameof(Read)).Count().ShouldBe(3);
        var spans = await recorder.WaitForAsync(request.TraceId, "process Meter", count: 3);
        spans.Count(span => span.DisplayName == "process Meter").ShouldBe(3);
        spans.Where(span => span.DisplayName == "stash Meter")
            .ShouldAllBe(span => Equals(span.GetTagItem("akka.message.stashed"), true));
    }

    [Fact]
    public async Task Given_a_traced_command_then_its_writes_are_children_of_its_span()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var (meter, _) = await StartedAsync();
        using var request = Source.StartActivity("POST /meters/readings")!;

        var total = await meter.AskTraced<Total>(new Read(7), RemainingOrDefault, Ct);

        var spans = await recorder.WaitForAsync(request.TraceId, "persisted Meter", count: 1);
        var process = spans.Single(span => span.DisplayName == "process Meter");
        var persist = spans.Single(span => span.DisplayName == "persist Counted");
        var apply = spans.Single(span => span.DisplayName == "apply Counted");
        var persisted = spans.Single(span => span.DisplayName == "persisted Meter");
        new[] { persist, apply, persisted }
            .ShouldAllBe(span => span.ParentSpanId == process.SpanId);
        persist.StartTimeUtc.ShouldBeLessThan(process.StartTimeUtc + process.Duration);
        (persist.StartTimeUtc + persist.Duration).ShouldBeGreaterThan(
            process.StartTimeUtc + process.Duration);
        persist.GetTagItem("akka.persistence.id").ShouldBe("meter");
        persist.GetTagItem("akka.persistence.sequence_nr").ShouldBe(2L);
        total.AppliedIn.ShouldBe(apply.Id);
        total.AnsweredIn.ShouldBe(persisted.Id);
    }

    [Fact]
    public async Task Given_a_plain_command_then_it_is_handled_once_and_records_no_span()
    {
        using var recorder = new SpanRecorder(ActorShellDiagnostics.Name);
        var (meter, handled) = await StartedAsync();

        var total = await meter.Ask<Total>(new Read(7), RemainingOrDefault, Ct);

        total.AppliedIn.ShouldBeNull();
        handled.Where(name => name == nameof(Read)).Count().ShouldBe(1);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The meter, recovered already, and the names of the messages it handled.</summary>
    private async Task<(IActorRef Meter, ConcurrentQueue<string> Handled)> StartedAsync()
    {
        var handled = new ConcurrentQueue<string>();
        var meter = Sys.ActorOf(Props.Create(() => new Meter(handled)), "meter");
        await meter.Ask<Total>(new Read(0), RemainingOrDefault, Ct);
        handled.Clear();
        return (meter, handled);
    }

    private sealed record Read(int Kwh);

    private sealed record Counted(int Kwh);

    private sealed record Total(int Kwh, string? AppliedIn, string? AnsweredIn);

    /// <summary>Holds every write back until it is opened, then lets them all through.</summary>
    /// <remarks>
    /// The interceptor reaches the test journal as a message, and with serialize-messages on
    /// the journal gets a copy: so the gate is found by its id, not held in the instance.
    /// </remarks>
    private sealed class JournalGate : IJournalInterceptor
    {
        private static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Gates = new();

        public Guid Id { get; init; } = Guid.NewGuid();

        public void Open() => Gate.TrySetResult();

        public Task InterceptAsync(IPersistentRepresentation message) => Gate.Task;

        private TaskCompletionSource Gate => Gates.GetOrAdd(
            Id, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    /// <summary>Persists each reading, and answers with the total once it is stored.</summary>
    private sealed class Meter : TracedPersistentActor
    {
        private readonly ConcurrentQueue<string> _handled;
        private int _kwh;
        private string? _appliedIn;

        public Meter(ConcurrentQueue<string> handled)
        {
            _handled = handled;
            Recover<Counted>(counted => _kwh += counted.Kwh);
            Command<Read>(read => PersistAllTraced(
                [new Counted(read.Kwh)],
                counted =>
                {
                    _kwh += counted.Kwh;
                    _appliedIn = Activity.Current?.Id;
                },
                () => Sender.Tell(new Total(_kwh, _appliedIn, Activity.Current?.Id))));
        }

        public override string PersistenceId => "meter";

        protected override void OnMessageHandled(object message, TimeSpan duration) =>
            _handled.Enqueue(message.GetType().Name);
    }
}
