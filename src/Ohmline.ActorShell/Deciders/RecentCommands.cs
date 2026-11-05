using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// The commands an aggregate accepted most recently, by id, with the version each one left it
/// at: a bounded window, in which the command used least recently is forgotten first. The shell
/// answers a command whose id is in the window with that version, without deciding it again.
/// </summary>
/// <remarks>
/// Only accepted commands that stored events are remembered: they are what the journal can
/// rebuild the window from, because every event records the id of its command. A command that
/// was rejected, or that changed nothing, changed nothing; deciding it again is safe.
/// </remarks>
public sealed class RecentCommands(int capacity = RecentCommands.DefaultCapacity)
{
    /// <summary>
    /// Enough for retries, which come within seconds, and small enough to keep in every entity
    /// and every snapshot.
    /// </summary>
    public const int DefaultCapacity = 256;

    private readonly Dictionary<CommandId, LinkedListNode<RecentCommand>> _byId = [];
    private readonly LinkedList<RecentCommand> _byUse = new(); // The most recently used first.

    public int Count => _byId.Count;

    /// <summary>Remembers the command at this version; a later event of it moves it on.</summary>
    public void Record(CommandId commandId, long version)
    {
        if (_byId.Remove(commandId, out var known))
        {
            _byUse.Remove(known);
        }

        _byId[commandId] = _byUse.AddFirst(new RecentCommand(commandId, version));
        if (_byId.Count > capacity)
        {
            _byId.Remove(_byUse.Last!.Value.CommandId);
            _byUse.RemoveLast();
        }
    }

    /// <summary>The version the command left the aggregate at, if it is in the window.</summary>
    public bool TryGetVersion(CommandId commandId, out long version)
    {
        if (_byId.TryGetValue(commandId, out var known))
        {
            _byUse.Remove(known);
            _byUse.AddFirst(known);
            version = known.Value.Version;
            return true;
        }

        version = 0;
        return false;
    }

    /// <summary>The window for a snapshot: the least recently used first.</summary>
    public ImmutableArray<RecentCommand> ToSnapshot() => [.. _byUse.Reverse()];

    /// <summary>The window as a snapshot stored it, in the same order of use.</summary>
    public void Restore(IEnumerable<RecentCommand> snapshot)
    {
        _byId.Clear();
        _byUse.Clear();
        foreach (var recent in snapshot)
        {
            Record(recent.CommandId, recent.Version);
        }
    }
}
