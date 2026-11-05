using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// Why the charge point refused a command. The cases live inside the union because some of them
/// share a name with an event: <c>ConnectorFaulted</c> is a fact the charge point records, and
/// <c>ChargePointError.ConnectorFaulted</c> is a reason to refuse a driver.
/// </summary>
public union ChargePointError(
    ChargePointError.NotRegistered,
    ChargePointError.AlreadyRegistered,
    ChargePointError.UnknownConnector,
    ChargePointError.ConnectorUnavailable,
    ChargePointError.ReservedForAnotherToken,
    ChargePointError.ConnectorFaulted)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        NotRegistered error => $"Charge point {error.ChargePointId} is not registered.",
        AlreadyRegistered error => $"Charge point {error.ChargePointId} is already registered.",
        UnknownConnector error => $"The charge point has no connector {error.ConnectorNumber}.",
        ConnectorUnavailable error =>
            $"Connector {error.ConnectorNumber} is in use or out of use.",
        ReservedForAnotherToken error => FormattableString.Invariant(
            $"Connector {error.ConnectorNumber} is reserved for another driver until {
                error.Until:u}."),
        ConnectorFaulted error =>
            $"Connector {error.ConnectorNumber} is faulted ({error.ErrorCode}).",
        null => "This ChargePointError is default: it holds no case.",
    };

    /// <summary>Before registration, only <c>RegisterChargePoint</c> is accepted.</summary>
    public sealed record NotRegistered(ChargePointId ChargePointId);

    public sealed record AlreadyRegistered(ChargePointId ChargePointId);

    public sealed record UnknownConnector(int ConnectorNumber);

    /// <summary>A session is using the connector, or operators took it out of use.</summary>
    public sealed record ConnectorUnavailable(int ConnectorNumber);

    /// <summary>Someone else holds the reservation; the driver learns when it runs out.</summary>
    public sealed record ReservedForAnotherToken(int ConnectorNumber, DateTimeOffset Until);

    public sealed record ConnectorFaulted(int ConnectorNumber, string ErrorCode);
}
