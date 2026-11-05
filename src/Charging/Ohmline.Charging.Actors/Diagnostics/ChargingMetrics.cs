using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Ohmline.ActorShell.Tracing;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Diagnostics;

/// <summary>
/// Charging's instruments (section 18.1), on the meter the host's <see cref="IMeterFactory"/>
/// makes: the commands its aggregates decide and refuse, the sessions that start, and the energy
/// they deliver, which is what the business sells. One per host, from the container, like the
/// clock; recorded by the actors through the shell's hooks, never by the domain.
/// </summary>
/// <remarks>
/// <para>
/// The cardinality budget. Every series a backend keeps is one combination of tag values, so
/// every tag value comes from a short list the code itself names: an aggregate's durable type
/// name (three), a command's type name (about twenty), and an error union's case name (about
/// fifteen). A few hundred series at most, however many charge points, sessions and drivers
/// there are.
/// </para>
/// <para>
/// Never an id. A charge point's id, a session's, a token's or an actor's path would make a
/// series per charge point, per session or per driver, a number that grows with the business
/// and never shrinks (branch <c>wrong/ch18-s01-high-cardinality-tags</c>). Ids belong on spans
/// and in logs, where each one is a single record.
/// </para>
/// </remarks>
public sealed class ChargingMetrics
{
    public const string Commands = "ohmline.charging.commands";
    public const string CommandsRejected = "ohmline.charging.commands.rejected";
    public const string SessionsStarted = "ohmline.charging.sessions.started";
    public const string EnergyDelivered = "ohmline.charging.energy.delivered";
    public const string TimeToEnergyFlow = "ohmline.charging.journey.time_to_energy_flow";

    /// <summary>The aggregate's durable type name, such as <c>charge-point</c>.</summary>
    public const string AggregateTag = "aggregate.type";

    /// <summary>The command's type name, such as <c>ReserveConnector</c>.</summary>
    public const string CommandTag = "command.type";

    /// <summary>The case of the error union, such as <c>ConnectorUnavailable</c>.</summary>
    public const string ReasonTag = "reason";

    /// <summary>
    /// How a journey's wait for energy ended: <see cref="EnergyFlowing"/>, or the code it was
    /// abandoned with (<c>AbandonReasons</c>, six of them).
    /// </summary>
    public const string OutcomeTag = "outcome";

    public const string EnergyFlowing = "energy-flowing";

    /// <summary>
    /// Per session, in kWh: a topped-up city car at the bottom, a bus at the depot at the top.
    /// The default boundaries are made for milliseconds and would put every session in one or
    /// two buckets.
    /// </summary>
    private static readonly double[] EnergyBoundaries = [1, 2, 5, 10, 20, 30, 40, 60, 80, 120];

    /// <summary>
    /// In seconds, around the objective of 60 s (section 18.9), which must be a boundary for the
    /// SLI to be counted exactly; 120 s is the journey's energy-flow timeout.
    /// </summary>
    private static readonly double[] WaitBoundaries = [5, 10, 15, 20, 30, 45, 60, 90, 120, 180];

    private readonly Counter<long> _commands;
    private readonly Counter<long> _rejected;
    private readonly Counter<long> _sessionsStarted;
    private readonly Histogram<double> _energyDelivered;
    private readonly Histogram<double> _timeToEnergyFlow;

    public ChargingMetrics(IMeterFactory meters)
        : this(ChargingDiagnostics.CreateMeter(meters))
    {
    }

    private ChargingMetrics(Meter meter)
    {
        _commands = meter.CreateCounter<long>(
            Commands, "{command}", "Commands decided by Charging's aggregates");
        _rejected = meter.CreateCounter<long>(
            CommandsRejected, "{command}", "Commands refused by a rule, by reason");
        _sessionsStarted = meter.CreateCounter<long>(
            SessionsStarted, "{session}", "Charging sessions started");
        _energyDelivered = meter.CreateHistogram(
            EnergyDelivered,
            "kWh",
            "Energy delivered per charging session",
            tags: null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = EnergyBoundaries });
        _timeToEnergyFlow = meter.CreateHistogram(
            TimeToEnergyFlow,
            "s",
            "From a journey's start to energy flowing, or to its abandonment on the way",
            tags: null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = WaitBoundaries });
    }

    /// <summary>
    /// For an actor that a test starts by hand, with no host: instruments on a meter of their
    /// own, which no host's factory owns.
    /// </summary>
    public static ChargingMetrics Unhosted { get; } =
        new(new Meter(ChargingDiagnostics.MeterName));

    /// <summary>
    /// A command an aggregate decided: counted, and when the decider refused it, counted as
    /// refused for the case of its error.
    /// </summary>
    public void Decided<TEvent, TError>(
        string aggregateType, object command, Result<ImmutableArray<TEvent>, TError> decision)
    {
        var tags = new TagList
        {
            { AggregateTag, aggregateType },
            { CommandTag, MessageNames.Of(command) },
        };
        _commands.Add(1, tags);
        if (decision is Err<TError> { Error: { } error })
        {
            tags.Add(ReasonTag, MessageNames.Of(error));
            _rejected.Add(1, tags);
        }
    }

    public void SessionStarted() => _sessionsStarted.Add(1);

    /// <summary>A session ended: the energy it delivered, in kWh.</summary>
    public void SessionEnded(Energy delivered) =>
        _energyDelivered.Record((double)delivered.KilowattHours);

    /// <summary>
    /// A journey's wait for energy is over, after this long: the driver's experience of
    /// "start charging", measured between two of the journey's own events.
    /// </summary>
    public void EnergyWaitEnded(TimeSpan waited, string outcome) =>
        _timeToEnergyFlow.Record(
            waited.TotalSeconds, new KeyValuePair<string, object?>(OutcomeTag, outcome));
}
