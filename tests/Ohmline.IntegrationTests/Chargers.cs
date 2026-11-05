using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;

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

    /// <summary>
    /// A car plugged into connector 1 and its session begun, as the Device Gateway reports it
    /// (section 16.7): the charge point is told through its region in the node.
    /// </summary>
    public static async Task PlugInAsync(
        OhmlineNode node, string chargePoint, Guid session, string tokenUid)
    {
        var chargePoints = await node.Services.GetRequiredService<ActorRegistry>()
            .GetAsync<ChargePointRegion>(TestContext.Current.CancellationToken);
        var occupy = new OccupyConnector(
            new CommandId(Guid.CreateVersion7()),
            new ChargePointId(chargePoint),
            1,
            new SessionId(session),
            new TokenUid(tokenUid));
        (await chargePoints.Ask<CommandReply>(
                occupy, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken))
            .ShouldBeOfType<CommandAccepted>();
    }
}
