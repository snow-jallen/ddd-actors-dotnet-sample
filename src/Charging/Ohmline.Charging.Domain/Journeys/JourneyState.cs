using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// Where a charging journey is, from "start charging" to a settled payment, and what it holds
/// there. Each step a journey waits in remembers when it began.
/// </summary>
public union JourneyState(
    NotStarted,
    AwaitingTokenLock,
    AwaitingHold,
    AwaitingAllocation,
    AwaitingEnergyFlow,
    Charging,
    Settling,
    Completed,
    Abandoned)
{
    public static JourneyState Initial { get; } = new NotStarted();

    /// <summary>What the journey was started with; none before it starts.</summary>
    public JourneyTerms? Terms => this switch
    {
        NotStarted => null,
        AwaitingTokenLock waiting => waiting.Terms,
        AwaitingHold waiting => waiting.Terms,
        AwaitingAllocation waiting => waiting.Terms,
        AwaitingEnergyFlow waiting => waiting.Terms,
        Charging charging => charging.Terms,
        Settling settling => settling.Terms,
        Completed completed => completed.Terms,
        Abandoned abandoned => abandoned.From.Terms,
        null => null,
    };
}

/// <summary>Nothing has been asked for yet.</summary>
public sealed record NotStarted;

/// <summary>Driver Accounts is asked to lock the token for the journey's session.</summary>
public sealed record AwaitingTokenLock(JourneyTerms Terms, DateTimeOffset Since);

/// <summary>The token is locked; the payment port is asked to hold money on the card.</summary>
public sealed record AwaitingHold(JourneyTerms Terms, DriverRef Driver, DateTimeOffset Since);

/// <summary>The money is held; Site Energy is asked for power for the connector.</summary>
public sealed record AwaitingAllocation(JourneyTerms Terms, string HoldId, DateTimeOffset Since);

/// <summary>
/// The site has answered; the charger is told to start, with the power allocated (none when the
/// request is queued), and the journey waits for the car to draw energy.
/// </summary>
public sealed record AwaitingEnergyFlow(
    JourneyTerms Terms, string HoldId, Power Allocated, DateTimeOffset Since);

/// <summary>The session has started under this tariff; the journey waits for it to end.</summary>
public sealed record Charging(JourneyTerms Terms, string HoldId, TariffSnapshot Tariff);

/// <summary>
/// The session is over; the payment port is asked to capture what it cost, and asked again,
/// after a pause, each time it fails.
/// </summary>
public sealed record Settling(
    JourneyTerms Terms, string HoldId, Money Amount, int FailedCaptures = 0);

/// <summary>
/// Nothing more happens: the money is taken, or, when it could not be, it is owed.
/// </summary>
public sealed record Completed(JourneyTerms Terms, Money Captured, Money? Owed = null);

/// <summary>
/// Given up, in the step it was in, for a reason with a stable code. What the journey had
/// obtained by then is given back.
/// </summary>
public sealed record Abandoned(JourneyState From, string Reason, string Detail);
