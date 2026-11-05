using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Actors.Tokens;

/// <summary>From the token's state to its snapshot and its view; from the snapshot back.</summary>
public static class IdTokenMapping
{
    extension(IdTokenState state)
    {
        /// <summary>Null before registration: there is nothing to keep.</summary>
        public IdTokenSnapshot? ToSnapshot() => state switch
        {
            Unknown => null,
            Active active => new(
                active.TokenUid, active.Driver, Blocked: false, active.ActiveSession),
            Blocked blocked => new(
                blocked.TokenUid, blocked.Driver, Blocked: true, blocked.ActiveSession),
            null => throw new InvalidOperationException("The state is default: it holds no case."),
        };

        public IdTokenView ToView(TokenUid tokenUid, long version) => state switch
        {
            Unknown => new(tokenUid, version, IdTokenView.Unknown),
            Active active =>
                new(tokenUid, version, IdTokenView.Active, active.Driver, active.ActiveSession),
            Blocked blocked =>
                new(tokenUid, version, IdTokenView.Blocked, blocked.Driver, blocked.ActiveSession),
            null => throw new InvalidOperationException("The state is default: it holds no case."),
        };
    }

    extension(IdTokenSnapshot snapshot)
    {
        public IdTokenState ToState() => snapshot.Blocked
            ? new Blocked(snapshot.TokenUid, snapshot.Driver, snapshot.ActiveSession)
            : new Active(snapshot.TokenUid, snapshot.Driver, snapshot.ActiveSession);
    }
}
