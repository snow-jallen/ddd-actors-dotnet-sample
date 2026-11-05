namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>
/// The translation rules of <c>docs/acl/assettrack.md</c>, numbered as there and applied in this
/// order. A skipped or rejected row names the rule that decided it.
/// </summary>
public enum AssetTrackRule
{
    Status = 1,
    SiteCode = 2,
    PowerValue = 3,
    Plausibility = 4,
    TimeZone = 5,
    ChargePoint = 6,
    ConnectorNumber = 7,
    PriorityClass = 8,
    Duplicate = 9,
    SiteAgreement = 10,
    EffectiveCapacity = 11,
}
