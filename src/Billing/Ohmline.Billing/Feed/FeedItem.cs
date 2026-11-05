using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Feed;

/// <summary>
/// An entry of the charging feed as Billing reads it: a session to invoice, or something Billing
/// does not bill for, stepped over. Either way it has a position, which Billing stores.
/// </summary>
public union FeedItem(CompletedSession, SkippedEntry)
{
    public long Position => this switch
    {
        CompletedSession completed => completed.Position,
        SkippedEntry skipped => skipped.Position,
        null => throw new InvalidOperationException("The item is default: no case."),
    };
}

public sealed record CompletedSession(long Position, SessionCompleted Event);

/// <summary>An event of a type Billing does not read, by its wire name.</summary>
public sealed record SkippedEntry(long Position, string? Type);
