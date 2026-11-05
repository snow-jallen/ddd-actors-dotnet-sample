# The idempotency contract between the driver app and Charging

*Section 19.3. Read with ADR 0014 and `src/Charging/Ohmline.Charging.Client.Contracts/README.md`.*

## Why commands are sent twice

A phone cannot tell "not done" from "done, but the answer was lost". A start that times out
at the edge (504) may still be carried out by its journey a moment later; a 202 may be lost
when the train enters a tunnel. The only safe thing for the app to do is to send the same
command again, and the only safe thing for the server to do is to recognise it.

## The rules

**The app:**

1. Makes every command's id at the tap, a version 7 Guid (`Guid.CreateVersion7()`), and the
   journey's id with it, before anything is sent. Both are in the request's body.
2. Writes the command to its outbox before the first send, and sends it again, with the same
   ids, after any transient failure: a network error, 503 or 504.
3. Never sends a command again after a 202, a 409 or a 422. Those answers are final.
4. Stops sending a command 24 hours after it was made and shows it as failed to sync.
5. May also send an `Idempotency-Key` header; if it does, the header must equal the body's
   `commandId`, or the request is refused with 400 (`idempotency-key-mismatch`).

**The server:**

1. Copies the body's `commandId` into Charging's command unchanged.
2. Answers a command it has already accepted with the version it answered the first time, and
   records nothing.
3. Remembers accepted commands for longer than the app sends them.

## How Charging remembers

| Aggregate | App command | How a repeat is recognised | For how long |
|---|---|---|---|
| Charge point | Reserve a connector | The recent-command window (section 12.6): the last 256 command ids that stored events, rebuilt from the events and snapshots | Until 256 newer commands have been accepted by the same charge point |
| Charging session | Stop charging | The same window, per session | Until 256 newer commands; a session accepts none after it has ended |
| Charging journey | Start charging | The journey id is the app's, and the journey keeps the command that started it in its terms (`JourneyTerms.StartedBy`) | For the journey's whole life |

The window is rebuilt from the journal because every event records the id of the command that
caused it: the check and the write are one decision of one actor, the single writer, so a
retry that arrives while the first send is still being persisted waits in the mailbox and finds
the id once the write has finished. No lock and no table of processed commands is needed.

**Why 256 is longer than 24 hours.** A charge point accepts a few commands per session (a
reservation, the connector occupied, the connector released), so a busy one with two
connectors accepts about a hundred a day. A session accepts a meter value a minute, but none
after the stop that ends it, so the app's stop is always among the session's last commands. If
an aggregate ever accepted 256 commands within a day, a stop or a reservation sent again after
that would be decided again: a reservation would most likely be refused as reserved for another
token, its own, and a stop as already ended. Watch for that before raising the window.

**A start is different.** The journey id names the journey, so the journey can tell a repeat
for ever: the same command again is answered with its first acceptance; another command with
the same journey id is refused with `journey-exists`. Two commands with one journey id are a
bug in the app, never two drivers.

## Commands that need more than deduplication

The app's three commands need only deduplication. Reserving a connector, starting and stopping
are decided against the aggregate's state as it is when the command arrives, and the driver's
intent does not depend on what the phone showed: "reserve connector 1" means the same thing
whatever the phone last saw.

A command whose meaning depends on what the driver saw must carry the version the driver saw,
and the aggregate must refuse it with a stale rejection when its version has moved on. Changing
the spend cap of a session that is charging would be one: a cap chosen against a reading of
7 kWh may be wrong at 40 kWh. Such a command would add an `expectedVersion` field to its request
and a `stale` value to `RejectionCodes`, both additive; the app keeps the driver's input,
refreshes the card and asks again. Ohmline has no such command yet.
