using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>The one outcome of every row of the export.</summary>
public union AssetRowTranslation(Translated, Skipped, Rejected)
{
    public AssetTrackRow Row => this switch
    {
        Translated translated => translated.Row,
        Skipped skipped => skipped.Row,
        Rejected rejected => rejected.Row,
        null => throw new InvalidOperationException("This AssetRowTranslation is default."),
    };
}

/// <summary>An active connector, every field read with certainty.</summary>
public sealed record Translated(
    AssetTrackRow Row, SiteSettings Site, ConnectorConfiguration Connector);

/// <summary>A row not meant for Site Energy: retired, a test record, an exact duplicate.</summary>
public sealed record Skipped(AssetTrackRow Row, AssetTrackRule Rule, string Reason);

/// <summary>A row meant for Site Energy that cannot be trusted; a person has to fix it.</summary>
public sealed record Rejected(AssetTrackRow Row, AssetTrackRule Rule, string Reason);
