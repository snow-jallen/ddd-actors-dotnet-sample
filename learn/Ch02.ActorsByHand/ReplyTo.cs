namespace Ch02.ActorsByHand;

/// <summary>
/// A reply address. A message that expects an answer carries one, so the actor can answer
/// without knowing who asked, and the answer can go to whoever the sender chose.
/// </summary>
public delegate void ReplyTo<in TReply>(TReply reply);
