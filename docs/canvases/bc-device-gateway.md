# Bounded Context Canvas: Device Gateway

*Bounded Context Canvas V5 (ddd-crew), filled in during section 5.1.*

## Name

**Device Gateway**

## Purpose

Speak to every charge point in its own protocol, a simplified subset in the style of OCPP 1.6J,
and translate between that protocol and Ohmline's language in both directions, so that no
protocol word and no vendor quirk ever reaches Charging.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Supporting (charger communication) | Cost reducer | Product |

## Domain roles

- **Gateway:** the only way in and out between Ohmline and the charge points.
- **Interchange:** it translates; it makes no business decisions of its own.

## Inbound communication

The charge points' messages are the protocol's, not Ohmline's, and stop at the gateway. Charging's
commands are public contracts in `Ohmline.Charging.Contracts`.

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charge points | BootNotification, Heartbeat, StatusNotification | Event | The charger protocol |
| Charge points | Authorize, StartTransaction, MeterValues, StopTransaction | Command and event | The charger protocol |
| Charging (journey, charging profile dispatcher) | `RemoteStart`, `RemoteStop`, `SetChargingProfile` | Command | Charger command port (section 15.4) |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging | `OccupyConnector`, `ReleaseConnector`, `ReportConnectorFault`, `ClearConnectorFault` | Command | `Ohmline.Charging.Contracts` |
| Charging | `StartSession`, `RecordMeterValue`, `SuspendCharging`, `ResumeCharging`, `StopSession` | Command | `Ohmline.Charging.Contracts` |
| Charging | `ChargerAccepted`, `ChargerRejected` | Event (reply) | Charger command port (section 15.4) |
| Charge points | RemoteStartTransaction, RemoteStopTransaction, SetChargingProfile | Command | The charger protocol |

## Ubiquitous language

The gateway is bilingual. The left column is the protocol's word; the right column is what it
becomes inside Ohmline.

| Protocol word | Inside Ohmline |
|---|---|
| Transaction (OCPP transaction) | Charging session |
| StartTransaction / StopTransaction | `StartSession` / `StopSession` |
| Authorize | `LockForSession`, through the charging journey |
| StatusNotification "Charging", "SuspendedEV", "Faulted" | `OccupyConnector`, `SuspendCharging`, `ReportConnectorFault` |
| MeterValues (in Wh or kWh, by vendor) | `RecordMeterValue` (always an `Energy` register) |
| Charging profile (`SetChargingProfile`) | Keeps its external name |

## Business decisions

- A frame that cannot be parsed or translated is answered with a protocol error and logged; it
  never becomes a command.
- Each vendor's quirks (energy in Wh or kWh, status names, extra fields) are normalized here and
  nowhere else.
- A charge point that misses three heartbeats is reported as unavailable.
- The gateway decides nothing about sessions, prices or power; it forwards.

## Assumptions

- A charge point keeps one WebSocket connection open and reconnects on its own.
- Each vendor's firmware dialect is stable within a firmware version.
- The gateway and Charging run in the same Akka.NET cluster and release together (the
  Partnership, `docs/context-map.md`).

## Verification metrics

- Fewer than 1 in 10,000 frames rejected as malformed per vendor.
- A remote start reaches the charge point within 1 s of the journey's request.
- A new vendor quirk is covered by a recorded-frame test before the vendor's chargers go live.

## Open questions

- Should outbound commands reach a charge point through a shard region or a pub-sub topic per
  charge point (section 16.7)?
- How do we learn about a vendor's firmware change before its chargers start sending new
  frames?
