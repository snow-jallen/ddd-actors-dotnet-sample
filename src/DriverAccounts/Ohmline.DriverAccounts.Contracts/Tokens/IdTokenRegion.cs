namespace Ohmline.DriverAccounts.Contracts.Tokens;

/// <summary>
/// The key under which a node's actor registry holds the id token shard region
/// (<c>id-token</c>): code that sends token commands asks for
/// <c>IRequiredActor&lt;IdTokenRegion&gt;</c> and needs no reference to the actors.
/// </summary>
public sealed class IdTokenRegion;
