namespace Ohmline.Billing.Contracts.Holds;

/// <summary>The statuses a hold can be in, as the hold API writes them.</summary>
public static class HoldStatus
{
    public const string Placed = "placed";
    public const string Captured = "captured";
    public const string Voided = "voided";
}
