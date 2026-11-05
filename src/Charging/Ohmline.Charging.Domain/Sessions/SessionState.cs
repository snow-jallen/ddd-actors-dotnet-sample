using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// Where a charging session is in its life. A running session is <see cref="Active"/>, not
/// <c>Charging</c>: a type named Charging would collide with the namespace <c>Ohmline.Charging</c>
/// in every other namespace.
/// </summary>
public union SessionState(NoSession, Active, Suspended, Ended)
{
    public static SessionState Initial { get; } = new NoSession();
}

/// <summary>Nothing has started yet.</summary>
public sealed record NoSession;

/// <summary>The session is running, and energy may flow.</summary>
public sealed record Active(SessionTerms Terms, SessionMeter Meter);

/// <summary>Charging is paused; the car is still plugged in.</summary>
public sealed record Suspended(SessionTerms Terms, SessionMeter Meter, DateTimeOffset Since);

/// <summary>Over. Only the terms and the outcome remain.</summary>
public sealed record Ended(SessionTerms Terms, Energy EnergyDelivered, DateTimeOffset EndedAt);
