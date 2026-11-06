using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Messages;
using Ohmline.SharedKernel;

namespace Ohmline.Simulator;

/// <summary>
/// One charge point on a WebSocket to the gateway, as a well-behaved charger of the subset
/// would act: it calls and waits for each answer, and accepts every remote start, remote stop
/// and charging profile it is sent, remembering the last.
/// </summary>
public sealed class SimulatedCharger(string chargePointId) : IAsyncDisposable
{
    private const int Attempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _calls = new();
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource<CloseFrame> _closed = NewSignal<CloseFrame>();
    private Task _receiving = Task.CompletedTask;
    private int _nextId;

    public string ChargePointId => chargePointId;

    /// <summary>The next remote start the gateway sends, once it has.</summary>
    public TaskCompletionSource<RemoteStartTransactionRequest> RemoteStart { get; private set; } =
        NewSignal<RemoteStartTransactionRequest>();

    /// <summary>The limit of the last charging profile, in W, if any.</summary>
    public decimal? ProfileLimit { get; private set; }

    /// <summary>
    /// Completes when the connection ends: with the gateway's close frame, which this charger
    /// answers, or with no status at all if the connection was lost.
    /// </summary>
    public Task<CloseFrame> Closed => _closed.Task;

    public async Task ConnectAsync(Uri gateway, CancellationToken cancellationToken)
    {
        _socket.Options.AddSubProtocol("ocpp1.6");
        var socketUri = new UriBuilder(gateway)
        {
            Scheme = gateway.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = $"/ocpp/{chargePointId}",
        }.Uri;
        await _socket.ConnectAsync(socketUri, cancellationToken);
        _receiving = ReceiveAllAsync(_stopping.Token);
    }

    /// <summary>
    /// Calls the gateway and waits for its answer's payload. An <c>InternalError</c> means that
    /// Charging did not answer the gateway in time and the gateway undid the call, so the
    /// charger sends it again, as chargers retry what the central system failed to process.
    /// </summary>
    public async Task<JsonElement> CallAsync<T>(
        string action, T payload, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CallOnceAsync(action, payload, cancellationToken);
            }
            catch (GatewayCallError error)
                when (error.ErrorCode == "InternalError" && attempt < Attempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    private async Task<JsonElement> CallOnceAsync<T>(
        string action, T payload, CancellationToken cancellationToken)
    {
        var id = $"{chargePointId}-{Interlocked.Increment(ref _nextId)}";
        var answer = NewSignal<JsonElement>();
        _calls[id] = answer;
        await SendAsync(new Call(id, action, OcppJson.Element(payload)), cancellationToken);
        return await answer.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    public Task<JsonElement> StatusAsync(
        int connector,
        string status,
        CancellationToken cancellationToken,
        string error = "NoError") =>
        CallAsync(
            OcppActions.StatusNotification,
            new StatusNotificationRequest(connector, error, status, DateTimeOffset.UtcNow),
            cancellationToken);

    /// <summary>The network fails: the socket goes, with no goodbye.</summary>
    public void Drop()
    {
        _stopping.Cancel();
        _socket.Abort();
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State is WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", default);
        }

        _stopping.Cancel();
        await _receiving.ContinueWith(_ => { }, TaskScheduler.Default);
        _socket.Dispose();
    }

    private async Task ReceiveAllAsync(CancellationToken stopping)
    {
        try
        {
            await ReceiveFramesAsync(stopping);
        }
        finally
        {
            _closed.TrySetResult(new CloseFrame(null, null));
        }
    }

    private async Task ReceiveFramesAsync(CancellationToken stopping)
    {
        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();
        while (_socket.State is WebSocketState.Open && !stopping.IsCancellationRequested)
        {
            var received = await _socket.ReceiveAsync(buffer, stopping);
            if (received.MessageType is WebSocketMessageType.Close)
            {
                await _socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                _closed.TrySetResult(
                    new CloseFrame(received.CloseStatus, received.CloseStatusDescription));
                return;
            }

            message.Write(buffer, 0, received.Count);
            if (!received.EndOfMessage)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(message.ToArray());
            message.SetLength(0);
            if (OcppFrameParser.Parse(text) is Ok<OcppFrame> { Value: var frame })
            {
                await HandleAsync(frame, stopping);
            }
        }
    }

    private async Task HandleAsync(OcppFrame frame, CancellationToken cancellationToken)
    {
        switch (frame.Value)
        {
            case CallResult result when _calls.TryRemove(result.MessageId, out var answer):
                answer.TrySetResult(result.Payload);
                break;
            case CallError error when _calls.TryRemove(error.MessageId, out var answer):
                answer.TrySetException(new GatewayCallError(error.ErrorCode, error.Description));
                break;
            case Call call:
                await SendAsync(
                    new CallResult(call.MessageId, OcppJson.Element(new { status = "Accepted" })),
                    cancellationToken);
                Heard(call);
                break;
        }
    }

    private void Heard(Call call)
    {
        switch (call.Action)
        {
            case OcppActions.RemoteStartTransaction:
                RemoteStart.TrySetResult(
                    call.Payload.Deserialize<RemoteStartTransactionRequest>(OcppJson.Options)!);
                break;
            case OcppActions.SetChargingProfile:
                var profile = call.Payload
                    .Deserialize<SetChargingProfileRequest>(OcppJson.Options)!;
                ProfileLimit = profile.CsChargingProfiles.ChargingSchedule
                    .ChargingSchedulePeriod[0].Limit;
                break;
        }
    }

    private async Task SendAsync(OcppFrame frame, CancellationToken cancellationToken)
    {
        await _sending.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(
                Encoding.UTF8.GetBytes(OcppFrameParser.Write(frame)),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            _sending.Release();
        }
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>How a connection ended: the close frame's status and reason, or neither.</summary>
public sealed record CloseFrame(WebSocketCloseStatus? Status, string? Description);

/// <summary>The gateway answered a call with a <c>CallError</c>.</summary>
public sealed class GatewayCallError(string errorCode, string description)
    : InvalidOperationException($"The gateway answered {errorCode}: {description}")
{
    public string ErrorCode => errorCode;
}
