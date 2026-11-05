using System.Collections.Immutable;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain;

/// <summary>
/// One published version of a tariff: the pricing rules the tariff book holds for the sites of
/// one time zone. A version never changes once published; <see cref="Revise"/> makes the next
/// one, and a session copies the version that applies when it starts (<see cref="Snapshot"/>).
/// </summary>
public sealed record Tariff
{
    private Tariff(
        TariffId tariffId,
        int version,
        Currency currency,
        ImmutableArray<TariffComponent> components,
        Money? priceCap,
        RoundingPolicy rounding,
        string timeZoneId)
    {
        TariffId = tariffId;
        Version = version;
        Currency = currency;
        Components = components;
        PriceCap = priceCap;
        Rounding = rounding;
        TimeZoneId = timeZoneId;
    }

    public TariffId TariffId { get; }

    /// <summary>1 for the first publication, one more for each revision.</summary>
    public int Version { get; }

    /// <summary>The currency of every amount in the tariff.</summary>
    public Currency Currency { get; }

    public ImmutableArray<TariffComponent> Components { get; }

    /// <summary>The most a session can cost, whatever its components add up to.</summary>
    public Money? PriceCap { get; }

    public RoundingPolicy Rounding { get; }

    /// <summary>The IANA id of the time zone the bands are written in.</summary>
    public string TimeZoneId { get; }

    public static Result<Tariff, ValidationError> TryCreate(
        TariffId tariffId,
        int version,
        Currency currency,
        IEnumerable<TariffComponent> components,
        Money? priceCap,
        RoundingPolicy rounding,
        string timeZoneId)
    {
        var tariff = new Tariff(
            tariffId, version, currency, [.. components], priceCap, rounding, timeZoneId);
        return tariff.Problem() is { } problem ? Result.Fail(problem) : Result.Ok(tariff);
    }

    /// <summary>For values known to be valid. An invalid value is a bug, so it throws.</summary>
    public static Tariff Create(
        TariffId tariffId,
        int version,
        Currency currency,
        IEnumerable<TariffComponent> components,
        Money? priceCap,
        RoundingPolicy rounding,
        string timeZoneId) =>
        TryCreate(tariffId, version, currency, components, priceCap, rounding, timeZoneId)
            .Match(valid => valid, error => throw new ArgumentException(error.Message));

    /// <summary>The next version, with new rules. This version stays as it was.</summary>
    public Result<Tariff, ValidationError> Revise(
        IEnumerable<TariffComponent> components, Money? priceCap) =>
        TryCreate(TariffId, Version + 1, Currency, components, priceCap, Rounding, TimeZoneId);

    /// <summary>The copy a session keeps: this version, as it is now, for good.</summary>
    public TariffSnapshot Snapshot() =>
        new(TariffId, Version, Currency, Components, PriceCap, Rounding, TimeZoneId);

    public bool Equals(Tariff? other) => other is not null && Snapshot() == other.Snapshot();

    public override int GetHashCode() => Snapshot().GetHashCode();

    private ValidationError? Problem()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId))
        {
            return new Missing("Time zone");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out _))
        {
            return new Malformed("Time zone", TimeZoneId, "an IANA time zone like America/Denver");
        }

        if (Version < 1)
        {
            return new OutOfRange("Tariff version", Version, "1 or more");
        }

        return Components.SelectMany(Prices)
                .Concat(PriceCap is { } cap ? [("Price cap", cap)] : [])
                .Select(PriceProblem)
                .FirstOrDefault(problem => problem is not null)
            ?? CountProblem<SessionFee>("session fee")
            ?? CountProblem<IdleFee>("idle fee")
            ?? GraceProblem()
            ?? BandProblem(Components.OfType<EnergyRate>().Select(rate => rate.Band), "energy")
            ?? BandProblem(Components.OfType<TimeRate>().Select(rate => rate.Band), "time");
    }

    private ValidationError? PriceProblem((string Name, Money Price) priced) => priced switch
    {
        { Price.Currency: var other } when other != Currency =>
            new Malformed($"{priced.Name} currency", $"{other}", $"{Currency}, the tariff's"),
        { Price.Amount: < 0m } => new OutOfRange(priced.Name, priced.Price.Amount, "0 or more"),
        _ => null,
    };

    private ValidationError? CountProblem<TComponent>(string name)
        where TComponent : TariffComponent =>
        Components.OfType<TComponent>().Count() > 1
            ? (ValidationError?)new Malformed(
                "Components", $"more than one {name}", $"at most one {name}")
            : null;

    private ValidationError? GraceProblem() =>
        Components.OfType<IdleFee>().SingleOrDefault() is { GracePeriod: var grace }
        && grace < TimeSpan.Zero
            ? (ValidationError?)new OutOfRange(
                "Grace period", (decimal)grace.TotalMinutes, "0 minutes or more")
            : null;

    /// <summary>
    /// Every moment has at most one rate of a kind: at most one rate without a band, bands
    /// that do not overlap, and no band that starts where it ends.
    /// </summary>
    private static ValidationError? BandProblem(IEnumerable<TimeOfUseBand?> bands, string kind)
    {
        var all = bands.ToList();
        var banded = all.OfType<TimeOfUseBand>().ToList();

        if (all.Count(band => band is null) > 1)
        {
            return new Malformed(
                $"{kind} rates", "two rates without a band", "at most one rate without a band");
        }

        if (banded.FirstOrDefault(band => band.From == band.Until) is { } empty)
        {
            return new Malformed("Time-of-use band", $"{empty}", "a start and an end that differ");
        }

        return banded
            .SelectMany((band, index) => banded.Skip(index + 1)
                .Where(band.Overlaps)
                .Select(other => (ValidationError?)new Malformed(
                    $"{kind} rate bands", $"{band} and {other}", "bands that do not overlap")))
            .FirstOrDefault();
    }

    private static IEnumerable<(string Name, Money Price)> Prices(TariffComponent component) =>
        component switch
        {
            EnergyRate rate => [("Energy rate", rate.PricePerKwh)],
            TimeRate rate => [("Time rate", rate.PricePerMinute)],
            SessionFee fee => [("Session fee", fee.Amount)],
            IdleFee fee => [("Idle fee", fee.PricePerMinute)],
        };
}
