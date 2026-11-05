using System.Collections.Immutable;

namespace Ohmline.Billing.Contracts.Receipts;

/// <summary>
/// <c>GET /receipts/{sessionId}</c>: the driver-facing record of a settled session, line by
/// line, as Charging priced it.
/// </summary>
public sealed record ReceiptResponse(
    Guid SessionId,
    string TokenUid,
    string ConnectorId,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Currency,
    decimal Total,
    ImmutableArray<ReceiptLine> Lines,
    DateTimeOffset IssuedAt)
{
    public bool Equals(ReceiptResponse? other) =>
        other is not null
        && SessionId == other.SessionId
        && TokenUid == other.TokenUid
        && ConnectorId == other.ConnectorId
        && EnergyKwh == other.EnergyKwh
        && StartedAt == other.StartedAt
        && EndedAt == other.EndedAt
        && Currency == other.Currency
        && Total == other.Total
        && Lines.SequenceEqual(other.Lines)
        && IssuedAt == other.IssuedAt;

    public override int GetHashCode() => HashCode.Combine(SessionId, Total, Lines.Length);
}

/// <summary>One line of a receipt: what was charged for, how much, at what price.</summary>
public sealed record ReceiptLine(
    string Code, decimal Quantity, string Unit, decimal UnitPrice, decimal Amount);
