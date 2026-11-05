using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The driver's journey at the depot's connector 1, as the shell tests see it: its start and
/// the steps it records, all at the fixed morning.
/// </summary>
internal static class TheJourney
{
    public static readonly JourneyId Journey = new(Guid.CreateVersion7(Morning));

    public static readonly SessionId JourneySession = ChargingJourney.SessionOf(Journey);

    public static readonly ConnectorId Connector = new(Charger, 1);

    public static readonly DriverRef Driver = new("drv-7f3a91c2");

    public static readonly Money Hold = new(50m, Currency.Usd);

    public const string HoldId = "hold-8812";

    public static StartCharging Start() =>
        new(Command, Journey, DriversCard, Connector, Depot, new Power(22m), Hold);

    public static JourneyEvent Started(CommandId by) => Stamped(new JourneyStarted(
        Journey, by, JourneySession, DriversCard, Connector, Depot, new Power(22m), Hold));

    public static JourneyEvent TokenSecured() => Stamped(new TokenSecured(Journey, Driver));

    public static JourneyEvent HoldSecured() => Stamped(new HoldSecured(Journey, HoldId));

    public static JourneyEvent PowerSecured() =>
        Stamped(new PowerSecured(Journey, new Power(22m)));

    public static JourneyEvent Stamped(JourneyEvent @event) => @event with { OccurredAt = Morning };
}
