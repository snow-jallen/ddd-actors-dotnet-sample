using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// One connector in a charge point's snapshot. The case is the connector's state, written as a
/// permanent <c>"$kind"</c>, and each case carries only the data that is true in that state, as
/// the state union's cases do.
/// </summary>
public closed record ConnectorSnapshot(int Number, Power MaxPower)
{
    public sealed record Available(int Number, Power MaxPower)
        : ConnectorSnapshot(Number, MaxPower);

    public sealed record Reserved(
        int Number, Power MaxPower, TokenUid TokenUid, DateTimeOffset ExpiresAt)
        : ConnectorSnapshot(Number, MaxPower);

    public sealed record Occupied(int Number, Power MaxPower, SessionId SessionId)
        : ConnectorSnapshot(Number, MaxPower);

    public sealed record Faulted(int Number, Power MaxPower, string ErrorCode)
        : ConnectorSnapshot(Number, MaxPower);

    public sealed record Unavailable(int Number, Power MaxPower)
        : ConnectorSnapshot(Number, MaxPower);
}
