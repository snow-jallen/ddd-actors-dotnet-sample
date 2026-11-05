using Ohmline.Charging.Client.Contracts;

namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// The server's rejection codes in the driver's words: "This connector is reserved for another
/// driver", never "409". A newer server may send a code this build of the app does not know
/// yet, which gets a general sentence instead of an error.
/// </summary>
public static class RejectionText
{
    public const string Fallback =
        "Ohmline could not do that just now. Try again, or contact support if it happens again.";

    public static string For(string? code) => code switch
    {
        RejectionCodes.InvalidRequest =>
            "Something in the request was not right. Check the connector code and the spend cap.",
        RejectionCodes.UnknownConnector =>
            "Ohmline does not know this connector. Check the code on its label.",
        RejectionCodes.NotRegistered => "This charge point is not in service yet.",
        RejectionCodes.ConnectorUnavailable => "This connector is out of service or in use.",
        RejectionCodes.ReservedForAnotherToken =>
            "This connector is reserved for another driver.",
        RejectionCodes.ConnectorFaulted => "This connector has a fault and is waiting for repair.",
        RejectionCodes.NotStarted => "This charge never started.",
        RejectionCodes.AlreadyEnded => "This charge has already ended.",
        RejectionCodes.JourneyExists => "This charge was already started. Pull to refresh.",
        RejectionCodes.IdempotencyKeyMismatch =>
            "The app sent a request it should not have. Update the app and try again.",
        _ => Fallback,
    };
}
