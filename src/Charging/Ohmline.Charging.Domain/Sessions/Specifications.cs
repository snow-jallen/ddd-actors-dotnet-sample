using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Specifications;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// The session's rules as patterns: static predicates the compiler checks, cheap enough for
/// Decide, Evolve and policies. Where a rule must be a value (handed to a projection or a
/// router, logged, combined), <see cref="IdleFeeApplies"/> and
/// <see cref="RequiresPreAuthorization"/> wrap the same predicates, so each rule is written once.
/// </summary>
public static class SessionPredicates
{
    /// <summary>
    /// The idle fee applies at a moment when the car has stopped charging, the tariff has an
    /// idle fee, and its grace period, as the session's pricing model applies it, has run out.
    /// Never when the charge point stopped it.
    /// </summary>
    public static bool IdleFeeApplies(SessionState session, DateTimeOffset at) =>
        session is SuspendedByEv { Since: var since, Terms.IdleFee.GracePeriod: var grace }
        && at >= since + grace;

    /// <summary>
    /// A session needs a hold on the driver's card before it starts, unless its tariff charges
    /// nothing at all.
    /// </summary>
    public static bool RequiresPreAuthorization(StartSession start) =>
        start.Tariff.Components.Any(component => component is not (
            EnergyRate { PricePerKwh.Amount: 0m }
            or TimeRate { PricePerMinute.Amount: 0m }
            or SessionFee { Amount.Amount: 0m }
            or IdleFee { PricePerMinute.Amount: 0m }));

    /// <summary>
    /// A history that started and ended with nothing delivered: its hold is voided rather than
    /// captured (policy P7 in <c>docs/eventstorming/process-settle-session.md</c>).
    /// </summary>
    public static bool EndedWithoutEnergy(IReadOnlyList<SessionEvent> history) =>
        history is [SessionStarted, .., SessionEnded { EnergyDelivered.KilowattHours: 0m }];
}

/// <summary>The idle-fee rule at a given moment, as a value.</summary>
public sealed record IdleFeeApplies(DateTimeOffset At) : ISpecification<SessionState>
{
    public bool IsSatisfiedBy(SessionState session) =>
        SessionPredicates.IdleFeeApplies(session, At);
}

/// <summary>The pre-authorization rule, as a value.</summary>
public sealed record RequiresPreAuthorization : ISpecification<StartSession>
{
    public bool IsSatisfiedBy(StartSession start) =>
        SessionPredicates.RequiresPreAuthorization(start);
}
