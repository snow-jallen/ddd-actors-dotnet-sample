using System.ComponentModel.DataAnnotations;

namespace Ohmline.DriverAccounts.Api;

/// <summary>The settings of Driver Accounts' edge, under <c>Ohmline:DriverAccounts</c>.</summary>
public sealed class DriverAccountsApiOptions
{
    public const string Section = "Ohmline:DriverAccounts";

    /// <summary>How long an endpoint waits for a token's answer before it answers 504.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan AskTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
