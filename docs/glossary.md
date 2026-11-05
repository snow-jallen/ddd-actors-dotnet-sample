# Ohmline glossary

This is the ubiquitous language of Ohmline. Use these words exactly as written here: in
conversations, in documents, in type and member names, and in test names. When a conversation
produces a better word, change the glossary first and the code in the same commit.

The language is not one list. Each bounded context has its own, and the same word can mean
different things in two contexts. The sections below follow the contexts that the Big Picture
EventStorming suggested (`docs/eventstorming/big-picture.md`); Chapter 5 draws their boundaries.

The *In code* column names the type or member that carries the term. A name in that column is
reserved from the moment it appears here, even before the section that adds the type.
`docs/naming-rules.md` says how new names are formed.

## Used in every context

These words cross context boundaries unchanged. Their types live in `Ohmline.SharedKernel`.

| Term | Meaning | In code |
|---|---|---|
| Ohmline | The fictional charge point operator whose software this is | `Ohmline.*` |
| Site | A location with one grid connection and therefore one power budget | `SiteId` (kebab case, `slc-depot`) |
| Charge point (charger) | A physical charging device with one or more connectors | `ChargePointId` (`OHM-SLC-0042`) |
| Connector | A numbered plug on a charge point; hosts at most one session at a time | `ConnectorId` (`OHM-SLC-0042/1`) |
| Id token (token) | An RFID card or app credential that authorizes charging | `TokenUid` |
| Driver | The person charging; never stored by name in events | `DriverRef` (opaque) |
| Power | An instantaneous rate, in kW. Never confuse it with energy | `Power` |
| Energy | An amount delivered over time, in kWh | `Energy` |
| Current | Electrical current, in A | `Current` |
| Money | An amount in one currency; amounts in two currencies are never added | `Money`, `Currency` |

## Charging

The context that runs charge points, connectors, reservations and charging sessions.

| Term | Meaning | In code |
|---|---|---|
| Charging session (session) | One charge on one connector, from start to end. OCPP calls it a "transaction"; inside Charging it is always a session | `SessionId`, `ChargingSession` |
| Connector status | Available, Reserved, Occupied, Faulted or Unavailable | `ConnectorState` |
| Reservation | A claim on a connector for one token until it expires. Not a payment hold | `ReserveConnector`, `ConnectorReserved`, `ReservationExpired` |
| Occupied | A car is plugged into the connector | `OccupyConnector`, `ConnectorOccupied` |
| Fault | The charge point reports that a connector cannot be used | `ReportConnectorFault`, `ConnectorFaulted` |
| Meter reading | The charge point's cumulative energy register (Wh) with a timestamp; it never decreases | `MeterReading` |
| Meter anomaly | A meter reading that breaks the rules, such as a register that went backwards. Recorded, never rejected | `MeterAnomalyDetected` |
| Energy delivered | Energy between two meter readings, or over a whole session | `Energy` |
| Suspended | Charging paused during a session while the car stays plugged in | `SuspendCharging`, `ChargingSuspended` |
| Spend cap | The most the driver pre-authorized; reaching it stops charging | `SpendCapReached` |
| Tariff snapshot | The version of the site's tariff copied into a session when it starts; later tariff changes never affect that session | `TariffSnapshot` |
| Charging journey | The process from "start charging" to a settled payment, across several contexts | `ChargingJourney`, `JourneyId` |

## Site Energy

The context that shares each site's grid connection between its sessions.

| Term | Meaning | In code |
|---|---|---|
| Grid connection limit | The maximum power the site may draw from the grid | `Power` |
| Safety margin | Headroom Ohmline keeps below the grid connection limit | `Power` |
| Curtailment | A temporary reduction of the site's capacity, requested by the grid operator and applied by Ohmline | `ApplyCurtailment`, `CurtailmentApplied` |
| Effective capacity | Grid connection limit minus active curtailment minus safety margin | computed in `SitePowerBudget` |
| Power allocation | Power granted to one connector's session; always between the connector's minimum and maximum | `PowerAllocation` |
| Priority class | Fleet or Public; fleet requests are served first | `PriorityClass` |
| Fair-share allocation | Sharing the effective capacity among all requests, up to each one's maximum | `FairShareAllocator` |
| Rebalance (allocation) | Recomputing every allocation at a site after something changed. Not *shard* rebalancing | `AllocationsRebalanced` |

## Tariffs

