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
