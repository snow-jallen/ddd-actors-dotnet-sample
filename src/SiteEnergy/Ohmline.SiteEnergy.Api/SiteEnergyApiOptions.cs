using System.ComponentModel.DataAnnotations;

namespace Ohmline.SiteEnergy.Api;

/// <summary>The settings of Site Energy's HTTP edge, under <c>Ohmline:SiteEnergy</c>.</summary>
public sealed class SiteEnergyApiOptions
{
    public const string Section = "Ohmline:SiteEnergy";

    /// <summary>How long an endpoint waits for a budget's answer before it answers 504.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan AskTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
