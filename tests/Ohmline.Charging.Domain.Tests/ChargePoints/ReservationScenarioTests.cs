using System.Collections.Immutable;
using System.Globalization;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

/// <summary>
/// The reservation rules as the operations lead explains them: one row per situation, in the
/// glossary's words. Connector 1 of the depot's charge point on a weekday morning (UTC). The
/// driver's reservation was made at 07:52, so it holds the connector until 08:07; "other" is
/// another driver's token.
/// </summary>
public sealed class ReservationScenarioTests
{
    private static readonly DateTimeOffset Expiry = Morning.AddMinutes(15);

    public static readonly TheoryData<string, string, string> Reservations = new()
    {
        // Connector      What happens                Outcome
        { "available",   "driver reserves at 07:52", "reserved for driver until 08:07" },
        { "reserved",    "driver reserves at 07:55", "nothing changes" },
        { "reserved",    "other reserves at 07:55",  "refused: reserved until 08:07" },
        { "reserved",    "other reserves at 08:06",  "refused: reserved until 08:07" },
        { "reserved",    "other reserves at 08:07",  "expired; reserved for other until 08:22" },
        { "reserved",    "driver reserves at 08:10", "expired; reserved for driver until 08:25" },
        { "reserved",    "driver cancels at 08:00",  "cancelled" },
        { "reserved",    "other cancels at 08:00",   "refused: reserved until 08:07" },
        { "available",   "driver cancels at 08:00",  "nothing changes" },
        { "reserved",    "timer fires at 08:06",     "nothing changes" },
        { "reserved",    "timer fires at 08:07",     "expired" },
        { "available",   "timer fires at 08:07",     "nothing changes" },
        { "reserved",    "driver plugs in at 08:00", "occupied by driver" },
        { "reserved",    "driver plugs in at 08:10", "occupied by driver" },
        { "reserved",    "other plugs in at 08:00",  "refused: reserved until 08:07" },
        { "reserved",    "other plugs in at 08:10",  "expired; occupied by other" },
        { "occupied",    "other reserves at 08:00",  "refused: unavailable" },
        { "faulted",     "driver reserves at 08:00", "refused: faulted" },
        { "unavailable", "driver reserves at 08:00", "refused: unavailable" },
    };

    [Theory]
    [MemberData(nameof(Reservations))]
    public void A_reservation_holds_a_connector_for_one_token_until_it_expires(
        string connector, string happening, string outcome)
    {
        var (command, at) = Happening(happening);

        var decision = ChargePointSpec.Given(History(connector)).At(at).When(command);

        Describe(decision.Result).ShouldBe(outcome);
    }

    private static ChargePointEvent[] History(string connector)
    {
        ChargePointEvent[] since = connector switch
        {
            "available" => [],
            "reserved" => [new ConnectorReserved(Charger, 1, DriversCard, Expiry)],
            "occupied" => [new ConnectorOccupied(Charger, 1, Session, DriversCard)],
            "faulted" => [new ConnectorFaulted(Charger, 1, "GroundFailure")],
            "unavailable" => [new ConnectorMadeUnavailable(Charger, 1)],
            _ => throw new ArgumentOutOfRangeException(nameof(connector)),
        };

        return [new ChargePointRegistered(Charger, Depot, [new(50m)]), .. since];
    }

    /// <summary>"other plugs in at 08:10": who, what and when.</summary>
    private static (ChargePointCommand Command, DateTimeOffset At) Happening(string happening)
    {
        var at = happening.LastIndexOf(" at ", StringComparison.Ordinal);
        var time = TimeOnly.ParseExact(
            happening[(at + 4)..], "HH:mm", CultureInfo.InvariantCulture);
        var when = new DateTimeOffset(DateOnly.FromDateTime(Morning.Date), time, TimeSpan.Zero);

        ChargePointCommand command = happening[..at] switch
        {
            "driver reserves" => new ReserveConnector(Command, Charger, 1, DriversCard),
            "other reserves" => new ReserveConnector(Command, Charger, 1, SomeoneElsesCard),
            "driver cancels" => new CancelReservation(Command, Charger, 1, DriversCard),
            "other cancels" => new CancelReservation(Command, Charger, 1, SomeoneElsesCard),
            "timer fires" => new ExpireReservation(Command, Charger, 1),
            "driver plugs in" => new OccupyConnector(Command, Charger, 1, Session, DriversCard),
            "other plugs in" =>
                new OccupyConnector(Command, Charger, 1, NextSession, SomeoneElsesCard),
            var unknown => throw new ArgumentOutOfRangeException(nameof(happening), unknown, null),
        };

        return (command, when);
    }

    /// <summary>The decision in the table's words.</summary>
    private static string Describe(
        Result<ImmutableArray<ChargePointEvent>, ChargePointError> result) =>
        result switch
        {
            Ok<ImmutableArray<ChargePointEvent>> { Value: [] } => "nothing changes",
            Ok<ImmutableArray<ChargePointEvent>> accepted =>
                string.Join("; ", accepted.Value.Select(Describe)),
            Err<ChargePointError> refused => "refused: " + refused.Error switch
            {
                ChargePointError.ReservedForAnotherToken held =>
                    $"reserved until {Time(held.Until)}",
                ChargePointError.ConnectorUnavailable => "unavailable",
                ChargePointError.ConnectorFaulted => "faulted",
                var other => other.Message,
            },
            null => "a default Result",
        };

    private static string Describe(ChargePointEvent @event) => @event switch
    {
        ConnectorReserved reserved =>
            $"reserved for {Who(reserved.TokenUid)} until {Time(reserved.ExpiresAt)}",
        ReservationExpired => "expired",
        ReservationCancelled => "cancelled",
        ConnectorOccupied occupied => $"occupied by {Who(occupied.TokenUid)}",
        var other => other.GetType().Name,
    };

    private static string Who(TokenUid token) => token == DriversCard ? "driver" : "other";

    private static string Time(DateTimeOffset time) =>
        time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
