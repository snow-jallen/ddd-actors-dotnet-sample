using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What a charging journey records, one event per step it completes. Written to the journal,
/// so plain data only: no union, no state. The journey's state is the fold of these, and a
/// journey that starts again after a rebalance knows from them where it was.
/// </summary>
public closed record JourneyEvent(JourneyId JourneyId)
{
    /// <summary>When it happened: the time the shell read when the journey decided.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// The journey started, with everything it was started with: a journey recovered from its
/// journal knows it without asking anyone. Its session is named after it, and the command that
/// started it is kept, so that the same command sent again can be recognised.
/// </summary>
public sealed record JourneyStarted(
    JourneyId JourneyId,
    CommandId CommandId,
    SessionId SessionId,
    TokenUid TokenUid,
    ConnectorId ConnectorId,
    SiteId SiteId,
    Power MaxPower,
    Money Hold)
    : JourneyEvent(JourneyId);

/// <summary>Driver Accounts locked the token for the session; the driver's account pays.</summary>
public sealed record TokenSecured(JourneyId JourneyId, DriverRef Driver)
    : JourneyEvent(JourneyId);

/// <summary>Money is held on the driver's card, under the payment port's reference.</summary>
public sealed record HoldSecured(JourneyId JourneyId, string HoldId) : JourneyEvent(JourneyId);

/// <summary>
/// Site Energy answered the request for power: this much now, or none yet when the request is
/// queued, in which case the connector gets its share when the site rebalances.
/// </summary>
public sealed record PowerSecured(JourneyId JourneyId, Power Allocated) : JourneyEvent(JourneyId);

/// <summary>The car draws energy: the session started, under this tariff.</summary>
public sealed record EnergyFlowStarted(JourneyId JourneyId, TariffSnapshot Tariff)
    : JourneyEvent(JourneyId);

/// <summary>The session ended with this much energy delivered, which costs this much.</summary>
public sealed record SettlementDue(JourneyId JourneyId, Energy Delivered, Money Amount)
    : JourneyEvent(JourneyId);

/// <summary>
/// A capture failed, this many times so far; the journey tries again after a pause.
/// </summary>
public sealed record CaptureAttemptFailed(
    JourneyId JourneyId, int Attempt, string Code, string Message)
    : JourneyEvent(JourneyId);

/// <summary>
/// The capture failed for good: the amount is owed, and someone collects it (section 16.3).
/// The compensation for a payment that cannot be taken is a fact, recorded like any other.
/// </summary>
public sealed record DebtRecorded(JourneyId JourneyId, Money Amount, string Reason)
    : JourneyEvent(JourneyId);

/// <summary>The money is taken from the hold. The journey is over.</summary>
public sealed record JourneyCompleted(JourneyId JourneyId, Money Captured)
    : JourneyEvent(JourneyId);

/// <summary>
/// The journey was given up, for a reason from <see cref="AbandonReasons"/>. Recorded before
/// anything is given back, so that a journey recovered from its journal gives it back too.
/// </summary>
public sealed record JourneyAbandoned(JourneyId JourneyId, string Reason, string Detail)
    : JourneyEvent(JourneyId);
