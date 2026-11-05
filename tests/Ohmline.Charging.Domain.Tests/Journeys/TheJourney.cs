using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Journeys;

/// <summary>
/// The primer's driver starting a charge at the depot's connector 1 with her card, step by
/// step: the inputs the journey hears and the events it records, each a minute after the last.
/// </summary>
internal static class TheJourney
{
    public static readonly JourneyId Journey = new(Guid.CreateVersion7(Morning));

    /// <summary>Named after the journey, as every journey's session is.</summary>
    public static readonly SessionId JourneySession = ChargingJourney.SessionOf(Journey);

    public static readonly ConnectorId Connector = new(Charger, 1);

    public static readonly DriverRef Driver = new("drv-7f3a91c2");

    public static readonly Money Hold = new(50m, Currency.Usd);

    public static readonly Power MaxPower = new(22m);

    public const string HoldId = "hold-8812";

    public static readonly JourneyTerms Terms =
        new(Journey, JourneySession, DriversCard, Connector, Depot, MaxPower, Hold);

    /// <summary>The minute of each step: the start at 07:52, the next step at 07:53...</summary>
    public static DateTimeOffset Minute(int step) => Morning.AddMinutes(step);

    public static StartCharging Start { get; } =
        new(Command, Journey, DriversCard, Connector, Depot, MaxPower, Hold);

    public static TokenLocked Locked { get; } =
        new(DriversCard, JourneySession, Driver, Version: 4);

    public static HoldPlaced Placed { get; } = new(HoldId, Hold);

    public static SessionStarted SessionBegan { get; } =
        new SessionStarted(
                JourneySession,
                Depot,
                Connector,
                DriversCard,
                StandardTariff(0.40m),
                Hold,
                MeterStartWh: 1_200_000)
            with { CommandId = Command, OccurredAt = Minute(5) };

    public static SessionEnded SessionOver { get; } =
        new SessionEnded(JourneySession, Depot, Connector, DriversCard, new Energy(30m))
            with { CommandId = Command, OccurredAt = Minute(50) };

    /// <summary>The key of the journey's request at <paramref name="step"/>.</summary>
    public static IdempotencyKey KeyFor(string step) => IdempotencyKey.Of(Journey, step);

    public static JourneyEffect.LockToken AskForLock { get; } =
        new(KeyFor(IdempotencyKey.Lock), DriversCard, JourneySession);

    public static JourneyEffect.PlaceHold AskForHold { get; } =
        new(KeyFor(IdempotencyKey.Hold), Driver, Hold);

    public static JourneyEffect.RequestAllocation AskForPower { get; } =
        new(KeyFor(IdempotencyKey.Allocation), Depot, Connector, JourneySession, MaxPower);

    public static JourneyEffect.StartTimer AwaitReply { get; } =
        new(JourneyTimer.Reply, ChargingJourney.ReplyTimeout);

    public static JourneyEffect.ReleaseAllocation ReleaseThePower { get; } =
        new(KeyFor(IdempotencyKey.Release), Depot, Connector, JourneySession);

    public static JourneyEffect.VoidHold VoidTheHold { get; } = new(KeyFor(IdempotencyKey.Hold));

    public static JourneyEffect.UnlockToken UnlockTheToken { get; } =
        new(KeyFor(IdempotencyKey.Unlock), DriversCard, JourneySession);

    public static JourneyEffect.StartCharger StartChargerAt(Power limit) =>
        new(KeyFor(IdempotencyKey.Charger), Connector, DriversCard, JourneySession, limit);

    public static JourneyEffect.CapturePayment Capture(Money amount) =>
        new(KeyFor(IdempotencyKey.Capture), HoldId, amount);

    /// <summary>The events of the journey up to each step, as the journey records them.</summary>
    public static ImmutableArray<JourneyEvent> History(int steps) =>
    [
        .. new JourneyEvent[]
            {
                new JourneyStarted(
                    Journey,
                    Command,
                    JourneySession,
                    DriversCard,
                    Connector,
                    Depot,
                    MaxPower,
                    Hold),
                new TokenSecured(Journey, Driver),
                new HoldSecured(Journey, HoldId),
                new PowerSecured(Journey, new Power(22m)),
                new EnergyFlowStarted(Journey, StandardTariff(0.40m)),
                new SettlementDue(Journey, new Energy(30m), new Money(12m, Currency.Usd)),
            }
            .Take(steps)
            .Select((@event, step) => @event with { OccurredAt = Minute(step) }),
    ];

    /// <summary>Where the journey is after the first <paramref name="steps"/> steps.</summary>
    public static JourneyState After(int steps) => ChargingJourney.Fold(History(steps));

    /// <summary>
    /// Given the journey after so many steps, when it hears <paramref name="input"/> at the next
    /// minute.
    /// </summary>
    public static JourneyStep When(int steps, JourneyInput input) =>
        ChargingJourney.Next(After(steps), input, new DecisionContext(Minute(steps)));

    extension(JourneyEvent @event)
    {
        /// <summary>The event as the journey records it at <paramref name="time"/>.</summary>
        public JourneyEvent At(DateTimeOffset time) => @event with { OccurredAt = time };
    }
}
