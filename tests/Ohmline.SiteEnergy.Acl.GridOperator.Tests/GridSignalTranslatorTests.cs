using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;
using Ohmline.SiteEnergy.Contracts.Budgets;
using static Ohmline.SiteEnergy.Acl.GridOperator.Tests.Northgate;

namespace Ohmline.SiteEnergy.Acl.GridOperator.Tests;

/// <summary>Northgate's events and a moment in, what the depot is told out: a row a case.</summary>
public sealed class GridSignalTranslatorTests
{
    public static TheoryData<string, decimal, string, int, string> Rows => new()
    {
        // status, payload, units, at (minutes after 17:00), then
        { "active", -60m, "kW", -1, "restore" },
        { "far", -60m, "kW", 0, "curtail 60 kW" },
        { "active", -60m, "kW", 30, "curtail 60 kW" },
        { "active", -60m, "kW", 120, "restore" },
        { "cancelled", -60m, "kW", 30, "restore" },
        { "completed", -60m, "kW", 30, "restore" },
        { "active", -60000m, "W", 30, "curtail 60 kW" },
        { "active", -0.25m, "MW", 30, "curtail 250 kW" },
        { "active", 40m, "kW", 30, "curtail 40 kW" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void An_event_in_force_curtails_the_site_and_one_not_in_force_restores_it(
        string status, decimal payload, string units, int minutes, string then)
    {
        var desired = GridSignalTranslator.Desired(
            [Event(payload, status, units)], Sites, FivePm.AddMinutes(minutes));

        Show(desired[Depot]).ShouldBe(then);
    }

    [Fact]
    public void Given_a_VEN_that_is_none_of_Ohmlines_then_nothing_is_said_about_it()
    {
        GridSignalTranslator.Desired([Event(ven: "NG-VEN-9999")], Sites, FivePm)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Given_two_events_in_force_then_the_deepest_cut_wins()
    {
        var events = new[] { Event(-60m), Event(-90m, id: "ng-dr-0008", sequence: 2) };

        var desired = GridSignalTranslator.Desired(events, Sites, FivePm.AddMinutes(10));

        desired[Depot].Value.ShouldBe(new Curtail(Depot, new Power(90m), "ng-dr-0008", 0));
    }

    [Fact]
    public void The_same_revision_is_the_same_command_and_a_new_revision_is_another()
    {
        var curtail = new Curtail(Depot, new Power(60m), "ng-dr-0007", 0);

        var once = GridSignalTranslator.Command(curtail);
        var again = GridSignalTranslator.Command(curtail);
        var revised = GridSignalTranslator.Command(curtail with { Modification = 1 });

        once.ShouldBe(again);
        once.ShouldBe(new ApplyCurtailment(once.CommandId, Depot, new Power(60m)));
        revised.CommandId.ShouldNotBe(once.CommandId);
        GridSignalTranslator.Command(new Restore(Depot, "ng-dr-0007", 0))
            .ShouldBeOfType<EndCurtailment>().SiteId.ShouldBe(Depot);
    }

    private static string Show(GridInstruction instruction) => instruction.Value switch
    {
        Curtail curtail => $"curtail {curtail.Reduction.Kilowatts:0.##} kW",
        Restore => "restore",
        _ => "nothing",
    };
}
