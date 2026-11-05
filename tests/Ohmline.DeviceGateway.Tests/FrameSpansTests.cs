using System.Diagnostics;
using Ohmline.DeviceGateway.Connections;
using Ohmline.DeviceGateway.Diagnostics;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Tests;

public sealed class FrameSpansTests
{
    [Fact]
    public void Given_a_connection_span_then_a_frame_starts_a_new_trace_linked_to_it()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DeviceGatewayDiagnostics.Name,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var connection = new Activity("GET /ocpp/{chargePointId}").Start();

        using var frame = FrameSpans.Start(new ChargePointId("OHM-SLC-0042"), connection.Context);

        frame.ShouldNotBeNull();
        frame.DisplayName.ShouldBe("receive ocpp");
        frame.Kind.ShouldBe(ActivityKind.Consumer);
        frame.ParentSpanId.ShouldBe(default);
        frame.TraceId.ShouldNotBe(connection.TraceId);
        frame.Links.ShouldHaveSingleItem().Context.ShouldBe(connection.Context);
        frame.GetTagItem("ohmline.charge_point.id").ShouldBe("OHM-SLC-0042");
        Activity.Current.ShouldBeSameAs(frame);
    }
}
