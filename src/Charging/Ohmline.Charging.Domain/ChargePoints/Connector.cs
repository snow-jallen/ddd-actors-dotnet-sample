using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// A connector as its charge point sees it. An entity: it stays the same connector while its
/// state changes. Not an actor: it lives inside the charge point's state and only the charge
/// point changes it, so a rule such as "one session per connector" is checked in one place.
/// As a record it compares every member; to compare identities, use
/// <see cref="ConnectorComparer.ById"/>.
/// </summary>
public sealed record Connector(ConnectorId Id, Power MaxPower, ConnectorState State);
