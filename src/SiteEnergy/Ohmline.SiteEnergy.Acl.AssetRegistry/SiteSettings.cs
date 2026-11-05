using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>
/// The site's fields as one row states them. AssetTrack repeats them on every row, which is how
/// rows of one site come to disagree (rule 10).
/// </summary>
public sealed record SiteSettings(
    SiteId SiteId, Power GridLimit, Power SafetyMargin, string TimeZoneId);
