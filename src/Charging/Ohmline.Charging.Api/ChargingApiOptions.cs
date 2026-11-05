using System.ComponentModel.DataAnnotations;

namespace Ohmline.Charging.Api;

/// <summary>
/// The settings of Charging's HTTP edge, under <c>Ohmline:Charging</c>. They are checked when
/// the host starts, so a wrong value stops the node instead of failing its first request.
/// </summary>
public sealed class ChargingApiOptions
{
    public const string Section = "Ohmline:Charging";

    /// <summary>
    /// How long an endpoint waits for an aggregate's answer before it gives up with 504. Long
    /// enough for a recovery and a write; short enough that a client is not left hanging.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan AskTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
