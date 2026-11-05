namespace Ch02.ActorsByHand;

/// <summary>The address of an actor: something you can tell a message to.</summary>
public interface ICanTell<in TMessage>
{
    void Tell(TMessage message);
}
