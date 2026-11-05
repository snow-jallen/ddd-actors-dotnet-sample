using System.Reflection;
using System.Runtime.CompilerServices;
using Ch03.ClosedLib;

namespace Ch03.LanguageLab.Tests;

public sealed class ClosedHierarchyTests
{
    private const string ChargePointId = "OHM-SLC-0042";

    [Fact]
    public void Given_a_closed_record_then_it_is_abstract_and_marked_closed()
    {
        var type = typeof(ChargerCommand);

        type.IsAbstract.ShouldBeTrue();
        type.GetCustomAttribute<IsClosedTypeAttribute>().ShouldNotBeNull();
    }

    [Fact]
    public void Given_each_command_from_another_assembly_when_described_then_no_discard_is_needed()
    {
        Describe(new RemoteStart(ChargePointId, 1, "04A1B2C3")).ShouldBe("start on 1 for 04A1B2C3");
        Describe(new RemoteStop(ChargePointId, "session-a")).ShouldBe("stop session-a");
        Describe(new SetChargingProfile(ChargePointId, 1, 32m)).ShouldBe("limit 1 to 32 kW");
        Describe(new ClearChargingProfile(ChargePointId, 1)).ShouldBe("clear the limit on 1");
    }

    [Fact]
    public void Given_an_intermediate_closed_level_when_matched_then_one_arm_covers_its_leaves()
    {
        IsSafeToRetry(new SetChargingProfile(ChargePointId, 1, 32m)).ShouldBeTrue();
        IsSafeToRetry(new ClearChargingProfile(ChargePointId, 1)).ShouldBeTrue();
        IsSafeToRetry(new RemoteStart(ChargePointId, 1, "04A1B2C3")).ShouldBeFalse();
    }

    [Fact]
    public void Given_an_abstract_record_when_another_assembly_adds_a_case_then_the_discard_runs()
    {
        Describe(new BootNotification(ChargePointId, "4.2.1")).ShouldBe("booted with 4.2.1");
        Describe(new Heartbeat(ChargePointId)).ShouldBe("alive");
        Describe(new DiagnosticsUploaded(ChargePointId)).ShouldBe("something new");
    }

    // Exhaustive across assemblies: no discard arm, and no null arm for a non-nullable reference.
    private static string Describe(ChargerCommand command) => command switch
    {
        RemoteStart start => $"start on {start.ConnectorNumber} for {start.TokenUid}",
        RemoteStop stop => $"stop {stop.SessionId}",
        SetChargingProfile profile => $"limit {profile.ConnectorNumber} to {profile.LimitKw} kW",
        ClearChargingProfile clear => $"clear the limit on {clear.ConnectorNumber}",
    };

    private static bool IsSafeToRetry(ChargerCommand command) => command switch
    {
        ChargingProfileCommand => true,
        RemoteStart or RemoteStop => false,
    };

    // A plain abstract record is open, so the compiler insists on a discard arm.
    private static string Describe(ChargerNotification notification) => notification switch
    {
        BootNotification boot => $"booted with {boot.FirmwareVersion}",
        Heartbeat => "alive",
        _ => "something new",
    };
}

/// <summary>Allowed: ChargerNotification is not closed, so this assembly can add a case.</summary>
file sealed record DiagnosticsUploaded(string ChargePointId) : ChargerNotification(ChargePointId);
