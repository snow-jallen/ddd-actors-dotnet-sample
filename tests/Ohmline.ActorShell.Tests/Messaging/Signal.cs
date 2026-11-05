namespace Ohmline.ActorShell.Tests.Messaging;

/// <summary>A small protocol union to send around; its cases are plain records.</summary>
public union Signal(Ping, Pong);

public sealed record Ping(string Text);

public sealed record Pong(int Count);
