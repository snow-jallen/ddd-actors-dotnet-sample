using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// One row of the outbox table: a command, and what has become of it so far. The server's
/// answer is kept with it: the version it was accepted at, or the code it was rejected with.
/// </summary>
public sealed record OutboxRow(
    Guid CommandId,
    string Kind,
    string AggregateKey,
    string Payload,
    DateTimeOffset CreatedUtc,
    int Attempts,
    string Status,
    long? Version,
    string? Code,
    string? Reason,
    string? TraceParent = null)
{
    /// <summary>The row's status in the app's language, for a screen to show.</summary>
    public CommandStatus ToStatus() => Status switch
    {
        OutboxStatus.Accepted => new Accepted(Version ?? 0),
        OutboxStatus.Rejected => new Rejected(Code ?? "", RejectionText.For(Code)),
        OutboxStatus.FailedToSync => new FailedToSync(),
        _ => new Queued(Attempts),
    };
}
