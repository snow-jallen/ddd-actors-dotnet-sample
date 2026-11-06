using System.Collections.Immutable;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Actors.Sessions;

/// <summary>
/// A started charging session, as its snapshot stores it: the terms, which never change, and
/// the phase it is in. A DTO with a manifest of its own, never the state union. Equal when the
/// values are equal.
/// </summary>
public sealed record ChargingSessionSnapshot(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    TariffSnapshot Tariff,
    Money SpendCap,
    SessionPhaseSnapshot Phase)
{
    /// <summary>
    /// The commands it accepted most recently, so that a duplicate after a restart still gets
    /// its first answer (section 12.6). A snapshot stored before there was a window has none.
    /// </summary>
    public ImmutableArray<RecentCommand> RecentCommands
    {
        get;
        init => field = value.IsDefault ? [] : value;
    } = [];

    /// <summary>
    /// The phases the car charges on (section 20.2). Optional: a snapshot stored before it
    /// existed has none, and the state takes the assumed count.
    /// </summary>
    public int? PhaseCount { get; init; }

    /// <summary>
    /// How the session's fees apply (section 21.8). Optional: a snapshot stored before it
    /// existed has none, and the session is standard.
    /// </summary>
    public string? PricingModel { get; init; }

    public bool Equals(ChargingSessionSnapshot? other) =>
        other is not null
        && SessionId == other.SessionId
        && SiteId == other.SiteId
        && ConnectorId == other.ConnectorId
        && TokenUid == other.TokenUid
        && Tariff == other.Tariff
        && SpendCap == other.SpendCap
        && Phase == other.Phase
        && PhaseCount == other.PhaseCount
        && PricingModel == other.PricingModel
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() => HashCode.Combine(SessionId, Phase);
}
