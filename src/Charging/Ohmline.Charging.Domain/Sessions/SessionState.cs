using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// Where a charging session is in its life. A running session is <see cref="Active"/>, not
/// <c>Charging</c>: a type named Charging would collide with the namespace <c>Ohmline.Charging</c>
/// in every other namespace.
/// </summary>
public union SessionState(NoSession, Active, SuspendedByEv, SuspendedByCharger, Ended)
{
    public static SessionState Initial { get; } = new NoSession();
}

/// <summary>Nothing has started yet.</summary>
public sealed record NoSession;

/// <summary>The session is running, and energy may flow.</summary>
public sealed record Active(SessionTerms Terms, SessionMeter Meter);

/// <summary>
/// The car has stopped charging and is still plugged in. Since then the driver has been idle:
/// once the grace period is over, the idle fee applies.
/// </summary>
public sealed record SuspendedByEv(SessionTerms Terms, SessionMeter Meter, DateTimeOffset Since);

/// <summary>
/// The charge point has stopped charging at Ohmline's request, such as during a curtailment.
/// The driver is waiting for Ohmline, not idle, so no idle fee applies.
/// </summary>
public sealed record SuspendedByCharger(
    SessionTerms Terms, SessionMeter Meter, DateTimeOffset Since);

/// <summary>Over. Only the terms and the outcome remain.</summary>
public sealed record Ended(SessionTerms Terms, Energy EnergyDelivered, DateTimeOffset EndedAt);
