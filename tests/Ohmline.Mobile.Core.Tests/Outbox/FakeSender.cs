using Ohmline.Mobile.Core.Outbox;

namespace Ohmline.Mobile.Core.Tests.Outbox;

/// <summary>
/// The server as the dispatcher's tests play it: each send gets the next queued outcome, or is
/// accepted at version 1 when none is queued; every row sent is remembered, in order.
/// </summary>
internal sealed class FakeSender : ICommandSender
{
    private readonly Queue<SendOutcome> _outcomes = new();

    public List<OutboxRow> Sent { get; } = [];

    public FakeSender Then(SendOutcome outcome)
    {
        _outcomes.Enqueue(outcome);
        return this;
    }

    public Task<SendOutcome> SendAsync(OutboxRow row, CancellationToken cancellationToken)
    {
        Sent.Add(row);
        return Task.FromResult(_outcomes.TryDequeue(out var outcome)
            ? outcome
            : new SendAccepted(1));
    }
}
