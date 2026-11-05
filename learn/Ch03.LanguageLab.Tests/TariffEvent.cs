using System.Text.Json.Serialization;

namespace Ch03.LanguageLab.Tests;

/// <summary>
/// Explicit discriminators: the names on the wire are chosen, not taken from the type names, so
/// renaming a record does not break what was already written.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$event")]
[JsonDerivedType(typeof(TariffPublished), "tariffs.tariff-published.v1")]
[JsonDerivedType(typeof(TariffWithdrawn), "tariffs.tariff-withdrawn.v1")]
public closed record TariffEvent(string TariffId);

public sealed record TariffPublished(string TariffId, decimal PricePerKwh) : TariffEvent(TariffId);

public sealed record TariffWithdrawn(string TariffId) : TariffEvent(TariffId);
