using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.ChargePoints;

namespace Ohmline.Charging.Actors;

/// <summary>
/// From the domain's reasons to the wire's replies. A decider refuses with a union, which stays
/// inside the actor; the sender gets a <see cref="CommandRejected"/> with a stable code. The
/// switch is exhaustive, so a new reason does not compile until it has a code. Codes are
/// published vocabulary: add new ones, never change or reuse one.
/// </summary>
public static class ErrorMapping
{
    extension(ChargePointError error)
    {
        public string Code => error switch
        {
            ChargePointError.NotRegistered => "not-registered",
            ChargePointError.AlreadyRegistered => "already-registered",
            ChargePointError.UnknownConnector => "unknown-connector",
            ChargePointError.ConnectorUnavailable => "connector-unavailable",
            ChargePointError.ReservedForAnotherToken => "reserved-for-another-token",
            ChargePointError.ConnectorFaulted => "connector-faulted",
            null => throw DefaultError(nameof(ChargePointError)),
        };

        public CommandRejected ToReply() => new(error.Code, error.Message);
    }

    private static InvalidOperationException DefaultError(string union) =>
        new($"This {union} is default: it holds no case, so there is no reason to report.");
}
