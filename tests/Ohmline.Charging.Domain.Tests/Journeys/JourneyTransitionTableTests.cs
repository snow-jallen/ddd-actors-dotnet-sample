using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using static Ohmline.Charging.Domain.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Domain.Tests.Known;
using ChargingState = Ohmline.Charging.Domain.Journeys.Charging;

namespace Ohmline.Charging.Domain.Tests.Journeys;

/// <summary>
/// The journey's transition table, read across (section 15.7): in this step, when this happens,
/// the journey goes there and does that. Time is a value here, not a clock: every row is decided
/// ten seconds after its step began, when a reply timer first elapses and no step has waited
/// too long. A timer is just another input, so a time-driven process needs no scheduler to be
/// tested.
/// </summary>
public sealed class JourneyTransitionTableTests
{
    private static readonly Money Twelve = new(12m, Currency.Usd);

    public static readonly TheoryData<string, string, string, string> Transitions = new()
    {
        // In this step, when this happens, then this step, doing this.
        { "not started", "start charging", "awaiting token lock", "lock token; start reply timer" },
        { "not started", "session started", "not started", "nothing" },
        { "awaiting token lock", "token locked", "awaiting hold", "place hold; start reply timer" },
        { "awaiting token lock", "token lock refused", "abandoned", "unlock token" },
        {
            "awaiting token lock", "reply timer", "awaiting token lock",
            "lock token; start reply timer"
        },
        { "awaiting token lock", "start charging", "awaiting token lock", "nothing" },
        {
            "awaiting hold", "hold placed", "awaiting allocation",
            "request power; start reply timer"
        },
        { "awaiting hold", "hold declined", "abandoned", "void hold; unlock token" },
        { "awaiting hold", "hold voided", "awaiting hold", "nothing" },
        { "awaiting hold", "reply timer", "awaiting hold", "place hold; start reply timer" },
        {
            "awaiting allocation", "power allocated", "awaiting energy flow",
            "cancel reply timer; start charger; start energy flow timer"
        },
        {
            "awaiting allocation", "queued for power", "awaiting energy flow",
            "cancel reply timer; start charger; start energy flow timer"
        },
        {
            "awaiting allocation", "allocation refused", "abandoned",
            "release power; void hold; unlock token"
        },
        {
            "awaiting allocation", "reply timer", "awaiting allocation",
            "request power; start reply timer"
        },
        { "awaiting energy flow", "charger accepted", "awaiting energy flow", "nothing" },
        {
            "awaiting energy flow", "charger rejected", "abandoned",
            "cancel energy flow timer; release power; void hold; unlock token"
        },
        {
            "awaiting energy flow", "energy flow timer", "abandoned",
            "cancel energy flow timer; release power; void hold; unlock token"
        },
        { "awaiting energy flow", "session started", "charging", "cancel energy flow timer" },
        { "awaiting energy flow", "reply timer", "awaiting energy flow", "nothing" },
        { "charging", "session ended", "settling", "capture" },
        { "charging", "energy flow timer", "charging", "nothing" },
        { "settling", "payment captured", "completed", "nothing" },
        { "settling", "capture failed", "settling", "start capture retry timer" },
        { "settling", "capture retry timer", "settling", "capture" },
        { "completed", "token locked", "completed", "nothing" },
        { "completed", "session ended", "completed", "nothing" },
        { "abandoned", "session started", "abandoned", "nothing" },
        { "abandoned", "power allocated", "abandoned", "nothing" },
    };

    [Theory]
    [MemberData(nameof(Transitions))]
    public void In_a_step_when_something_happens_then_the_journey_moves_and_acts(
        string step, string happens, string then, string doing)
    {
        var (state, at) = Step(step);

        var next = ChargingJourney.Next(state, Input(happens), new DecisionContext(at));

        (Name(next.State), Describe(next.Effects)).ShouldBe((then, doing));
    }

    [Fact]
    public void Given_the_table_then_every_step_and_every_kind_of_input_is_in_it()
    {
        var rows = Transitions.Select(row => row.Data).ToList();

        rows.Select(row => Step(row.Item1).State.Value!.GetType()).Distinct()
            .ShouldBe(CasesOf<JourneyState>(), ignoreOrder: true);
        rows.Select(row => Input(row.Item2).Value!.GetType()).Distinct()
            .ShouldBe(CasesOf<JourneyInput>(), ignoreOrder: true);
    }

