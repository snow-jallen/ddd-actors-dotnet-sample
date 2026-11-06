using Ohmline.DeviceGateway.Draining;

namespace Ohmline.DeviceGateway.Tests;

/// <summary>How a stopping gateway spreads out its chargers' reconnections (21.7).</summary>
public sealed class DrainPlanTests
{
    private static readonly GatewayOptions Options = new()
    {
        DrainWindow = TimeSpan.FromSeconds(8),
        DrainBatchSize = 25,
        ReconnectAfterMin = TimeSpan.FromSeconds(5),
        ReconnectAfterMax = TimeSpan.FromSeconds(30),
    };

    private static readonly int[] Hundred = [.. Enumerable.Range(1, 100)];

    [Fact]
    public void Given_100_connections_then_each_is_closed_once_in_4_batches_of_25()
    {
        var plan = DrainPlan.For(Hundred, Options, new Random(42));

        plan.Select(step => step.Connection).Order().ShouldBe(Hundred);
        plan.GroupBy(step => step.CloseAt).Select(batch => batch.Count())
            .ShouldBe([25, 25, 25, 25]);
    }

    [Fact]
    public void Given_100_connections_then_each_batch_closes_in_its_own_2_s_slot()
    {
        var plan = DrainPlan.For(Hundred, Options, new Random(42));

        var moments = plan.Select(step => step.CloseAt).Distinct().ToList();
        moments.Select((closeAt, slot) => closeAt >= TimeSpan.FromSeconds(2 * slot)
                                          && closeAt < TimeSpan.FromSeconds(2 * slot + 1))
            .ShouldAllBe(inItsSlot => inItsSlot);
    }

    [Fact]
    public void Given_a_drain_then_every_charger_is_told_a_wait_of_its_own_within_the_bounds()
    {
        var plan = DrainPlan.For(Hundred, Options, new Random(42));

        plan.ShouldAllBe(step => step.ReconnectAfter >= TimeSpan.FromSeconds(5)
                                 && step.ReconnectAfter <= TimeSpan.FromSeconds(30)
                                 && step.ReconnectAfter.Milliseconds == 0);
        plan.Select(step => step.ReconnectAfter).Distinct().Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Given_the_same_randomness_then_the_same_plan()
    {
        DrainPlan.For(Hundred, Options, new Random(7))
            .ShouldBe(DrainPlan.For(Hundred, Options, new Random(7)));
    }

    [Fact]
    public void Given_no_connections_then_nothing_to_drain()
    {
        DrainPlan.For<int>([], Options, new Random(42)).ShouldBeEmpty();
    }
}
