using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Contracts.Tokens;

/// <summary>A question for an id token: a family of its own, never a command.</summary>
public closed record IdTokenQuery(TokenUid TokenUid);

/// <summary>Whose token is it, may it charge, and is it charging now?</summary>
public sealed record GetIdToken(TokenUid TokenUid) : IdTokenQuery(TokenUid);
