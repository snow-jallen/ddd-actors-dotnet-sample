namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// A row's status as the outbox stores it. Only queued rows are sent; the others are final.
/// "Sending" is never stored: a command being sent when the app dies is still queued.
/// </summary>
public static class OutboxStatus
{
    public const string Queued = "queued";

    public const string Accepted = "accepted";

    public const string Rejected = "rejected";

    public const string FailedToSync = "failed-to-sync";
}
