using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Live;

/// <summary>
/// What a projection tells the nodes after it has committed a change to its read model
/// (section 14.4), for whoever pushes it on to clients. It travels on DistributedPubSub to every
/// node with the <c>charging</c> role, so it is a closed record with a manifest of its own. It is
/// a notification, not a source of truth: what it says can always be read back from the read
/// model, at that version or a newer one.
/// </summary>
public closed record LiveUpdate;

/// <summary>
/// A session's row in the session history changed. <paramref name="Version"/> is the session's
/// version the row now shows; <paramref name="Status"/> and <paramref name="EnergyKwh"/> are the
/// row's, as the query API returns them.
/// </summary>
public sealed record SessionSummaryChanged(
    SessionId SessionId, long Version, string Status, decimal EnergyKwh) : LiveUpdate;

/// <summary>
/// A session's card in the driver app changed (section 14.6): the whole card, as the app shows
/// it, and the <paramref name="Position"/> of its entry in the token's change feed, so an app
/// that applies it can carry on reading the feed after it. <paramref name="TraceParent"/> is
/// the projection's span (section 19.8), for the app to link to.
/// </summary>
public sealed record DriverCardChanged(
    TokenUid TokenUid,
    SessionId SessionId,
    long Version,
    long Position,
    string Status,
    string ConnectorId,
    string? ChargePointDisplayName,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? TraceParent = null) : LiveUpdate;
