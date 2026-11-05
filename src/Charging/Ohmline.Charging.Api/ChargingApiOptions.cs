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

    /// <summary>
    /// How much a charging journey holds on the driver's card before charging starts
    /// (section 15.3): more than most sessions cost. Chapter 19 lets the driver choose.
    /// </summary>
    [Range(typeof(decimal), "1", "1000")]
    public decimal HoldAmount { get; set; } = 50m;

    /// <summary>The hold's currency, an ISO 4217 code.</summary>
    [RegularExpression("^[A-Z]{3}$")]
    public string HoldCurrency { get; set; } = "USD";
}
