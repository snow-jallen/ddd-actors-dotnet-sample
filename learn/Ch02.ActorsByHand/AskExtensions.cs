namespace Ch02.ActorsByHand;

public static class AskExtensions
{
    /// <summary>
    /// Tells the actor a message that carries a fresh reply address, then waits for the reply.
    /// The timeout is not optional: the actor may be busy, stopped, or never answer.
    /// </summary>
    public static async Task<TReply> AskAsync<TMessage, TReply>(
        this ICanTell<TMessage> actor,
        Func<ReplyTo<TReply>, TMessage> createMessage,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        // Without RunContinuationsAsynchronously the asker's code after the await would run on
        // the actor's loop, inside the actor's turn.
        var reply = new TaskCompletionSource<TReply>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using var registration = timeoutSource.Token.Register(() =>
        {
            if (cancellationToken.IsCancellationRequested)
                reply.TrySetCanceled(cancellationToken);
            else
                reply.TrySetException(new TimeoutException($"No reply within {timeout}."));
        });

        // A reply that arrives after the timeout finds the task completed and is dropped.
        actor.Tell(createMessage(answer => reply.TrySetResult(answer)));
        return await reply.Task;
    }
}
