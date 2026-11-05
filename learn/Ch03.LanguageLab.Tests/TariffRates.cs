using System.Collections.Immutable;

namespace Ch03.LanguageLab.Tests;

/// <summary>A record with a collection member, to show where value equality stops.</summary>
public sealed record TariffRates(string TariffId, ImmutableArray<decimal> PricePerKwh);
