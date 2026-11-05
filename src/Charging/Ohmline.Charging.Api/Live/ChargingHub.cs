using Microsoft.AspNetCore.SignalR;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Live;

/// <summary>
/// Live updates for app clients (section 14.5), at <c>/hubs/charging</c>. A client joins the
/// group of each session or token it shows, and receives what the projections commit. Every
/// name here is durable: the driver app is built against it.
/// </summary>
/// <remarks>
/// SignalR keeps nothing for a client that is away: what was sent while it was disconnected is
/// gone. So a client reads the current state after each (re)connect, as it does on start, and
/// joins its groups again: a reconnect is a new connection, in no group.
/// </remarks>
public sealed class ChargingHub : Hub
{
    public const string Path = "/hubs/charging";

    /// <summary>The client method a session's new summary is sent to.</summary>
    public const string SessionSummaryChanged = "sessionSummaryChanged";

    public static string SessionGroup(SessionId sessionId) => $"session-{sessionId}";

    public static string TokenGroup(TokenUid tokenUid) => $"token-{tokenUid}";

    /// <summary>From now on, this connection hears about the session.</summary>
    public Task JoinSession(Guid sessionId) =>
        SessionId.TryCreate(sessionId).TryGet(out var id, out var error)
            ? Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(id))
            : throw new HubException(error.Message);

    /// <summary>From now on, this connection hears about the token's sessions.</summary>
    public Task JoinToken(string tokenUid) =>
        TokenUid.TryCreate(tokenUid).TryGet(out var uid, out var error)
            ? Groups.AddToGroupAsync(Context.ConnectionId, TokenGroup(uid))
            : throw new HubException(error.Message);
}
