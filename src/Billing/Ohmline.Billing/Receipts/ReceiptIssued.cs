namespace Ohmline.Billing.Receipts;

/// <summary>
/// A receipt is ready for the driver: a message in Billing's outbox, written in the same
/// database transaction as the invoice it is for.
/// </summary>
public sealed record ReceiptIssued(Guid SessionId, string TokenUid, decimal Total, string Currency)
{
    public const string Type = "billing.receipt-issued.v1";
}
