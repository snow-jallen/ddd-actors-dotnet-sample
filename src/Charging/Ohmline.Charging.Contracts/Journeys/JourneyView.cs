using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Journeys;

/// <summary>
/// The answer to <see cref="GetJourney"/>, at <paramref name="Version"/>. <paramref name="Step"/>
/// is one of the constants below; a journey that never started has version 0 and no session. An
/// abandoned journey says why, with a stable code and a sentence for people.
/// </summary>
public sealed record JourneyView(
    JourneyId JourneyId,
    long Version,
    string Step,
    SessionId? SessionId = null,
    string? AbandonReason = null,
    string? Detail = null)
{
    public const string NotStarted = "not-started";
    public const string AwaitingTokenLock = "awaiting-token-lock";
    public const string AwaitingHold = "awaiting-hold";
    public const string AwaitingAllocation = "awaiting-allocation";
    public const string AwaitingEnergyFlow = "awaiting-energy-flow";
    public const string Charging = "charging";
    public const string Settling = "settling";
    public const string Completed = "completed";
    public const string Abandoned = "abandoned";
}
