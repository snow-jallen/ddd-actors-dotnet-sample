# Anticorruption layer: the charger protocol

**Ohmline's gateway speaks a simplified subset in the style of OCPP 1.6J. It is not OCPP, it
makes no claim of conformance to any OCPP version, and it must not be used to talk to real
chargers.** It keeps OCPP's frame shapes and action names so that the patterns in Chapter 16
look like the real thing (section 16.6).

| Part | Where |
|---|---|
| `OcppFrame` (`Call`, `CallResult`, `CallError`) and `OcppFrameParser` | `src/DeviceGateway/Ohmline.DeviceGateway.Ocpp/Frames/` |
| Payload records, `OcppJson` | `.../Messages/` |
| `OcppTranslator`, `ChargerContext`, `Translation` (`Translated`, `Ignored`, `Malformed`), `OutboundCalls` | `.../Translation/` |
| Recorded frames per vendor | `tests/Ohmline.DeviceGateway.Ocpp.Tests/Frames/{vendor}/` |

## Frames

| Frame | JSON | Meaning |
|---|---|---|
| Call | `[2, "id", "Action", {payload}]` | Do this, or take note of this |
| CallResult | `[3, "id", {payload}]` | The answer to call `id` |
| CallError | `[4, "id", "ErrorCode", "description", {}]` | Call `id` could not be handled |

Text that is not a frame is a `FormationViolation`: answered with a `CallError` when it has a
message id, dropped when it does not.

## The subset

| Action | From | Becomes in Charging |
|---|---|---|
| `BootNotification` | charger | nothing; accepted, heartbeat every 60 s |
| `Heartbeat` | charger | nothing; the current time |
| `StatusNotification` | charger | `ReportConnectorFault` (Faulted), `ClearConnectorFault` (Faulted to Available), `ReleaseConnector` (Finishing to Available), `SuspendCharging` (SuspendedEV: by EV; SuspendedEVSE: by charger), `ResumeCharging` (Suspended to Charging); anything else is remembered only |
| `Authorize` | charger | nothing; any well-formed token is accepted, Driver Accounts decides when the journey locks it |
| `StartTransaction` | charger | `OccupyConnector` and `StartSession`, only for a start a journey asked for; anything else is answered `Invalid` |
| `MeterValues` | charger | `RecordMeterValue` per energy-register sample, in Wh |
| `StopTransaction` | charger | `RecordMeterValue` (the stop reading) and `StopSession` |
| `RemoteStartTransaction` | Ohmline | the journey's `RemoteStartAsync` |
| `RemoteStopTransaction` | Ohmline | the journey's `RemoteStopAsync` |
| `SetChargingProfile` | Ohmline | a new allocation (section 16.8); the profile id is the allocation's version |

Any other action is answered `NotImplemented` and ignored.

## Words that stop at the gateway

| OCPP | Ohmline |
|---|---|
| transaction (`transactionId`, an integer the central system assigns) | charging session (`SessionId`, a Guid); the number is derived from the session id |
| `idTag` | `TokenUid`, upper-cased |
| `connectorId` | connector number; `ConnectorId` is `OHM-SLC-0042/1` |
| `meterStart`, `meterStop`, sampled values | registers in Wh (`MeterStartWh`, `RegisterWh`) |
| `SuspendedEV`, `SuspendedEVSE` | suspended by EV, suspended by charger |

## Vendor quirks normalized

| Quirk | Vendor (fictional) | Normalized to |
|---|---|---|
| Energy register in kWh with decimals (`"1210.5"`) | Chargewell CW-11 | Wh (`1210500`) |
| No `measurand` or no `unit` on a sample | Chargewell CW-11 | the energy register, in Wh |
| Token in lower case | Chargewell CW-11 | upper case |
| Times without an offset (`2026-11-04T14:40:00`) | Chargewell CW-11 | UTC |
| No `transactionId` with meter values | Chargewell CW-11 | the transaction on the connector |
| Power readings beside the register | Voltaris VX-22 | ignored |
