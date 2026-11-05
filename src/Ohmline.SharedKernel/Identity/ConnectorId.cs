namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// A connector: a numbered plug on a charge point. A composite identity, because connector 1
/// means nothing on its own. Written as the charge point id, a slash and the number, such as
/// <c>OHM-SLC-0042/1</c>.
/// </summary>
public readonly record struct ConnectorId : IValueObject<ConnectorId, string>
{
    public ConnectorId(ChargePointId chargePointId, int number)
    {
        ChargePointId = chargePointId;
        Number = number;
    }

    public ChargePointId ChargePointId
    {
        get;
        init => field = value.Value is not null
            ? value
            : throw new ArgumentException(
                "A connector needs its charge point.", nameof(ChargePointId));
    }

    public int Number
    {
        get;
        init => field = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Number), value, "Numbers start at 1.");
    }

    string IValueObject<ConnectorId, string>.Value => ToString();

    public static Result<ConnectorId, ValidationError> TryCreate(
        ChargePointId chargePointId, int number) =>
        number >= 1
            ? Result.Ok(new ConnectorId(chargePointId, number))
            : Result.Fail<ValidationError>(new OutOfRange("Connector number", number, "1 or more"));

    /// <summary>Reads the written form, <c>OHM-SLC-0042/1</c>.</summary>
    public static Result<ConnectorId, ValidationError> TryCreate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Fail<ValidationError>(new Missing(nameof(ConnectorId)));
        }

        return value.Split('/') is [var chargePoint, var number]
            && int.TryParse(number, out var parsed)
            ? ChargePointId.TryCreate(chargePoint).Bind(id => TryCreate(id, parsed))
            : Result.Fail<ValidationError>(
                new Malformed(nameof(ConnectorId), value, "a charge point id, /, a number"));
    }

    public override string ToString() => $"{ChargePointId}/{Number}";
}
