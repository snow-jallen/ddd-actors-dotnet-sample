using System.Text.Json;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Translation;
using Ohmline.SharedKernel.Units;
using static Ohmline.DeviceGateway.Ocpp.Tests.Recorded;

namespace Ohmline.DeviceGateway.Ocpp.Tests;

public sealed class OutboundCallsTests
{
    [Fact]
    public void A_remote_start_asks_for_the_token_on_the_connector_at_most_at_the_limit()
    {
        var call = OutboundCalls.RemoteStart("m-1", 1, DriversCard, new Power(11m), 17);

        OcppFrameParser.Write(call).ShouldBe(Minified("""
            [2, "m-1", "RemoteStartTransaction", {
              "connectorId": 1,
              "idTag": "04A2B3C4D5E6F7",
              "chargingProfile": {
                "chargingProfileId": 17,
                "stackLevel": 0,
                "chargingProfilePurpose": "TxProfile",
                "chargingProfileKind": "Absolute",
                "chargingSchedule": {
                  "chargingRateUnit": "W",
                  "chargingSchedulePeriod": [{ "startPeriod": 0, "limit": 11000 }]
                }
              }
            }]
            """));
    }

    [Fact]
    public void A_charging_profile_carries_the_allocation_version_as_its_id()
    {
        var call = OutboundCalls.SetChargingProfile("m-2", 2, new Power(7.4m), 42);

        var profile = call.Payload.GetProperty("csChargingProfiles");
        profile.GetProperty("chargingProfileId").GetInt32().ShouldBe(42);
        profile.GetProperty("chargingSchedule").GetProperty("chargingSchedulePeriod")[0]
            .GetProperty("limit").GetDecimal().ShouldBe(7400m);
    }

    [Theory]
    [InlineData("""{"status":"Accepted"}""", true)]
    [InlineData("""{"status":"Rejected"}""", false)]
    [InlineData("""{}""", false)]
    public void A_charger_accepts_only_by_saying_so(string payload, bool accepted)
    {
        var result = new CallResult("m-3", JsonDocument.Parse(payload).RootElement);

        OutboundCalls.Accepted(result).ShouldBe(accepted);
    }

    private static string Minified(string json) =>
        JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);
}
