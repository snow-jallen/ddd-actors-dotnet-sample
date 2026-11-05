using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Api.Tariffs;

/// <summary>
/// Where the edge finds the version of a tariff that applies now, to copy it into a session
/// that starts. Charging conforms to Tariffs' model of a tariff and only reads it. The host
/// supplies the implementation.
/// </summary>
public interface ITariffCatalog
{
    /// <summary>The tariff's current version, or null when no such tariff is published.</summary>
    Task<TariffSnapshot?> CurrentAsync(TariffId tariffId, CancellationToken cancellationToken);
}
