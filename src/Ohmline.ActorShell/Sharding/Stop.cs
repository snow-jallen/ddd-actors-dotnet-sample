namespace Ohmline.ActorShell.Sharding;

/// <summary>
/// What the shard tells an entity when it must stop: after it has been idle too long
/// (passivation), before its shard moves to another node (hand-off), and at shutdown. An
/// ordinary message, unlike <c>PoisonPill</c>, which stops a persistent actor at once, even
/// with a write in flight. This one waits in the mailbox behind the commands that arrived
/// before it, and in the internal stash behind a write, so the entity answers them first.
/// </summary>
public sealed record Stop
{
    public static Stop Instance { get; } = new();
}