The context that owns prices: versioned rules that the pricing analyst edits as data.

| Term | Meaning | In code |
|---|---|---|
| Tariff | Versioned pricing rules for a site: energy rate, time rate, session fee, idle fee, time-of-use bands, price cap | `Tariff`, `TariffComponent` |
| Time-of-use (TOU) band | A rate that applies in a window of site-local time | `TimeOfUseBand` |
| Idle fee | A per-minute fee once charging has stopped and the car is still plugged in, after a grace period | `IdleFee` |
| Grace period | The minutes after charging stops during which no idle fee applies | `IdleFee` |
| Price breakdown | The line items and total for one session | `PriceBreakdown` |
| Rounding policy | How amounts are rounded, per currency and tariff | `RoundingPolicy` |

## Driver Accounts

The context that knows which tokens exist and whether each may charge.

| Term | Meaning | In code |
|---|---|---|
| Token lock | An id token held by one running session; a locked token cannot start another | `LockForSession`, `TokenLockedForSession` |
| Blocked token | A token that may not start any session, for example a card reported lost | `BlockToken`, `TokenBlocked` |

## Billing

The context that moves money, through TallyPay.

| Term | Meaning | In code |
|---|---|---|
| Payment | A money movement at TallyPay: a hold, a capture or a void. Never called a "transaction" | Billing tables |
| Hold (pre-authorization) | Money reserved on the driver's card before charging. Not a connector reservation | `PlaceHold`, `HoldPlaced` |
| Capture | Taking the final amount from a hold; the rest of the hold is released | `CapturePayment`, `PaymentCaptured` |
| Void | Releasing a hold without taking any money | `VoidHold` |
| Debt | An amount the driver owes after a capture failed for good, or after the price exceeded the hold | `DebtRecorded` |
| Receipt | The driver-facing record of a settled session | Billing tables |

## Device Gateway

The context that talks to charge points in their own protocol, a simplified subset in the style
of OCPP 1.6J. These words are the protocol's, and they stop at the gateway.

| Term | Meaning | Inside Ohmline it becomes |
|---|---|---|
| Transaction (OCPP) | The charge point's word for a charging session | Charging session |
| StartTransaction / StopTransaction | The charge point reports that a session started or stopped | `StartSession`, `StopSession` |
| Authorize | The charge point asks whether an id token may charge | `LockForSession` |
| Charging profile | A power limit sent to a charge point; the OCPP action `SetChargingProfile` keeps its external name | `SetChargingProfile` |
| Remote start / remote stop | Ohmline asks a charge point to start or stop | `RemoteStart`, `RemoteStop` |
| Heartbeat / boot notification | A liveness ping / the first message after a charge point connects | — |

## External systems

| Term | Meaning | In the sample |
|---|---|---|
| TallyPay | The fictional payment provider (pre-authorize, capture, void) | `tools/Ohmline.FakePsp` |
| Northgate Grid | The fictional grid operator; publishes demand-response signals | `tools/Ohmline.FakeGridOperator` |
| Demand-response signal | Northgate Grid's request to reduce a site's draw for a period. Its word, not ours: Ohmline turns it into a curtailment | Translated by the grid operator ACL |
| AssetTrack | The fictional legacy asset registry; a messy CSV export | `tools/legacy-assets/` |

## One word, several meanings

These words caused arguments on the EventStorming wall (hotspot H1). Always qualify them.

| Word | Meanings | Rule |
|---|---|---|
| session | A charging session (Charging); an HTTP session | A bare "session" is always a charging session |
| transaction | OCPP's name for a charging session; a payment at TallyPay; a database transaction | Say "OCPP transaction", "payment" or "database transaction". Never use a bare "transaction" for a session |
| hold / reservation | A payment hold (Billing); a connector reservation (Charging) | "Hold" is money, "reservation" is a connector |
| actor | A role in a domain story; an Akka.NET actor | Say "story actor" in domain stories |
| rebalance | Allocation rebalancing (Site Energy); shard rebalancing (Akka.NET) | Always qualify |
| event | A domain event; an integration event; an EventStorming sticky; a .NET event; Northgate Grid's demand-response event | Qualify whenever two meanings are in play. Northgate Grid's is a "signal" |
| version | An aggregate's sequence number; a manifest version (`.v2`); a tariff version; a release | Always qualify |
| capacity | The grid connection limit; the effective capacity | A bare "capacity" is the effective capacity |
