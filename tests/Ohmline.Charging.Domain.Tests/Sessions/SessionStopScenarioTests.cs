using System.Collections.Immutable;
using System.Globalization;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// Stopping a session, as the operations lead explains it: a session ends once, with the energy
/// it delivered, whatever it was doing when it was stopped. One row per situation, in the
/// glossary's words. The meter read 1,200 kWh when the session started.
/// </summary>
public sealed class SessionStopScenarioTests
{
    private const long MeterStartWh = 1_200_000;

    private static readonly ConnectorId Connector = new(Charger, 1);

    public static readonly TheoryData<string, decimal, string> Stops = new()
    {
        // The session is                  Energy so far (kWh)  Outcome
        { "not started",                   0m,                  "refused: not started" },
        { "active",                        0m,                  "ended with 0 kWh" },
        { "active",                        27.6m,               "ended with 27.6 kWh" },
        { "active after a meter anomaly",  15m,                 "ended with 15 kWh" },
        { "suspended by EV",               27.6m,               "ended with 27.6 kWh" },
        { "suspended by charger",          12.5m,               "ended with 12.5 kWh" },
        { "ended",                         20m,                 "nothing changes" },
    };

    [Theory]
    [MemberData(nameof(Stops))]
    public void Stopping_a_session_ends_it_once_with_the_energy_delivered(
        string session, decimal kilowattHours, string outcome)
    {
        var decision = SessionSpec.Given(History(session, kilowattHours))
            .At(Morning.AddMinutes(73))
            .When(new StopSession(Command, Session));

        Describe(decision.Result).ShouldBe(outcome);
    }

    private static SessionEvent[] History(string session, decimal kilowattHours)
    {
        var started = new SessionStarted(
            Session, Depot, Connector, DriversCard, StandardTariff(0.42m),
            new Money(40m, Currency.Usd), MeterStartWh);
        var read = Morning.AddMinutes(48);
        var registerWh = MeterStartWh + (long)(kilowattHours * 1000m);
        var reading = new MeterValueRecorded(Session, Energy.FromWattHours(registerWh), read);

        return session switch
        {
            "not started" => [],
            "active" => [started, reading],
            "active after a meter anomaly" =>
                [started, reading, new MeterAnomalyDetected(Session, 50, read.AddMinutes(1),
                    registerWh, read)],
            "suspended by EV" => [started, reading, new ChargingSuspendedByEv(Session)],
            "suspended by charger" => [started, reading, new ChargingSuspendedByCharger(Session)],
            "ended" =>
                [started, reading, new SessionEnded(
                    Session, Depot, Connector, DriversCard, new Energy(kilowattHours))],
            _ => throw new ArgumentOutOfRangeException(nameof(session)),
        };
    }

    /// <summary>The decision in the table's words.</summary>
    private static string Describe(Result<ImmutableArray<SessionEvent>, SessionError> result) =>
        result switch
        {
            Ok<ImmutableArray<SessionEvent>> { Value: [] } => "nothing changes",
            Ok<ImmutableArray<SessionEvent>> { Value: [SessionEnded ended] } =>
                $"ended with {ended.EnergyDelivered.KilowattHours.ToString(
                    "0.###", CultureInfo.InvariantCulture)} kWh",
            Ok<ImmutableArray<SessionEvent>> accepted =>
                string.Join("; ", accepted.Value.Select(@event => @event.GetType().Name)),
            Err<SessionError> refused => "refused: " + refused.Error switch
            {
                SessionError.NotStarted => "not started",
                var other => other.Message,
            },
            null => "a default Result",
        };
}
