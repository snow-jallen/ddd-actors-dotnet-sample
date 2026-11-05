namespace Ohmline.Charging.ReadModels.Tests;

public sealed class ChargingReadModelsExtensionsTests
{
    /// <summary>
    /// Instance <c>i</c> of the daemon process runs stream <c>i</c>, so the order is durable:
    /// new streams go at the end.
    /// </summary>
    [Fact]
    public void The_daemon_runs_one_instance_per_projection_stream_in_a_fixed_order() =>
        ChargingReadModelsExtensions.Streams
            .Select(stream => $"{stream.Projection.ProjectionId}/{stream.Tag}")
            .ShouldBe(["session-history-v1/charging-session"]);
}
