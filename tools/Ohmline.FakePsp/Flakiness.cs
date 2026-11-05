using System.Globalization;

namespace Ohmline.FakePsp;

/// <summary>
/// How often TallyPay fails for no reason of the caller's: the <c>FLAKY_RATE</c> setting, from 0
/// (never) to 1 (always). A failed request answers 503 and changes nothing.
/// </summary>
public sealed class Flakiness(IConfiguration configuration)
{
    private readonly double _rate = Math.Clamp(
        double.TryParse(
            configuration["FLAKY_RATE"],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var rate)
            ? rate
            : 0d,
        0d,
        1d);

    public bool Strikes() => _rate > 0d && Random.Shared.NextDouble() < _rate;
}
