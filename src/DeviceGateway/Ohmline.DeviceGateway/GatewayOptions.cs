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
}
