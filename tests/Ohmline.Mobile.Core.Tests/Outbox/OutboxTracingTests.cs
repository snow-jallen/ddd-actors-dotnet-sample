using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Mobile.Core.Diagnostics;
using Ohmline.Mobile.Core.Outbox;
using static Ohmline.Mobile.Core.Tests.Outbox.Commands;

namespace Ohmline.Mobile.Core.Tests.Outbox;

/// <summary>
/// A command's trace from the tap to its send (section 19.8), hours apart if need be: the
/// outbox keeps the tap's traceparent with the command.
/// </summary>
public sealed class OutboxTracingTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;
    private readonly TemporaryDatabase _database = new();

    public OutboxTracingTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MobileDiagnostics.Name,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_tap_then_the_command_keeps_its_traceparent()
    {
        var outbox = new SqliteOutbox(_database.ConnectionString);

        var (command, tap) = await TapAsync(outbox);

        (await outbox.FindAsync(command.CommandId, Ct))!.TraceParent.ShouldBe(tap.Id);
    }

    [Fact]
    public async Task Given_a_send_in_the_process_that_saw_the_tap_then_it_is_the_taps_child()
    {
        var outbox = new SqliteOutbox(_database.ConnectionString);
        var (command, tap) = await TapAsync(outbox);

        await SendAsync(outbox);

        var send = Sent(command.CommandId);
        (send.TraceId, send.ParentSpanId).ShouldBe((tap.TraceId, tap.SpanId));
        send.Links.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_send_after_the_app_restarted_then_it_links_to_the_tap()
    {
        var (command, tap) = await TapAsync(new SqliteOutbox(_database.ConnectionString));

        await SendAsync(new SqliteOutbox(_database.ConnectionString)); // A new process.

        var send = Sent(command.CommandId);
        send.ParentSpanId.ShouldBe(default);
        send.TraceId.ShouldNotBe(tap.TraceId);
        send.Links.ShouldHaveSingleItem().Context.SpanId.ShouldBe(tap.SpanId);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _database.Dispose();
    }

    private static async Task<(OutboxCommand Command, Activity Tap)> TapAsync(SqliteOutbox outbox)
    {
        using var tap = MobileDiagnostics.StartTap(OutboxKinds.StartCharging)!;
        var command = StartCharging() with { TraceParent = tap.Id };
        await outbox.EnqueueAsync(command, Morning, Ct);
        return (command, tap);
    }

    private static async Task SendAsync(SqliteOutbox outbox)
    {
        var dispatcher = new OutboxDispatcher(
            outbox,
            new FakeSender(),
            new FakeConnectivity(),
            new InlineDispatcher(),
            new FakeTimeProvider(Morning));
        dispatcher.Start();
        await dispatcher.FlushAsync();
        await dispatcher.StopAsync();
    }

    private Activity Sent(Guid commandId) => _stopped.Single(span =>
        span.DisplayName == "send start-charging"
        && (string?)span.GetTagItem(MobileDiagnostics.CommandIdTag) == commandId.ToString());
}
