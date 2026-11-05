using System.Collections.Immutable;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Domain.Policies;

/// <summary>
/// A lilac sticky as code: whenever this happens, then that. A pure reaction from one event to
/// the intents it causes, often none. Policies are strategies behind one interface, so the shell
/// runs whichever apply to a session without knowing which they are.
/// </summary>
public interface ISessionPolicy
{
    ImmutableArray<ChargingIntent> React(SessionEvent @event);
}
