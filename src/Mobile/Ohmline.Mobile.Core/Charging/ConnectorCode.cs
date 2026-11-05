using System.Text.RegularExpressions;

namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// A connector as it is printed on its label, and as the driver scans or types it:
/// <c>OHM-SLC-0042/1</c>, the charge point's code, a slash and the connector's number.
/// </summary>
/// <remarks>
/// The server has the same rules in its own <c>ConnectorId</c>, and its are the ones that
/// count. These are duplicated on purpose, so that a typo is caught at once, offline too, and
/// never becomes a command; when the two disagree, the server's rejection says so.
/// </remarks>
public readonly partial record struct ConnectorCode
{
    private ConnectorCode(string chargePointId, int number) =>
        (ChargePointId, Number) = (chargePointId, number);

    /// <summary>The charge point's code: <c>OHM-SLC-0042</c>.</summary>
    public string ChargePointId { get; }

    /// <summary>The connector's number on the charge point, from 1.</summary>
    public int Number { get; }

    public static ParseResult<ConnectorCode> TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Invalid("Scan or type the code on the connector's label.");
        }

        var match = Printed().Match(text.Trim().ToUpperInvariant());
        return match.Success
            ? new Parsed<ConnectorCode>(
                new ConnectorCode(match.Groups[1].Value, int.Parse(match.Groups[2].Value)))
            : new Invalid(
                $"'{text.Trim()}' is not a connector code. It looks like OHM-SLC-0042/1.");
    }

    public override string ToString() => $"{ChargePointId}/{Number}";

    [GeneratedRegex("^(OHM-[A-Z]{3}-[0-9]{4})/([1-9][0-9]?)$")]
    private static partial Regex Printed();
}
