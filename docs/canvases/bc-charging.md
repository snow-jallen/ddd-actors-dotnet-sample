# Bounded Context Canvas: Charging

*Bounded Context Canvas V5 (ddd-crew), filled in during section 5.1. The canvases for the other
contexts are in this folder; `docs/context-map.md` shows how they relate.*

## Name

**Charging**

## Purpose

Run Ohmline's charge points from the plug's point of view: which connector is available, reserved,
occupied or faulted, and what happens during each charging session, from the first meter reading
to the last. Charging also runs the charging journey, the process that takes a driver from "start
charging" to a settled payment across the other contexts.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Core (charging session management) | Revenue generator, engagement creator | Custom built |

## Domain roles

- **Execution model:** it carries out the work (sessions, reservations) under rules other
  contexts set (tariffs, power allocations, token locks).
- **Enforcer:** a connector hosts at most one session, and a reserved connector accepts only the
  token that holds the reservation.

## Inbound communication

Every message in these tables is a public contract. Its type lives in a `Contracts` project (or,
for the charge point's own words, in the Device Gateway's protocol), it crosses a serializer, and
it changes only by adding a new version next to the old one (sections 10.3 and 20.2).

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Device Gateway | `OccupyConnector`, `ReleaseConnector`, `ReportConnectorFault`, `ClearConnectorFault` | Command | `Ohmline.Charging.Contracts` |
| Device Gateway | `StartSession`, `RecordMeterValue`, `SuspendCharging`, `ResumeCharging`, `StopSession` | Command | `Ohmline.Charging.Contracts` |
| Device Gateway | `ChargerAccepted`, `ChargerRejected` (answers to a remote start) | Event (reply) | Charger command port (section 15.4) |
| Driver app, operators (HTTP) | `ReserveConnector`, `CancelReservation`, start charging | Command | `Ohmline.Charging.Contracts` |
| Operators (HTTP) | `RegisterChargePoint`, `MakeConnectorUnavailable`, `MakeConnectorAvailable`, `ReviseChargePointListing` | Command | `Ohmline.Charging.Contracts` |
| Driver app, operators (HTTP) | Charge point status, listing, session history, site live board | Query | `Ohmline.Charging.Contracts` |
| Site Energy | Allocation granted, allocation queued | Event (reply) | `Ohmline.SiteEnergy.Contracts` |
| Driver Accounts | `TokenLocked`, `TokenLockRefused` | Event (reply) | `Ohmline.DriverAccounts.Contracts` |
| Tariffs | The tariff snapshot for a site | Event (reply) | `Ohmline.Tariffs.Contracts` |
| Billing | Hold placed, hold declined, payment captured, capture failed | Event (reply) | `Ohmline.Billing.Contracts` |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Site Energy | `RequestAllocation`, `ReleaseAllocation` | Command | `Ohmline.SiteEnergy.Contracts` |
| Driver Accounts | `LockForSession`, `UnlockFromSession` | Command | `Ohmline.DriverAccounts.Contracts` |
| Tariffs | Get the tariff snapshot for a site | Query | `Ohmline.Tariffs.Contracts` |
| Billing | Place hold, capture payment, void hold (HTTP) | Command | `Ohmline.Billing.Contracts` |
| Device Gateway | `RemoteStart`, `RemoteStop`, `SetChargingProfile` | Command | Charger command port (section 15.4) |
| Billing, any subscriber | `SessionCompleted` (`ohmline.charging.session-completed.v1`) on the charging feed | Event | `Ohmline.Charging.Contracts` (Published Language) |
| Driver app, operators | Session cards, session history, site live board | Query result | Read models |

## Ubiquitous language

| Term | Meaning here |
|---|---|
| Charging session (session) | One charge on one connector, from start to end. Here, and only here, "session" is the whole story: meter readings, suspensions, spend cap, tariff snapshot |
| Connector status | Available, Reserved, Occupied, Faulted or Unavailable |
| Reservation | A claim on a connector for one token until it expires. Never a payment hold |
| Meter reading | The charge point's cumulative register (Wh) with a timestamp |
| Meter anomaly | A reading that breaks the rules; recorded, never rejected |
| Spend cap | The most the driver pre-authorized; reaching it stops charging |
| Tariff snapshot | The tariff copied into a session at its start |
| Charging journey | The process from "start charging" to a settled payment |

The word "session" means something different next door. In Billing it is only the thing an
invoice refers to: a `SessionId`, an amount and a receipt line, with no meter readings at all. At
the roaming hub it is the hub's own record of a charge, with its own identifier and statuses,
which Ohmline does not control. Three models, three meanings, one word: that is why there are
three boundaries.

## Business decisions

- A connector hosts at most one session at a time.
- A reserved connector accepts only the token that holds the reservation, until it expires.
- Meter registers never go backwards; a reading that does is recorded as a meter anomaly.
- A session's price uses the tariff snapshot taken when it started, never a later tariff.
- Reaching the spend cap stops charging.
- Hold before power: no remote start before the payment hold is placed.
- If energy does not flow within 60 s of a remote start, the journey releases the allocation,
  voids the hold and unlocks the token.

## Assumptions

- Charge points report every state change, possibly late and possibly twice.
- A charge point can lose its connection mid-session and keep charging; its readings arrive in
  a burst when it reconnects.
- The tariff snapshot is at most a few seconds old when a session starts, and that is
  acceptable.

## Verification metrics

- Zero connectors ever recorded with two sessions at once.
- 99% of commands from the Device Gateway answered within 200 ms.
- Meter anomalies stay below 1 in 10,000 readings per charge point vendor.
- Every completed session reaches Billing as `SessionCompleted` within 30 s.

## Open questions

- Who suspended the charge, the car or Ohmline? The idle fee depends on it (hotspot H2,
  section 8.6).
- Who runs the price calculation when a session ends: Charging, with the snapshot it holds, or
  Tariffs? (Section 8.3.)
- How long does a reservation last before it expires, and may an operator extend it?