    /// <summary>
    /// The journey in a step, and ten seconds after the step began: step <c>n</c> of the
    /// history began at minute <c>n - 1</c>.
    /// </summary>
    private static (JourneyState State, DateTimeOffset At) Step(string name) => name switch
    {
        "not started" => (After(0), Minute(0)),
        "awaiting token lock" => (After(1), TenSecondsInto(1)),
        "awaiting hold" => (After(2), TenSecondsInto(2)),
        "awaiting allocation" => (After(3), TenSecondsInto(3)),
        "awaiting energy flow" => (After(4), TenSecondsInto(4)),
        "charging" => (After(5), TenSecondsInto(5)),
        "settling" => (After(6), TenSecondsInto(6)),
        "completed" => (Then(new JourneyCompleted(Journey, Twelve)), TenSecondsInto(7)),
        "abandoned" => (
            ChargingJourney.Evolve(
                After(1), new JourneyAbandoned(Journey, AbandonReasons.TokenRefused, "Blocked.")),
            TenSecondsInto(2)),
        _ => throw new ArgumentException($"No step '{name}'.", nameof(name)),
    };

    private static DateTimeOffset TenSecondsInto(int step) =>
        Minute(step - 1) + ChargingJourney.ReplyTimeout;

    private static JourneyState Then(JourneyEvent @event) =>
        ChargingJourney.Evolve(After(6), @event.At(Minute(6)));

    private static JourneyInput Input(string name) => name switch
    {
        "start charging" => Start,
        "token locked" => Locked,
        "token lock refused" =>
            new TokenLockRefused(DriversCard, JourneySession, "token-is-blocked", "Blocked."),
        "hold placed" => Placed,
        "hold declined" => new HoldDeclined("insufficient-funds", "Declined."),
        "hold voided" => new HoldVoided(HoldId),
        "power allocated" =>
            new PowerAllocated(Depot, Connector, JourneySession, new Power(22m), Version: 7),
        "queued for power" => new QueuedForPower(Depot, Connector, JourneySession, Version: 7),
        "allocation refused" => new BudgetCommandRejected("unknown-connector", "Unknown."),
        "charger accepted" => new ChargerAccepted(Connector),
        "charger rejected" => new ChargerRejected(Connector, "Occupied."),
        "session started" => SessionBegan,
        "session ended" => SessionOver,
        "payment captured" => new PaymentCaptured(HoldId, Twelve),
        "capture failed" => new CaptureFailed(HoldId, "provider-unavailable", "No answer."),
        "energy flow timer" => new JourneyTimerElapsed(JourneyTimer.EnergyFlow),
        "capture retry timer" => new JourneyTimerElapsed(JourneyTimer.CaptureRetry),
        "reply timer" => new JourneyTimerElapsed(JourneyTimer.Reply),
        _ => throw new ArgumentException($"No input '{name}'.", nameof(name)),
    };

    private static string Name(JourneyState state) => state switch
    {
        NotStarted => "not started",
        AwaitingTokenLock => "awaiting token lock",
        AwaitingHold => "awaiting hold",
        AwaitingAllocation => "awaiting allocation",
        AwaitingEnergyFlow => "awaiting energy flow",
        ChargingState => "charging",
        Settling => "settling",
        Completed => "completed",
        Abandoned => "abandoned",
        null => "default",
    };

    private static string Describe(IEnumerable<JourneyEffect> effects) =>
        string.Join("; ", effects.Select(Describe)) is { Length: > 0 } described
            ? described
            : "nothing";

    private static string Describe(JourneyEffect effect) => effect switch
    {
        JourneyEffect.LockToken => "lock token",
        JourneyEffect.PlaceHold => "place hold",
        JourneyEffect.RequestAllocation => "request power",
        JourneyEffect.StartCharger => "start charger",
        JourneyEffect.StartTimer start => $"start {Words(start.Timer)} timer",
        JourneyEffect.CancelTimer cancel => $"cancel {Words(cancel.Timer)} timer",
        JourneyEffect.ReleaseAllocation => "release power",
        JourneyEffect.VoidHold => "void hold",
        JourneyEffect.CapturePayment => "capture",
        JourneyEffect.UnlockToken => "unlock token",
        null => "default",
    };

    private static string Words(JourneyTimer timer) => timer switch
    {
        JourneyTimer.EnergyFlow => "energy flow",
        JourneyTimer.CaptureRetry => "capture retry",
        JourneyTimer.Reply => "reply",
        _ => timer.ToString(),
    };

    /// <summary>The case types of a union, from its constructors: one per case.</summary>
    private static IEnumerable<Type> CasesOf<TUnion>() =>
        typeof(TUnion).GetConstructors()
            .Select(constructor => constructor.GetParameters())
            .Where(parameters => parameters is [_])
            .Select(parameters => parameters[0].ParameterType);
}
