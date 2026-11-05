using System.Collections.Immutable;
using CsCheck;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;
using Gen = CsCheck.Gen;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// What holds for a session whatever its charge point reports, in any order: readings that go
/// up, go down or arrive late, suspensions by either side, resumptions and stops.
/// </summary>
public sealed class ChargingSessionProperties
{
    private const long MeterStartWh = 1_200_000;

    /// <summary>A spend cap of USD 10.00 at USD 0.50 a kWh: reached at 20 kWh.</summary>
    private static readonly StartSession Start = new(
        Command,
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        StandardTariff(0.50m),
        new Money(10.00m, Currency.Usd),
        MeterStartWh);

    /// <summary>
    /// Registers from 1 kWh below the start to 60 kWh above it, measured at any minute of the
    /// next two hours, so readings also go backwards and arrive out of order.
    /// </summary>
    private static readonly Gen<SessionCommand> Commands = Gen.OneOf<SessionCommand>(
        Gen.Select(Gen.Int[-1_000, 60_000], Gen.Int[0, 120], (wattHours, minute) =>
            new RecordMeterValue(
                Command, Session, MeterStartWh + wattHours, Morning.AddMinutes(minute))),
        Gen.OneOf<SuspendedBy>(Gen.Const(new ByEv()), Gen.Const(new ByCharger()))
            .Select(by => new SuspendCharging(Command, Session, by)),
        Gen.Const(new ResumeCharging(Command, Session)),
        Gen.Const(new StopSession(Command, Session)),
        Gen.Const(Start));

    private static readonly Gen<ImmutableArray<SessionChecks.Timed>> Scripts =
        SessionChecks.Scripts(Commands, Gen.Int[0, 5].Select(m => TimeSpan.FromMinutes(m)), 40,
            Start);

    [Fact]
    public void The_energy_delivered_never_goes_down()
    {
        SessionChecks.Monotonic(Scripts, Morning, "The energy delivered", Delivered);
    }

    [Fact]
    public void Evolve_is_total_for_every_event_decide_produces()
    {
        SessionChecks.EvolveIsTotal(Scripts, Morning);
    }

    [Fact]
    public void Decide_never_records_an_event_that_evolve_ignores()
    {
        SessionChecks.EveryEventChangesTheState(
            Scripts,
            Morning,
            journalOnly: @event => @event is MeterAnomalyDetected or SpendCapReached);
    }

    [Fact]
    public void A_session_ends_with_the_energy_its_meter_delivered()
    {
        SessionChecks.AfterEveryDecision(Scripts, Morning, step =>
        {
            foreach (var ended in step.Events.OfType<SessionEnded>())
            {
                ended.EnergyDelivered.ShouldBe(Delivered(step.Before));
            }
        });
    }

    /// <summary>The energy the session has counted so far, in every state it can be in.</summary>
    private static Energy Delivered(SessionState session) => session switch
    {
        NoSession => Energy.Zero,
        Active active => active.Meter.Delivered,
        SuspendedByEv idle => idle.Meter.Delivered,
        SuspendedByCharger paused => paused.Meter.Delivered,
        Ended ended => ended.EnergyDelivered,
        null => throw new InvalidOperationException("The session state is default."),
    };
}
