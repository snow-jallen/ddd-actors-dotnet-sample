using System.Net.WebSockets;
using Ohmline.DeviceGateway.Ocpp.Frames;

namespace Ohmline.DeviceGateway.Ocpp.Tests;

/// <summary>Ohmline's reconnect hint on a WebSocket close frame (section 21.7).</summary>
public sealed class ReconnectHintTests
{
    [Fact]
    public void Given_a_wait_then_the_reason_names_it_in_whole_seconds_rounded_up()
    {
        ReconnectHint.Describe(TimeSpan.FromSeconds(16.2)).ShouldBe("retry-after=17");
    }

    [Fact]
    public void Given_a_restart_with_a_hint_then_the_charger_reads_the_wait()
    {
        ReconnectHint.Of(ReconnectHint.ServiceRestart, "retry-after=17")
            .ShouldBe(TimeSpan.FromSeconds(17));
    }

    [Theory]
    [InlineData(WebSocketCloseStatus.NormalClosure, "retry-after=17")]
    [InlineData(ReconnectHint.ServiceRestart, "Bye")]
    [InlineData(ReconnectHint.ServiceRestart, "retry-after=soon")]
    [InlineData(ReconnectHint.ServiceRestart, null)]
    [InlineData(null, null)]
    public void Given_any_other_close_then_there_is_no_hint(
        WebSocketCloseStatus? status, string? description)
    {
        ReconnectHint.Of(status, description).ShouldBeNull();
    }

    [Fact]
    public void A_restart_is_status_1012_service_restart()
    {
        ((int)ReconnectHint.ServiceRestart).ShouldBe(1012);
    }
}
