using System.Collections.Immutable;
using System.Diagnostics;

namespace Ch03.LanguageLab.Tests;

public sealed class PatternTests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_meter_histories_when_classified_by_list_patterns_then_each_shape_is_named()
    {
        Classify([]).ShouldBe("no readings");
        Classify([Sample(0, 1_200_000)]).ShouldBe("one reading");
        Classify([Sample(0, 1_200_000), Sample(1, 1_200_600), Sample(2, 1_201_230)])
            .ShouldBe("1230 Wh delivered");
        Classify([Sample(0, 1_200_000), Sample(1, 1_200_600), Sample(2, 1_200_100)])
            .ShouldBe("the register went backwards");
    }

    [Theory]
    [InlineData(true, 0, 16, true)]
    [InlineData(true, 0, 15, false)]
    [InlineData(true, 7, 30, false)]
    [InlineData(false, 0, 30, false)]
    public void Given_a_snapshot_when_the_idle_fee_rule_is_a_property_pattern_then_it_reads_as_rule(
        bool pluggedIn, int powerKw, int idleMinutes, bool expected)
    {
        var snapshot = new ChargingSnapshot(
            pluggedIn, new Kilowatts(powerKw), TimeSpan.FromMinutes(idleMinutes));

        IdleFeeApplies(snapshot).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "idle")]
    [InlineData(7.4, "AC")]
    [InlineData(50, "DC fast")]
    [InlineData(350, "DC ultra-fast")]
    public void Given_a_power_when_matched_by_relational_patterns_then_no_discard_is_needed(
        decimal powerKw, string expected)
    {
        Describe(new Kilowatts(powerKw)).ShouldBe(expected);
    }

    [Fact]
    public void Given_nested_loops_when_a_fault_is_found_then_a_labeled_break_leaves_both()
    {
        (string ChargePointId, ConnectorStatus[] Connectors)[] site =
        [
            ("OHM-SLC-0041", [new Available(), new Occupied("session-a")]),
            ("OHM-SLC-0042", [new Faulted("E-42"), new Available()]),
            ("OHM-SLC-0043", [new Faulted("E-17"), new Available()]),
        ];

        var (fault, inspected) = FindFirstFault(site);

        fault.ShouldBe(new ConnectorKey("OHM-SLC-0042", 1));
        inspected.ShouldBe(3);
    }

    [Fact]
    public void Given_a_collection_expression_with_a_comparer_then_token_uids_ignore_case()
    {
        HashSet<string> tokens = [with(StringComparer.OrdinalIgnoreCase), "04A1B2C3", "04a1b2c3"];

        tokens.Count.ShouldBe(1);
        tokens.ShouldContain("04A1B2C3");
    }

    [Fact]
    public void Given_a_collection_expression_with_a_capacity_then_the_list_is_presized()
    {
        // An hour of meter samples, one a minute.
        List<MeterSample> buffer = [with(capacity: 60), Sample(0, 1_200_000)];

        buffer.Capacity.ShouldBe(60);
        buffer.Count.ShouldBe(1);
    }

    private static string Classify(ImmutableArray<MeterSample> history) => history switch
    {
        [] => "no readings",
        [_] => "one reading",
        [.., var previous, var last] when last.RegisterWh < previous.RegisterWh =>
            "the register went backwards",
        [var first, .., var last] => $"{last.RegisterWh - first.RegisterWh} Wh delivered",
    };

    private static bool IdleFeeApplies(ChargingSnapshot snapshot) =>
        snapshot is { PluggedIn: true, Power.Value: 0m, IdleFor.TotalMinutes: > 15 };

    private static string Describe(Kilowatts power) => power.Value switch
    {
        0m => "idle",
        > 0m and < 22m => "AC",
        >= 22m and < 150m => "DC fast",
        >= 150m => "DC ultra-fast",
        < 0m => throw new UnreachableException("Kilowatts are never negative."),
    };

    private static (ConnectorKey? Fault, int Inspected) FindFirstFault(
        (string ChargePointId, ConnectorStatus[] Connectors)[] site)
    {
        ConnectorKey? fault = null;
        var inspected = 0;

        chargePoints:
        foreach (var (chargePointId, connectors) in site)
        {
            for (var index = 0; index < connectors.Length; index++)
            {
                inspected++;
                if (connectors[index] is Faulted)
                {
                    fault = new ConnectorKey(chargePointId, index + 1);
                    break chargePoints;
                }
            }
        }

        return (fault, inspected);
    }

    private static MeterSample Sample(int minute, long registerWh) =>
        new(PluggedInAt.AddMinutes(minute), registerWh);
}
