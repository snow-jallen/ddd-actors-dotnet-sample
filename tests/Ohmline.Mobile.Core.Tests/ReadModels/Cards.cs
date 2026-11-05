using Ohmline.Charging.Client.Contracts.ReadModels;

namespace Ohmline.Mobile.Core.Tests.ReadModels;

/// <summary>The driver's session at the depot, as its card is at each version.</summary>
internal static class Cards
{
    public const string Token = "04A2B3C4D5E6F7";

    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly Guid Session = Guid.CreateVersion7(Morning);

    public static DriverSessionCard At(long version, decimal energyKwh = 0m, bool ended = false) =>
        new(
            Session,
            Token,
            ended ? "ended" : "charging",
            "OHM-SLC-0042/1",
            "Salt Lake bus depot, public bays",
            energyKwh,
            Morning,
            ended ? Morning.AddMinutes(45) : null,
            version);
}
