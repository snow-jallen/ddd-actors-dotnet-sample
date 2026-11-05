# Bounded Context Canvas: Driver App

*Bounded Context Canvas V5 (ddd-crew), filled in during section 19.1. The app is the .NET MAUI
driver app on a phone; its code is `src/Mobile/Ohmline.Mobile.Core` and the head in `mobile/`.*

## Name

**Driver App**

## Purpose

Let a driver find a charge point, start and stop charging and watch the session, from a phone
that is often offline: in an underground car park, on a motorway with no signal, or killed by
the operating system in the background. The app captures the driver's intent at once and makes
sure it reaches Ohmline; it never decides whether that intent is allowed.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Supporting (driver experience) | Engagement creator | Custom built |

## Domain roles

- **Draft and intent capture:** it turns what the driver typed or scanned into a command, and
  keeps that command until the server has answered it.
- **Presentation of read models:** it shows what Charging's read models say, cached so that the
  last known state survives a dead network.
- **Not an enforcer.** Every rule the app checks (a connector code's shape, a spend cap's
  range) is checked again by the server, which alone decides.

## Inbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Driver | Taps, typed or scanned connector codes, a chosen spend cap | Command (UI) | The app's own screens |
| Charging (HTTP) | Command accepted with a version; command rejected with a code | Event (reply) | `Ohmline.Charging.Client.Contracts` (Published Language) |
| Charging (HTTP) | Session cards, the token's card feed, a charge point's listing | Query result | `Ohmline.Charging.Client.Contracts` |
| Charging (SignalR hub) | `driverCardChanged`, `sessionSummaryChanged` | Event (push) | `Ohmline.Charging.Client.Contracts` |
| The phone | Connectivity changed; the app stopped or resumed | Event | Platform adapters in the MAUI head |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging (HTTP, `/app/v1`) | Start charging, stop charging, reserve a connector, each with a command id made on the phone | Command | `Ohmline.Charging.Client.Contracts` |
| Charging (HTTP, `/app/v1`) | Cards, feed after a position, a listing | Query | `Ohmline.Charging.Client.Contracts` |
| Charging (SignalR hub) | Join the token's group | Command | Hub method `JoinToken` |

## Ubiquitous language

The app has words the server never uses. They describe the phone's view of a command, not the
domain.

| Term | Meaning here |
|---|---|
| Pending command | A command the driver gave that the server has not yet accepted or rejected: queued in the outbox, or being sent |
| Syncing | The app is sending pending commands or catching up its cache |
| Failed to sync | A pending command that could not be sent within its maximum age (24 hours); the driver is told, and it is never sent again |
| Offline | The phone has no usable connection. A hint, never proof: only a send attempt shows whether the server can be reached |
| Read-model cache | The last session cards the server sent, kept on the phone. Disposable: it can be deleted and filled again from the server |
| Rejected | The server said no, with a code; shown as a sentence ("This connector is reserved for another driver"), never retried |

The words it shares with Charging (connector, spend cap, charging session, reservation) mean
what the glossary says they mean; the app shows them, it does not redefine them.

## Business decisions

The app makes none of Charging's decisions. Its own rules are about the phone:

- A command is written to the outbox before any network call; a double tap is one command.
- A rejected command is terminal; a new attempt is a new command with a new id.
- A transient failure stops sending, so commands reach the server in the order they were given.
- A pending command older than 24 hours is marked failed to sync.
- A pending overlay stays on a card until the card's version reaches the version the server
  accepted.

## Assumptions

- The server remembers command ids for longer than the app retries them (`docs/idempotency-contract.md`).
- Old builds of the app stay in drivers' pockets for months; the Published Language only grows.
- The operating system may kill the app whenever it is in the background, without warning.

## Verification metrics

- No command is lost between a tap and the server, however often the app is killed.
- No command is applied twice, however often it is sent.
- 95% of commands given while online are accepted within 2 s of the tap.

## Open questions

- Should a pending command be cancellable before it is sent?
- Push notifications (FCM, APNs) for a session that ends while the app is closed: out of scope
  for now.
