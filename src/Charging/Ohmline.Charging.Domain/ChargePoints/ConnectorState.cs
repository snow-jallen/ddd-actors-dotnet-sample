using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// What a connector is doing now. Each case carries only the data that is true in it: only a
/// reserved connector has a token and an expiry, and only an occupied one has a session. A
/// connector that is available and still has a session cannot be written down.
/// </summary>
public union ConnectorState(Available, Reserved, Occupied, Faulted, Unavailable);

/// <summary>Free for the next driver.</summary>
public sealed record Available;

/// <summary>Held for one token until the expiry time.</summary>
public sealed record Reserved(TokenUid TokenUid, DateTimeOffset ExpiresAt)
{
    /// <summary>A reservation protects the connector up to its expiry time, not at it.</summary>
    public bool HasExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
}

/// <summary>A car is plugged in, and this session is using the connector.</summary>
public sealed record Occupied(SessionId SessionId);

/// <summary>The charge point reported a fault; the code is the charge point's own.</summary>
public sealed record Faulted(string ErrorCode);

/// <summary>Operators took the connector out of use.</summary>
public sealed record Unavailable;
