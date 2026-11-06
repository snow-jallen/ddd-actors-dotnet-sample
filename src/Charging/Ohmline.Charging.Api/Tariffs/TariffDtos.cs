namespace Ohmline.Charging.Api.Tariffs;

/// <summary>
/// A tariff version to publish: its id, currency and time zone, its components and an optional
/// price cap. Prices are in the tariff's currency.
/// </summary>
public sealed record PublishTariffRequest(
    string TariffId,
    string Currency,
    string TimeZoneId,
    IReadOnlyList<ComponentDto> Components,
    decimal? PriceCap = null);

/// <summary>A tariff version as the book holds it now.</summary>
public sealed record TariffResponse(
    string TariffId,
    int Version,
    string Currency,
    string TimeZoneId,
    IReadOnlyList<ComponentDto> Components,
    decimal? PriceCap);

/// <summary>
/// One pricing rule. <paramref name="Kind"/> is <c>energy-rate</c> (per kWh), <c>time-rate</c>
/// (per minute), <c>session-fee</c> or <c>idle-fee</c> (per minute after
/// <paramref name="GraceMinutes"/>, up to <paramref name="Cap"/> per session if given); a rate
/// may apply only within a time-of-use band.
/// </summary>
public sealed record ComponentDto(
    string Kind,
    decimal Price,
    int? GraceMinutes = null,
    BandDto? Band = null,
    decimal? Cap = null);

/// <summary>A window of local time: from one time up to, not including, another.</summary>
public sealed record BandDto(string Name, TimeOnly From, TimeOnly Until);
