using System.Threading.Channels;

namespace Ch02.ActorsByHand;

/// <summary>
/// An actor's mailbox. Any thread may tell it a message; a single loop takes the messages out
/// one at a time, in arrival order, and hands each to the handler. The handler is therefore the
/// only code that ever touches the actor's state, and it never runs on two threads at once.
/// </summary>
public sealed class Mailbox<TMessage> : IAsyncDisposable
{
    private readonly Channel<TMessage> _channel =
        Channel.CreateUnbounded<TMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Action<TMessage> _handle;
    private readonly Task _loop;

    public Mailbox(Action<TMessage> handle)
    {
        _handle = handle;
        _loop = Task.Run(ProcessMessagesAsync);
    }

    /// <summary>Completes when the mailbox stops, and faults if the handler threw.</summary>
    public Task Completion => _loop;

    /// <summary>Queues the message and returns at once. A stopped mailbox drops it.</summary>
    public void Tell(TMessage message) => _channel.Writer.TryWrite(message);

    private async Task ProcessMessagesAsync()
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync())
                _handle(message);
        }
        catch (Exception exception)
        {
            _channel.Writer.TryComplete(exception);
            throw;
        }
    }

    /// <summary>Stops accepting messages, processes those already queued, then stops.</summary>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _loop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}
