using System.Collections.Immutable;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>
/// The result of translating one export: an outcome for every row, in file order, and the
/// configuration of every site that has at least one translated connector.
/// </summary>
public sealed record AssetTrackImport(
    ImmutableArray<AssetRowTranslation> Rows, ImmutableArray<SiteConfiguration> Sites);
