using System.Text.Json;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Messages;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.DeviceGateway.Ocpp.Translation;

/// <summary>
/// What Ohmline asks a charger to do, written as OCPP calls, and the charger's answers read
/// back. The message id is the caller's, so it can match the answer to its question.
/// </summary>
public static class OutboundCalls
{
    public static Call RemoteStart(
        string messageId, int connector, TokenUid token, Power limit, long profileId) =>
        new(
            messageId,
            OcppActions.RemoteStartTransaction,
            OcppJson.Element(new RemoteStartTransactionRequest(
                connector, token.Value, Profile(limit, profileId, "TxProfile"))));

    public static Call RemoteStop(string messageId, int transactionId) =>
        new(
            messageId,
            OcppActions.RemoteStopTransaction,
            OcppJson.Element(new RemoteStopTransactionRequest(transactionId)));

    /// <summary>
    /// The profile's id is the allocation's version (section 16.8), so a charger told twice is
    /// told the same profile.
    /// </summary>
    public static Call SetChargingProfile(
        string messageId, int connector, Power limit, long allocationVersion) =>
        new(
            messageId,
            OcppActions.SetChargingProfile,
            OcppJson.Element(new SetChargingProfileRequest(
                connector, Profile(limit, allocationVersion, "TxDefaultProfile"))));

    /// <summary>The charger's answer to any of the three: did it accept?</summary>
    public static bool Accepted(CallResult result) =>
        result.Payload.Deserialize<CentralRequestResponse>(OcppJson.Options)?.IsAccepted == true;

    private static ChargingProfile Profile(Power limit, long id, string purpose) =>
        new(
            (int)(id % int.MaxValue),
            StackLevel: 0,
            purpose,
            ChargingProfileKind: "Absolute",
            new ChargingSchedule("W", [new ChargingSchedulePeriod(0, limit.Watts)]));
}
