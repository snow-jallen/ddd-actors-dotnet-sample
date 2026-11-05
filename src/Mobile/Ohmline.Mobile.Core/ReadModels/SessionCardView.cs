using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Outbox;

namespace Ohmline.Mobile.Core.ReadModels;

/// <summary>
/// The session card as the driver sees it (section 19.7): the card from the cache, which is
/// what the server's read model said, and a pending overlay for what the driver asked for and
/// the read model does not show yet. Optimistic, without lying: "Stopping..." stays until the
/// card itself has the version the server accepted the stop at, not merely until the server
/// said yes, so the overlay never disappears ahead of the facts.
/// </summary>
/// <param name="Card">The session charging now, or else the most recent; none before any.</param>
/// <param name="Pending">What the driver is waiting for, such as "Stopping..."; or none.</param>
/// <param name="Problem">Why the driver's last command came to nothing, in the app's words.</param>
public sealed record SessionCardView(DriverSessionCard? Card, string? Pending, string? Problem)
{
    public const string Starting = "Starting...";

    public const string Stopping = "Stopping...";

    public const string NotSent =
        "Not sent: the phone had no connection for a day. Try again.";

    /// <summary>
    /// A pure function of the cache and the outbox, oldest command first. A start is shown once
    /// its session has a card: the session is named after the journey. A stop is shown once
    /// the card has the version the stop was accepted at.
    /// </summary>
    public static SessionCardView Merge(
        IReadOnlyList<DriverSessionCard> cards, IReadOnlyList<OutboxRow> commands)
    {
        var bySession = cards.ToDictionary(card => card.SessionId);
        string? pending = null, problem = null;
        foreach (var command in commands)
        {
            if (Overlay(command.Kind) is not { } overlay)
            {
                continue;
            }

            bySession.TryGetValue(Guid.Parse(command.AggregateKey), out var card);
            (pending, problem) = command.ToStatus() switch
            {
                Queued or Sending => (overlay, null),
                Accepted accepted when !Shows(card, command.Kind, accepted.Version) =>
                    (overlay, null),
                Accepted => (pending, null),
                Rejected rejected => (pending, rejected.Text),
                FailedToSync => (pending, NotSent),
                null => throw new InvalidOperationException("The status is default."),
            };
        }

        var shown = cards.FirstOrDefault(card => card.EndedAt is null) ?? cards.FirstOrDefault();
        return new SessionCardView(shown, pending, problem);
    }

    private static string? Overlay(string kind) => kind switch
    {
        OutboxKinds.StartCharging => Starting,
        OutboxKinds.StopCharging => Stopping,
        _ => null,
    };

    /// <summary>Whether the card shows what the command, accepted at a version, did.</summary>
    private static bool Shows(DriverSessionCard? card, string kind, long accepted) =>
        card is not null && (kind != OutboxKinds.StopCharging || card.Version >= accepted);
}
