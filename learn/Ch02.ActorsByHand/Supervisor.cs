namespace Ch02.ActorsByHand;

/// <summary>
/// A parent that keeps one <see cref="ConnectorActor"/> running. When the child crashes, the
/// supervisor's loop starts a fresh child from the factory, so callers keep one stable address.
/// The fresh child starts from its initial state: whatever the crashed child knew is gone.
/// </summary>
public sealed class Supervisor : ICanTell<ConnectorMessage>, IAsyncDisposable
{
    private readonly Func<ConnectorActor> _createChild;
    private readonly Task _loop;
    private ConnectorActor _child;

    public Supervisor(Func<ConnectorActor> createChild)
    {
        _createChild = createChild;
        _child = createChild();
        _loop = SuperviseAsync();
    }

    /// <summary>Raised on the supervisor's loop after a crashed child has been replaced.</summary>
    public event Action<Exception>? ChildRestarted;

    /// <summary>
    /// Forwards to the current child. A message told while the child is crashing is lost.
    /// </summary>
    public void Tell(ConnectorMessage message) => Volatile.Read(ref _child).Tell(message);

    private async Task SuperviseAsync()
    {
        while (true)
        {
            try
            {
                await _child.Completion;
                return; // The child stopped because it was disposed, not because it crashed.
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _child, _createChild());
                ChildRestarted?.Invoke(exception);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Volatile.Read(ref _child).DisposeAsync();
        await _loop;
    }
}
