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
            .ShouldBe(
            [
                "session-history-v1/charging-session",
                "site-live-board-v1/charging",
                "site-live-board-v1/site-energy",
                "driver-app-v1/charging-session",
                "session-ended-subscriber-v1/charging-session",
                "journey-session-relay-v1/charging-session",
                "integration-relay-v1/charging-session",
                "charging-profile-dispatcher-v1/site-energy",
                "session-history-v2/charging-session",
            ]);
}
