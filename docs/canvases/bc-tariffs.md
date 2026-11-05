# Bounded Context Canvas: Tariffs

*Bounded Context Canvas V5 (ddd-crew), filled in during section 5.1.*

## Name

**Tariffs**

## Purpose

Own Ohmline's prices: versioned pricing rules per site that the pricing analyst edits as data,
and the exact calculation that turns a session's usage into a price breakdown.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Supporting (tariffs and pricing) | Revenue generator | Custom built |

## Domain roles

- **Specification model:** Tariffs says what a charge costs; Charging and Billing execute it.
- **Knowledge Level:** the rules are data with versions, not code (section 8.2).

## Inbound communication

Every message in these tables is a public contract: it lives in `Ohmline.Tariffs.Contracts`,
crosses a serializer and changes only by adding a version.

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Pricing analyst (operators' tools) | `PublishTariff`, `RetireTariff` | Command | `Ohmline.Tariffs.Contracts` |
| Charging (at the start of a session) | `GetTariffSnapshot` | Query | `Ohmline.Tariffs.Contracts` |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging | The tariff snapshot for a site | Event (reply) | `Ohmline.Tariffs.Contracts` |
| Billing (through Charging's `SessionCompleted`) | The price breakdown of a session | Data inside an event | `Ohmline.Charging.Contracts` |

## Ubiquitous language

| Term | Meaning here |
|---|---|
| Tariff | Versioned pricing rules for a site: energy rate, time rate, session fee, idle fee, time-of-use bands, price cap |
| Tariff version | One published revision of a tariff; never edited after publication |
| Tariff snapshot | A tariff version as copied into a session |
| Time-of-use (TOU) band | A rate that applies in a window of site-local time |
| Idle fee, grace period | A per-minute fee once charging has stopped and the car is still plugged in, after the grace period |
| Price breakdown | The line items and total of one session |
| Rounding policy | How amounts are rounded, per currency and tariff |

## Business decisions

- A published tariff version never changes; a new price is a new version.
- A session is priced with the snapshot it started with, so a tariff change never affects a
  running session.
- Time-of-use bands use the site's local time, including across daylight-saving changes.
- The price cap applies after all components are added; rounding applies once, at the end, with
  the tariff's rounding policy.

## Assumptions

- Tariffs change monthly at most, so one tariff book for the whole of Ohmline is enough (a
  cluster singleton, section 12.8).
- The pricing analyst checks a new tariff against example sessions before publishing it.

## Verification metrics

- Every price breakdown adds up to its total to the cent.
- Zero sessions priced with a tariff version newer than their snapshot.
- A new tariff reaches every site within one minute of publication.

## Open questions

- Who runs the price calculation when a session ends: Charging, with the snapshot it holds, or
  Tariffs? (Section 8.3.)
- Should a tariff be able to vary by priority class (fleet versus public)? Hotspot H5 says
  fleet customers expect it.
