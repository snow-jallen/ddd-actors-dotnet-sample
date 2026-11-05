using System.Text.Json;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.SharedKernel;

namespace Ohmline.DeviceGateway.Ocpp.Tests;

public sealed class OcppFrameParserTests
{
    [Fact]
    public void Given_a_recorded_call_then_its_id_action_and_payload_are_read()
    {
        var call = Recorded.Call("voltaris", "boot-notification");

        call.MessageId.ShouldBe("19223201");
        call.Action.ShouldBe("BootNotification");
        call.Payload.GetProperty("chargePointModel").GetString().ShouldBe("VX-22");
    }

    [Fact]
    public void Given_a_call_result_then_it_is_read_with_its_payload()
    {
        var frame = Parsed("""[3,"m-7",{"status":"Accepted"}]""");

        var result = frame.Value.ShouldBeOfType<CallResult>();
        result.MessageId.ShouldBe("m-7");
        result.Payload.GetProperty("status").GetString().ShouldBe("Accepted");
    }

    [Fact]
    public void Given_a_call_error_then_its_code_and_description_are_read()
    {
        var frame = Parsed("""[4,"m-8","NotSupported","No profiles here",{}]""");

        frame.Value.ShouldBe(new CallError("m-8", "NotSupported", "No profiles here"));
    }

    [Theory]
    [InlineData("""not json""", null)]
    [InlineData("""{"messageTypeId":2}""", null)]
    [InlineData("""[2,17,"Heartbeat",{}]""", null)]
    [InlineData("""[5,"m-9","Heartbeat",{}]""", "m-9")]
    [InlineData("""[2,"m-9","Heartbeat"]""", "m-9")]
    [InlineData("""[2,"m-9","Heartbeat",[]]""", "m-9")]
    public void Given_text_that_is_not_a_frame_then_it_is_a_formation_violation(
        string text, string? answerable)
    {
        var error = OcppFrameParser.Parse(text).Value.ShouldBeOfType<Err<FrameError>>().Error;

        error.ErrorCode.ShouldBe("FormationViolation");
        error.MessageId.ShouldBe(answerable);
        (error.Reply is null).ShouldBe(answerable is null);
    }

    [Theory]
    [InlineData("""[2,"m-1","Heartbeat",{}]""")]
    [InlineData("""[3,"m-2",{"currentTime":"2026-11-04T14:40:00.000Z"}]""")]
    [InlineData("""[4,"m-3","NotImplemented","DataTransfer is not supported.",{}]""")]
    public void A_frame_is_written_as_it_is_read(string text)
    {
        OcppFrameParser.Write(Parsed(text)).ShouldBe(text);
    }

    private static OcppFrame Parsed(string text) =>
        OcppFrameParser.Parse(text).Value.ShouldBeOfType<Ok<OcppFrame>>().Value;
}
