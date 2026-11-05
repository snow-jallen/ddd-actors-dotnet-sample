using System.Collections.Immutable;
using Ohmline.Billing.Contracts.Receipts;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Invoicing;

/// <summary>
/// A row of <c>billing.invoices</c>: one per settled session, in Billing's words. Billing knows
/// a session only by reference, with its price breakdown, never its meter readings.
/// </summary>
public sealed record Invoice(
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
    /// <summary>The invoice for a session Charging published as completed.</summary>
    public static Invoice From(SessionCompleted completed, DateTimeOffset issuedAt) => new(
        completed.SessionId,
        completed.TokenUid,
        completed.ConnectorId,
        completed.EnergyKwh,
        completed.StartedAt,
        completed.EndedAt,
        completed.Price.Currency,
        completed.Price.Total,
        [.. completed.Price.Lines.Select(line => new ReceiptLine(
            line.Code, line.Quantity, line.Unit, line.UnitPrice, line.Amount))],
        issuedAt);

    public ReceiptResponse ToReceipt() => new(
        SessionId,
        TokenUid,
        ConnectorId,
        EnergyKwh,
        StartedAt,
        EndedAt,
        Currency,
        Total,
        Lines,
        IssuedAt);
}
