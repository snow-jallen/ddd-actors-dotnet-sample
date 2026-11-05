using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What the journey wants done next, as data: the shell carries each effect out, through
/// another context's shard region, a port or a timer, once the step that caused it is stored.
/// A union, because effects stay in the process: decided, carried out, never persisted.
/// </summary>
public union JourneyEffect(
    JourneyEffect.LockToken,
    JourneyEffect.PlaceHold,
    JourneyEffect.RequestAllocation,
    JourneyEffect.StartCharger,
    JourneyEffect.StartTimer,
    JourneyEffect.CancelTimer,
    JourneyEffect.ReleaseAllocation,
    JourneyEffect.VoidHold,
    JourneyEffect.CapturePayment,
    JourneyEffect.UnlockToken)
{
    /// <summary>Ask Driver Accounts to lock the token for the session.</summary>
    public sealed record LockToken(TokenUid TokenUid, SessionId SessionId);

    /// <summary>Ask the payment port to hold this much on the driver's card.</summary>
    public sealed record PlaceHold(DriverRef Driver, Money Amount);

    /// <summary>Ask Site Energy for power for the session's connector.</summary>
    public sealed record RequestAllocation(
        SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId, Power Requested);

    /// <summary>Tell the charge point to start the session, drawing at most the limit.</summary>
    public sealed record StartCharger(
        ConnectorId ConnectorId, TokenUid TokenUid, SessionId SessionId, Power Limit);

    /// <summary>Start the timer, or start it again: it elapses after the delay.</summary>
    public sealed record StartTimer(JourneyTimer Timer, TimeSpan Delay);

    /// <summary>Stop the timer, if it runs.</summary>
    public sealed record CancelTimer(JourneyTimer Timer);

    /// <summary>Give the session's power back to the site.</summary>
    public sealed record ReleaseAllocation(
        SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId);

    /// <summary>Release the money held on the card, taking nothing.</summary>
    public sealed record VoidHold(string HoldId);

    /// <summary>Take this much from the hold.</summary>
    public sealed record CapturePayment(string HoldId, Money Amount);

    /// <summary>Let the token start another session.</summary>
    public sealed record UnlockToken(TokenUid TokenUid, SessionId SessionId);
}
