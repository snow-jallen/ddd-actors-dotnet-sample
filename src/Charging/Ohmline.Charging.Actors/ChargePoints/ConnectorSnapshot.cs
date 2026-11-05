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

    /// <summary>Held for a token, for <paramref name="ExpiresIn"/> after the snapshot.</summary>
    public sealed record Reserved(
        int Number, Power MaxPower, TokenUid TokenUid, TimeSpan ExpiresIn)
        : ConnectorSnapshot(Number, MaxPower);

    public sealed record Occupied(int Number, Power MaxPower, SessionId SessionId)
        : ConnectorSnapshot(Number, MaxPower);

    /// <summary>Out of order; its kind is still written <c>"faulted"</c> (section 20.7).</summary>
    public sealed record OutOfOrder(int Number, Power MaxPower, string ErrorCode)
        : ConnectorSnapshot(Number, MaxPower);

    public sealed record Unavailable(int Number, Power MaxPower)
        : ConnectorSnapshot(Number, MaxPower);
}
