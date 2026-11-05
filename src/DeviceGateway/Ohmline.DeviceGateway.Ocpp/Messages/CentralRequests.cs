using System.Collections.Immutable;

namespace Ohmline.DeviceGateway.Ocpp.Messages;

// What Ohmline asks a charger to do, as OCPP 1.6J names it. The charger answers each with a
// status: Accepted or Rejected.

/// <summary>Start charging on the connector for the token, at most the profile's limit.</summary>
public sealed record RemoteStartTransactionRequest(
    int? ConnectorId, string IdTag, ChargingProfile? ChargingProfile = null);

/// <summary>Stop the OCPP transaction with this number.</summary>
public sealed record RemoteStopTransactionRequest(int TransactionId);

/// <summary>Draw at most the profile's limit on the connector from now on.</summary>
public sealed record SetChargingProfileRequest(int ConnectorId, ChargingProfile CsChargingProfiles);

/// <summary>The answer to each of the three: <c>Accepted</c> or <c>Rejected</c>.</summary>
public sealed record CentralRequestResponse(string Status)
{
    public bool IsAccepted => Status == "Accepted";
}

/// <summary>
/// A charging profile, reduced to what Ohmline uses: one period, from now, in watts. The
/// profile id says which allocation it carries out, so a charger can tell an old one.
/// </summary>
public sealed record ChargingProfile(
    int ChargingProfileId,
    int StackLevel,
    string ChargingProfilePurpose,
    string ChargingProfileKind,
    ChargingSchedule ChargingSchedule);

public sealed record ChargingSchedule(
    string ChargingRateUnit, ImmutableArray<ChargingSchedulePeriod> ChargingSchedulePeriod);

public sealed record ChargingSchedulePeriod(int StartPeriod, decimal Limit);
