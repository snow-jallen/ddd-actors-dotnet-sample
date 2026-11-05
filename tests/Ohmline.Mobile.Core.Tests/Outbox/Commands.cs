using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Mobile.Core.Outbox;

namespace Ohmline.Mobile.Core.Tests.Outbox;

/// <summary>Start-charging commands for the outbox's tests, each with ids of its own.</summary>
internal static class Commands
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static OutboxCommand StartCharging(string connector = "OHM-SLC-0042/1") =>
        OutboxCommand.StartCharging(new StartChargingRequest(
            Guid.CreateVersion7(Morning),
            Guid.CreateVersion7(Morning),
            connector,
            "04A2B3C4D5E6F7",
            40m,
            "USD"));
}
