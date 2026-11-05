using System.ComponentModel.DataAnnotations;

namespace Ohmline.SiteEnergy.Acl.GridOperator;

/// <summary>
/// The grid operator's ACL, under <c>Ohmline:SiteEnergy:GridOperator</c>: whether to ask
/// Northgate at all, how often, and which of Northgate's VEN ids is which Ohmline site. A VEN
/// that is not in <see cref="Sites"/> is none of Ohmline's.
/// </summary>
public sealed class GridOperatorOptions
{
    public const string Section = "Ohmline:SiteEnergy:GridOperator";

    public bool Enabled { get; set; }

    [Range(typeof(TimeSpan), "00:00:00.010", "00:10:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan AskTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Northgate's VEN id to Ohmline's site: <c>NG-VEN-0042: slc-depot</c>.</summary>
    public Dictionary<string, string> Sites { get; set; } = [];
}
