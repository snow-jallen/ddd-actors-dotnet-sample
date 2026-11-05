# Ohmline.Charging.Client.Contracts

Charging's Published Language for client apps (section 19.2): what the driver app sends to
`/app/v1`, what it reads back, and what the hub at `/hubs/charging` pushes to it. The server's
endpoints and the app both reference this project and both serialize with
`ChargingClientJsonContext`, so the two ends cannot disagree about a shape.

## What is in it

| Folder | Types | Used for |
|---|---|---|
| `Requests/` | `StartChargingRequest`, `StopChargingRequest`, `ReserveConnectorRequest` | Commands; each carries the `CommandId` the app made at the tap |
| `Replies/` | `CommandAcceptedDto`, `ProblemDto` | A 202's body; any problem's body |
| `ReadModels/` | `DriverSessionCard`, `DriverSessionCards`, `DriverFeedEntry`, `DriverFeedPage`, `ChargePointListingDto` | Reads |
| `Notifications/` | `SessionSummaryChanged`, `DriverCardChanged` | Hub pushes (`sessionSummaryChanged`, `driverCardChanged`) and Server-Sent Events |
| | `RejectionCodes` | The `code` of a 409 or 422 problem |

Every field is a primitive: a connector is its printed form (`OHM-SLC-0042/1`), an amount is a
`decimal` beside an ISO 4217 currency code, an id is a `Guid`. Nothing here references
`Ohmline.SharedKernel` or any package (`ClientContractsTests` in `tests/Ohmline.ArchitectureTests`
checks it): the server's Shared Kernel is not the phone's, and whatever is referenced here ships
in every app package. `IsAotCompatible` is on, so the build reports anything that would not
survive trimming or NativeAOT.

## The HTTP mapping

| Answer | Status | Body | What the app does |
|---|---|---|---|
| Accepted | 202 | `CommandAcceptedDto(CommandId, Version)` | Done; keep the pending overlay until a read shows `Version` |
| Rejected by the aggregate | 409 | problem with `code` | Terminal: show the sentence for the code, never send again |
| Contradicts itself (an `Idempotency-Key` header that is not the body's `commandId`) | 400 | problem with `code` (`idempotency-key-mismatch`) | Terminal: a bug in the app |
| Not valid, or names something unknown | 422 | problem with `code` (`invalid-request`, `unknown-connector`) | Terminal |
| No answer in time | 504 | problem | Transient: send again later with the same `CommandId` |
| Unavailable | 503 | problem | Transient |

Sending a command again is safe, and expected, after a transient answer:
`docs/idempotency-contract.md` says how long the server remembers a command (section 19.3).

## Versioning rules

Apps stay on old builds for months, so this language only grows:

1. **Add fields; never rename or remove one.** A new field is optional for the server: an older
   app does not send it.
2. **The server tolerates missing fields.** A request from an older app is valid without the
   fields it does not know.
3. **The app tolerates unknown fields and unknown codes.** It ignores a field it does not know,
   and shows a general sentence for a `code` it does not know.
4. **`RejectionCodes` values are append-only.** A code is never changed or reused.
5. **A breaking change is a new route prefix**, `/app/v2` beside `/app/v1`, never a change to
   `v1` (ADR 0009).

Every DTO has a golden file in `tests/golden/client/` (`ClientContractsTests` in
`tests/Ohmline.ContractTests`). A failing golden test is a contract change: accept it with
`OHMLINE_ACCEPT_GOLDEN=1` only when it is additive.
