using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Contracts.ChargePoints;

/// <summary>
/// One connector in a <see cref="ChargePointStatus"/>. <paramref name="State"/> is one of the
/// glossary's connector statuses, written as one of the constants below; the optional fields
/// are filled only in the states they belong to: a token and an expiry when reserved, a session
/// when occupied, the charge point's error code when faulted.
/// </summary>
public sealed record ConnectorStatus(
    int ConnectorNumber,
    Power MaxPower,
    string State,
    TokenUid? ReservedFor = null,
    DateTimeOffset? ReservedUntil = null,
    SessionId? SessionId = null,
    string? ErrorCode = null)
{
    public const string Available = "available";
    public const string Reserved = "reserved";
    public const string Occupied = "occupied";
    public const string Faulted = "faulted";
    public const string Unavailable = "unavailable";
}
