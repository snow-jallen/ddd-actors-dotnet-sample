using System.Collections;
using System.Globalization;

namespace Ohmline.Simulator;

/// <summary>
/// Where the simulator connects and what it plays. From the command line
/// (<c>--chargers 5 --scenario plug-in-and-charge --gateway http://... --node http://...</c>),
/// with the addresses Aspire passes in its environment (<c>services__gateway__http__0</c>) as
/// the next choice, and the local defaults last. <c>--duration</c>, in seconds, bounds a
/// scenario that runs until it is stopped, such as <c>reconnect-storm</c>.
/// </summary>
public sealed record SimulatorOptions(Uri Gateway, Uri Node, int Chargers, string Scenario)
{
    public const string PlugInAndCharge = "plug-in-and-charge";
    public const string Fault = "fault";
    public const string Disconnect = "disconnect";
    public const string ReconnectStorm = "reconnect-storm";

    /// <summary>How long a scenario that runs until it is stopped runs.</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(5);

    public static SimulatorOptions Parse(string[] args, IDictionary environment)
    {
        var named = args.Chunk(2)
            .Where(pair => pair is [var name, _] && name.StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(pair => pair[0][2..], pair => pair[1]);
        return new SimulatorOptions(
            new Uri(named.GetValueOrDefault("gateway")
                ?? environment["services__gateway__http__0"] as string
                ?? "http://localhost:5190"),
            new Uri(named.GetValueOrDefault("node")
                ?? environment["services__node__http__0"] as string
                ?? "http://localhost:5180"),
            int.Parse(named.GetValueOrDefault("chargers") ?? "1", CultureInfo.InvariantCulture),
            named.GetValueOrDefault("scenario") ?? PlugInAndCharge)
        {
            Duration = named.GetValueOrDefault("duration") is { } seconds
                ? TimeSpan.FromSeconds(int.Parse(seconds, CultureInfo.InvariantCulture))
                : TimeSpan.FromMinutes(5),
        };
    }
}
