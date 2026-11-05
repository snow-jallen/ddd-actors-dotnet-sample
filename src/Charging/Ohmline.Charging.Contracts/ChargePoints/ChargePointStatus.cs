using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.ChargePoints;

/// <summary>
/// The answer to <see cref="GetChargePointStatus"/>: what the charge point knows now, at
/// <paramref name="Version"/>. Plain data for the wire, not the aggregate's state: a charge point
/// that is not registered has no site and no connectors. Equal when the values are equal: a
/// record alone would compare the array by reference.
/// </summary>
public sealed record ChargePointStatus(
    ChargePointId ChargePointId,
    long Version,
    SiteId? SiteId,
    ImmutableArray<ConnectorStatus> Connectors)
{
    public bool Equals(ChargePointStatus? other) =>
        other is not null
        && ChargePointId == other.ChargePointId
        && Version == other.Version
        && SiteId == other.SiteId
        && Connectors.SequenceEqual(other.Connectors);

    public override int GetHashCode() =>
        HashCode.Combine(ChargePointId, Version, SiteId, Connectors.Length);
}
