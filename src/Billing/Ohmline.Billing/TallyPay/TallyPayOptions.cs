using System.ComponentModel.DataAnnotations;

namespace Ohmline.Billing.TallyPay;

/// <summary>Where TallyPay answers, under <c>Ohmline:Billing:TallyPay</c>.</summary>
public sealed class TallyPayOptions
{
    public const string Section = "Ohmline:Billing:TallyPay";

    /// <summary><c>http://fakepsp</c>: the stand-in's Aspire resource, by discovery.</summary>
    [Required]
    public Uri BaseAddress { get; set; } = new("http://fakepsp");
}
