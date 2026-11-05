using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Contracts.ChargePoints;

/// <summary>
/// Everything a charge point can be asked to do, named as on the design-level EventStorming
/// board (<c>docs/eventstorming/design-level.md</c>). The base is closed: only this assembly
/// can add a command, so a switch over it anywhere else knows every case. Every command names
/// the charge point it is for, which is how the shell routes it (section 12.2), and carries the
/// id its sender gave it, which every event it causes records (section 8.1).
/// </summary>
public closed record ChargePointCommand(CommandId CommandId, ChargePointId ChargePointId);

/// <summary>
/// Add a charge point and its connectors to Ohmline's network. Connectors are numbered from 1
/// without gaps, as the charge point itself numbers them, so connector n is the nth entry.
/// </summary>
public sealed record RegisterChargePoint(
    CommandId CommandId,
    ChargePointId ChargePointId,
    SiteId SiteId,
    ImmutableArray<Power> ConnectorMaxPowers)
    : ChargePointCommand(CommandId, ChargePointId);

/// <summary>A command about one connector of the charge point.</summary>
public closed record ConnectorCommand(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber)
    : ChargePointCommand(CommandId, ChargePointId);

/// <summary>Keep a connector for one token until the reservation expires.</summary>
public sealed record ReserveConnector(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber, TokenUid TokenUid)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>The driver who holds the reservation no longer wants the connector.</summary>
public sealed record CancelReservation(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber, TokenUid TokenUid)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>The reservation's time is up; sent by a timer, not by a person.</summary>
public sealed record ExpireReservation(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>A car has been plugged in and the token authorized to start this session.</summary>
public sealed record OccupyConnector(
    CommandId CommandId,
    ChargePointId ChargePointId,
    int ConnectorNumber,
    SessionId SessionId,
    TokenUid TokenUid)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>The car has been unplugged; the connector can take the next one.</summary>
public sealed record ReleaseConnector(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>
/// The charge point reports that a connector cannot be used. The error code is the charge
/// point's own, such as <c>GroundFailure</c>, kept for the technician.
/// </summary>
public sealed record ReportConnectorFault(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber, string ErrorCode)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>The connector has been repaired and can be used again.</summary>
public sealed record ClearConnectorFault(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>Operators take a working connector out of use: maintenance, a closed bay.</summary>
public sealed record MakeConnectorUnavailable(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);

/// <summary>Operators put a connector they took out of use back into service.</summary>
public sealed record MakeConnectorAvailable(
    CommandId CommandId, ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorCommand(CommandId, ChargePointId, ConnectorNumber);
