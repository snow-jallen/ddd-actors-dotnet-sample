using System.Collections.Immutable;
using System.Globalization;
using Ohmline.Charging.Domain.Policies;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Policies;

/// <summary>
/// Every policy against every kind of session event, as one decision table: the lilac stickies
/// of the session's board, read across. A policy reacts to its own event and to nothing else.
/// Times are UTC on the depot's weekday morning.
/// </summary>
public sealed class PolicyTableTests
{
    private static readonly ConnectorId Connector = new(Charger, 1);
    private static readonly IdleFee Idle = new(Usd(0.40m), TimeSpan.FromMinutes(15));
    private static readonly DateTimeOffset Full = Morning.AddMinutes(48);

    public static readonly TheoryData<string, string, string> Reactions = new()
    {
        // Policy                     Whenever                 Then
        { "release on session end",  "session started",       "nothing" },
        { "release on session end",  "meter value recorded",  "nothing" },
        { "release on session end",  "meter anomaly",         "nothing" },
        { "release on session end",  "suspended by EV",       "nothing" },
        { "release on session end",  "suspended by charger",  "nothing" },
        { "release on session end",  "resumed",               "nothing" },
        { "release on session end",  "spend cap reached",     "nothing" },
        { "release on session end",  "session ended",         "release allocation; unlock token" },
        { "spend cap",               "session started",       "nothing" },
        { "spend cap",               "meter value recorded",  "nothing" },
        { "spend cap",               "meter anomaly",         "nothing" },
        { "spend cap",               "suspended by EV",       "nothing" },
        { "spend cap",               "suspended by charger",  "nothing" },
        { "spend cap",               "resumed",               "nothing" },
        { "spend cap",               "spend cap reached",     "stop charging" },
        { "spend cap",               "session ended",         "nothing" },
        { "idle fee",                "session started",       "nothing" },
        { "idle fee",                "meter value recorded",  "nothing" },
        { "idle fee",                "meter anomaly",         "nothing" },
        { "idle fee",                "suspended by EV",       "warn of idle fee from 08:55" },
        { "idle fee",                "suspended by charger",  "nothing" },
        { "idle fee",                "resumed",               "nothing" },
        { "idle fee",                "spend cap reached",     "nothing" },
        { "idle fee",                "session ended",         "nothing" },
    };

    [Theory]
    [MemberData(nameof(Reactions))]
    public void Whenever_a_session_event_happens_then_each_policy_reacts_only_to_its_own(
        string policy, string happened, string intents)
    {
        Describe(Policy(policy).React(Event(happened))).ShouldBe(intents);
    }

    [Fact]
    public void Given_the_table_then_it_has_every_policy_and_every_kind_of_session_event()
    {
        var policies = typeof(ISessionPolicy).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(ISessionPolicy)) && !type.IsInterface
                           && type != typeof(SessionPolicies));
        var events = typeof(SessionEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SessionEvent)) && !type.IsAbstract);
        var rows = Reactions.Select(row => (Policy: row.Data.Item1, Event: row.Data.Item2))
            .ToList();

        rows.Select(row => Policy(row.Policy).GetType()).Distinct()
            .ShouldBe(policies, ignoreOrder: true);
        rows.Select(row => Event(row.Event).GetType()).Distinct()
            .ShouldBe(events, ignoreOrder: true);
        rows.Count.ShouldBe(policies.Count() * events.Count());
    }

    private static ISessionPolicy Policy(string name) => name switch
    {
        "release on session end" => new ReleaseOnSessionEnded(),
        "spend cap" => new SpendCapPolicy(),
        "idle fee" => new IdleFeePolicy(Idle),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>One event of each kind, from the primer's charge (full at 08:40).</summary>
    private static SessionEvent Event(string name)
    {
        SessionEvent happened = name switch
        {
            "session started" => new SessionStarted(
                Session, Depot, Connector, DriversCard, StandardTariff(0.42m), Usd(40m), 0),
            "meter value recorded" => new MeterValueRecorded(Session, 27_600, Full),
            "meter anomaly" => new MeterAnomalyDetected(Session, 50, Full, 27_600, Full),
            "suspended by EV" => new ChargingSuspendedByEv(Session),
            "suspended by charger" => new ChargingSuspendedByCharger(Session),
            "resumed" => new ChargingResumed(Session),
            "spend cap reached" => new SpendCapReached(Session, Usd(40m), Usd(40m)),
            "session ended" => new SessionEnded(
                Session, Depot, Connector, DriversCard, new Energy(27.6m)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

        return happened with { OccurredAt = Full };
    }

    private static string Describe(ImmutableArray<ChargingIntent> intents) =>
        intents.IsEmpty
            ? "nothing"
            : string.Join("; ", intents.Select(intent => intent switch
            {
                ChargingIntent.ReleaseSiteAllocation => "release allocation",
                ChargingIntent.UnlockToken => "unlock token",
                ChargingIntent.StopCharging => "stop charging",
                ChargingIntent.WarnOfIdleFee warning => "warn of idle fee from "
                    + warning.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                null => "a default intent",
            }));

    private static Money Usd(decimal amount) => new(amount, Currency.Usd);
}
