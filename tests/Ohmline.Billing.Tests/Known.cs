using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Tests;

/// <summary>The sessions and holds Billing's tests talk about.</summary>
internal static class Known
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static SessionCompleted Completed(Guid? session = null, decimal total = 12.60m)
    {
        var sessionId = session ?? Guid.CreateVersion7();
        return new SessionCompleted(
            Guid.CreateVersion7(),
            Morning.AddMinutes(45),
            sessionId,
            "04A2B3C4D5E6F7",
            "OHM-SLC-0042/1",
            30m,
            new PriceBreakdown("USD", total, [new("energy", 30m, "kWh", total / 30m, total)]),
            Morning,
            Morning.AddMinutes(45));
    }

    /// <summary>A hold id of the test's own, shaped like the journey's idempotency keys.</summary>
    public static string NewHoldId() => $"{Guid.CreateVersion7()}-hold";
}
