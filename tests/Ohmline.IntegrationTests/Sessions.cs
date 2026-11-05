namespace Ohmline.IntegrationTests;

/// <summary>Request bodies for a session at the bus depot, at a simple published tariff.</summary>
internal static class Sessions
{
    public static object Tariff { get; } = new
    {
        tariffId = "slc-standard",
        currency = "USD",
        timeZoneId = "America/Denver",
        components = new[] { new { kind = "energy-rate", price = 0.42m } },
    };

    public static object Start(
        Guid sessionId,
        string connectorId = "OHM-SLC-0042/1",
        string tokenUid = "04A2B3C4D5E6F7") => new
    {
        sessionId,
        siteId = "slc-depot",
        connectorId,
        tokenUid,
        tariffId = "slc-standard",
        spendCap = 40m,
        meterStartWh = 1_200_000L,
    };

    public static object Reading(long registerWh) =>
        new { registerWh, measuredAt = DateTimeOffset.UtcNow };
}
