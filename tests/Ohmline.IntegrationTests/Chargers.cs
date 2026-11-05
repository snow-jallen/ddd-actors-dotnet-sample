namespace Ohmline.IntegrationTests;

/// <summary>
/// Charge point ids of the test's own: the database is shared by every test in the assembly
/// and lives for the whole run.
/// </summary>
internal static class Chargers
{
    private static int _next = Random.Shared.Next(1000, 9000);

    public static string Next() => $"OHM-ITG-{Interlocked.Increment(ref _next):0000}";

    public static object Registration(string chargePointId) => new
    {
        chargePointId,
        siteId = "slc-depot",
        connectorMaxPowersKw = new[] { 22m, 22m },
    };

    public static object Reservation(string tokenUid = "04A2B3C4D5E6F7") => new { tokenUid };
}
