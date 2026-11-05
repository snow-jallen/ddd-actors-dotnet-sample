using System.Text.Json.Serialization;

namespace Ch03.LanguageLab.Tests;

/// <summary>
/// System.Text.Json writes a "$type" discriminator for every case. Without the attribute (or
/// the same option on JsonSerializerOptions), it writes only the base type's members.
/// </summary>
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record ConnectorEvent(string ChargePointId, int ConnectorNumber);

public sealed record ConnectorReserved(string ChargePointId, int ConnectorNumber, string TokenUid)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ConnectorReleased(string ChargePointId, int ConnectorNumber)
    : ConnectorEvent(ChargePointId, ConnectorNumber);
