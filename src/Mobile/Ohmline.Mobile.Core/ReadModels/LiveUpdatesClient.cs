using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Notifications;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Diagnostics;
using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Mobile.Core.ReadModels;

/// <summary>
/// The hub at <c>/hubs/charging</c>, as the app uses it (section 19.7): joined to the driver's
/// token group, applying each <c>driverCardChanged</c> to the cache, and catching up from the
/// feed whenever it may have missed something, because SignalR keeps nothing for a client
/// that is away.
/// </summary>
/// <remarks>
/// A reconnect is a new connection, in no group: after <c>Reconnected</c>, as after the first
/// start, the client joins the group again and pulls the feed. <c>WithAutomaticReconnect</c>
/// tries for about 45 s (0, 2, 10 and 30 s) and then closes, and it never retries the first
/// start: both go back to the retry loop here, until the app stops the client.
/// </remarks>
public sealed class LiveUpdatesClient : ILiveUpdates, IAsyncDisposable
{
    /// <summary>The hub's client methods, by the names the server calls them.</summary>
    public const string CardChangedMethod = "driverCardChanged";

    public const string SummaryChangedMethod = "sessionSummaryChanged";

    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(5);

    private readonly HubConnection _hub;
    private readonly DriverToken _token;
    private readonly SqliteReadModelCache _cache;
    private readonly FeedCatchUp _catchUp;
    private readonly IAppDispatcher _ui;
    private readonly TimeProvider _clock;
    private CancellationTokenSource? _running;

    public LiveUpdatesClient(
        HubConnection hub,
        DriverToken token,
        SqliteReadModelCache cache,
        FeedCatchUp catchUp,
        IAppDispatcher ui,
        TimeProvider clock)
    {
        (_hub, _token, _cache, _catchUp, _ui, _clock) = (hub, token, cache, catchUp, ui, clock);
        _hub.On<DriverCardChanged>(CardChangedMethod, ApplyAsync);
        _hub.On<SessionSummaryChanged>(
            SummaryChangedMethod, summary => _ui.Post(() => SummaryChanged?.Invoke(summary)));
        _hub.Reconnected += _ => JoinAndCatchUpAsync(_running?.Token ?? default);
        _hub.Closed += _ => _running is { } running
            ? ConnectAsync(running.Token)
            : Task.CompletedTask;
    }

    public event Action? CardsChanged;

    public event Action<SessionSummaryChanged>? SummaryChanged;

    /// <summary>
    /// The hub connection for a server, with the client contracts' generated JSON and automatic
    /// reconnects. A test passes <paramref name="configure"/> to reach an in-memory server.
    /// </summary>
    public static HubConnection Connect(
        Uri server, Action<HttpConnectionOptions>? configure = null) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(server, "/hubs/charging"), options => configure?.Invoke(options))
            .WithAutomaticReconnect()
            .AddJsonProtocol(json => json.PayloadSerializerOptions.TypeInfoResolverChain
                .Insert(0, ChargingClientJsonContext.Default))
            .Build();

    /// <summary>
    /// Connects, retrying until it succeeds or the app stops it, then joins the token's group
    /// and catches up. Returns once connected and caught up.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running ??= CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return ConnectAsync(_running.Token);
    }

    /// <summary>Disconnects: no phone keeps a socket open for an app in the background.</summary>
    public async Task StopAsync()
    {
        if (_running is { } running)
        {
            _running = null;
            await running.CancelAsync();
            running.Dispose();
        }

        await _hub.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _hub.DisposeAsync();
    }

    private async Task ConnectAsync(CancellationToken stopping)
    {
        while (_hub.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _hub.StartAsync(stopping);
            }
            catch (Exception failed) when (failed is not OperationCanceledException)
            {
                await Task.Delay(RetryEvery, _clock, stopping);
                continue;
            }

            await JoinAndCatchUpAsync(stopping);
        }
    }

    private async Task JoinAndCatchUpAsync(CancellationToken stopping)
    {
        await _hub.InvokeAsync("JoinToken", _token.Uid, stopping);
        try
        {
            if (await _catchUp.CatchUpAsync(_token.Uid, stopping) > 0)
            {
                _ui.Post(() => CardsChanged?.Invoke());
            }
        }
        catch (HttpRequestException)
        {
            // The pushes still come; the next reconnect or resume catches up again.
        }
    }

    private async Task ApplyAsync(DriverCardChanged changed)
    {
        using var span = MobileDiagnostics.StartApply(CardChangedMethod, changed.TraceParent);
        if (await _cache.ApplyAsync(changed.Card, changed.Position, CancellationToken.None))
        {
            _ui.Post(() => CardsChanged?.Invoke());
        }
    }
}
