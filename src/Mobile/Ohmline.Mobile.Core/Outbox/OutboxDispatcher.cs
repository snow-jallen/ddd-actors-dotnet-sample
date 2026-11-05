using System.Diagnostics;
using System.Threading.Channels;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Diagnostics;
using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// Sends what the outbox holds, in order, one command at a time (section 19.6): the actor's
/// shape without an actor framework. Its mailbox is a <see cref="Channel{T}"/> with a single
/// reader, and the one loop that reads it is the only code that sends; a nudge from anywhere
/// (a tap, a connectivity change, the app coming back, a timer) is a message, never a lock.
/// </summary>
/// <remarks>
/// A drain sends every queued command, oldest first. An accepted command records the server's
/// version; a rejected one is final and never sent again, and does not hold up the next. A
/// transient failure stops the drain, so that commands reach the server in the order the
/// driver gave them; the timer tries again after a backoff with jitter, with the same ids. A
/// command still queued after <see cref="MaxAge"/> has failed to sync.
/// </remarks>
public sealed class OutboxDispatcher(
    SqliteOutbox outbox,
    ICommandSender sender,
    IConnectivity connectivity,
    IAppDispatcher ui,
    TimeProvider clock)
{
    /// <summary>How long the app keeps trying: shorter than the server remembers ids.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>How often an idle dispatcher looks anyway, in case a nudge was missed.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan LastRetry = TimeSpan.FromMinutes(5);

    private readonly Channel<Signal> _mailbox =
        Channel.CreateUnbounded<Signal>(new UnboundedChannelOptions { SingleReader = true });

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _running;
    private Task _loops = Task.CompletedTask;
    private int _failures;

    /// <summary>A command's status changed; raised on the UI thread.</summary>
    public event Action<Guid, CommandStatus>? StatusChanged;

    /// <summary>
    /// Starts the mailbox loop and the retry timer, and listens for connectivity changes: at
    /// app start, and when the app comes back to the foreground, followed by a nudge.
    /// </summary>
    public void Start()
    {
        if (_running is not null)
        {
            return;
        }

        _running = new CancellationTokenSource();
        _timer = new PeriodicTimer(Idle, clock);
        var stopping = _running.Token;
        _loops = Task.WhenAll(
            Task.Run(() => ReceiveAsync(stopping), stopping),
            Task.Run(() => TickAsync(_timer, stopping), stopping));
        connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Stops both loops: when the app goes to the background, where the operating system may
    /// kill it at any moment. A command being sent stays queued and is sent again, same id.
    /// </summary>
    public async Task StopAsync()
    {
        if (_running is null)
        {
            return;
        }

        connectivity.ConnectivityChanged -= OnConnectivityChanged;
        await _running.CancelAsync();
        try
        {
            await _loops;
        }
        catch (OperationCanceledException)
        {
            // Stopped while waiting or sending, as asked.
        }

        _timer!.Dispose();
        _running.Dispose();
        _running = null;
    }

    /// <summary>Asks for a drain and returns at once.</summary>
    public void Nudge() => _mailbox.Writer.TryWrite(new Nudged());

    /// <summary>Asks for a drain and waits for it: for a pull to refresh, and for tests.</summary>
    public async Task FlushAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mailbox.Writer.TryWrite(new FlushRequested(done));
        await done.Task;
    }

    /// <summary>
    /// The wait after <paramref name="failures"/> transient failures in a row: 2 s, doubling up
    /// to 5 minutes, then spread by ±20%, from <paramref name="jitter"/> in [0, 1), so that
    /// a thousand phones that lost the network together do not all come back in the same second.
    /// </summary>
    public static TimeSpan Backoff(int failures, double jitter)
    {
        var exponential = FirstRetry * Math.Pow(2, Math.Max(0, failures - 1));
        var capped = exponential < LastRetry ? exponential : LastRetry;
        return capped * (0.8 + (0.4 * jitter));
    }

    private void OnConnectivityChanged(object? sender, EventArgs e) => Nudge();

    /// <summary>The single reader: every drain happens here, one after the other.</summary>
    private async Task ReceiveAsync(CancellationToken stopping)
    {
        await foreach (var signal in _mailbox.Reader.ReadAllAsync(stopping))
        {
            await DrainAsync(stopping);
            if (signal.Value is FlushRequested flush)
            {
                flush.Done.TrySetResult();
            }
        }
    }

    private async Task TickAsync(PeriodicTimer timer, CancellationToken stopping)
    {
        while (await timer.WaitForNextTickAsync(stopping))
        {
            Nudge();
        }
    }

    /// <summary>
    /// Sends every queued command, oldest first, and stops at the first transient failure.
    /// Offline, it sends nothing: connectivity is a hint, but a hint that saves a battery.
    /// </summary>
    private async Task DrainAsync(CancellationToken stopping)
    {
        foreach (var expired in await outbox.ExpireAsync(clock.GetUtcNow() - MaxAge, stopping))
        {
            Publish(expired, new FailedToSync());
        }

        if (!connectivity.IsOnline)
        {
            return;
        }

        foreach (var row in await outbox.ReadQueuedAsync(stopping))
        {
            var attempt = row.Attempts + 1;
            await outbox.MarkAttemptAsync(row.CommandId, stopping);
            Publish(row.CommandId, new Sending(attempt));
            using var span = MobileDiagnostics.StartSend(
                row.Kind,
                row.CommandId,
                attempt,
                row.TraceParent,
                outbox.EnqueuedByThisProcess(row.CommandId));
            switch (await sender.SendAsync(row, stopping))
            {
                case SendAccepted accepted:
                    await outbox.MarkAcceptedAsync(row.CommandId, accepted.Version, stopping);
                    Publish(row.CommandId, new Accepted(accepted.Version));
                    break;
                case SendRejected rejected:
                    await outbox.MarkRejectedAsync(
                        row.CommandId, rejected.Code, rejected.Reason, stopping);
                    Publish(
                        row.CommandId,
                        new Rejected(rejected.Code, RejectionText.For(rejected.Code)));
                    break;
                case SendTransientFailure failure:
                    span?.SetStatus(ActivityStatusCode.Error, failure.Why);
                    Publish(row.CommandId, new Queued(attempt));
                    _timer!.Period = Backoff(++_failures, Random.Shared.NextDouble());
                    return; // Keep the order: nothing after it goes first.
                case null:
                    throw new InvalidOperationException("The outcome is default.");
            }
        }

        _failures = 0;
        _timer!.Period = Idle;
    }

    private void Publish(Guid commandId, CommandStatus status) =>
        ui.Post(() => StatusChanged?.Invoke(commandId, status));

    private sealed record Nudged;

    private sealed record FlushRequested(TaskCompletionSource Done);

    /// <summary>The mailbox's messages: a closed set, read by the one loop.</summary>
    private union Signal(Nudged, FlushRequested);
}
