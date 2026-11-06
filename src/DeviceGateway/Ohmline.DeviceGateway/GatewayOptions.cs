using System.ComponentModel.DataAnnotations;

namespace Ohmline.DeviceGateway;

/// <summary>The gateway's settings, under <c>Ohmline:Gateway</c>, checked at start.</summary>
public sealed class GatewayOptions
{
    public const string Section = "Ohmline:Gateway";

    /// <summary>
    /// The tariff a session at these chargers starts with, and the spend cap it is given: the
    /// sample's one tariff, and the same amount the journey holds (section 15.3). A real
    /// operator would set both per site.
    /// </summary>
    [Required]
    public string TariffId { get; set; } = "slc-standard";

    [Range(typeof(decimal), "1", "1000")]
    public decimal SpendCap { get; set; } = 50m;

    /// <summary>How long Charging has to answer a command the gateway forwards.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan AskTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a charger has to answer a call Ohmline sends it.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:05:00")]
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A charger that sends nothing for this long, not even a heartbeat (every 60 s), is taken
    /// for gone: its connection is closed.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan SilenceTimeout { get; set; } = TimeSpan.FromSeconds(180);

    /// <summary>
    /// How long a stopping gateway takes to close its chargers' connections (section 21.7), in
    /// batches spread over this window, so that they do not all reconnect to the other
    /// gateways in the same second. It must end inside the coordinated-shutdown phase
    /// <c>service-unbind</c> (10 s, <c>ShutdownBudget</c>), with time left for the close
    /// handshakes.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:00:08")]
    public TimeSpan DrainWindow { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>How many connections are closed at the same moment.</summary>
    [Range(1, 10_000)]
    public int DrainBatchSize { get; set; } = 25;

    /// <summary>
    /// The shortest wait a charger is asked for before it reconnects. Each is told a wait of
    /// its own between this and <see cref="ReconnectAfterMax"/>, so the reconnections are
    /// spread out a second time, by the chargers themselves.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan ReconnectAfterMin { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The longest wait a charger is asked for before it reconnects.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan ReconnectAfterMax { get; set; } = TimeSpan.FromSeconds(30);
}
