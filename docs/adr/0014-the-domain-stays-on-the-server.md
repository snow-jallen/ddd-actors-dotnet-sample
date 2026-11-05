# 0014. The domain stays on the server; the driver app is a context of its own

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch19-s01

## Context

Ohmline is getting a driver app, built with .NET MAUI. The same language and the same runtime
run on the server and on the phone, so it is tempting to share more than a contract: the
`ChargePoint` aggregate could check a reservation on the phone, and an actor system could run
the phone's work the way it runs the server's.

Five facts argue against it:

1. **The client is untrusted.** Any rule checked on a phone must be checked again on the
   server. A shared aggregate gives two enforcement points of which only one counts.
2. **Version skew.** Apps stay on old builds for months, through two app stores' reviews and
   drivers' update habits. An aggregate assembly on the phone would tie the domain's release
   cadence to the slowest store; a contract can grow additively, an aggregate cannot.
3. **The phone sees projections, not event streams.** It holds read models shaped for screens.
   An aggregate on the phone would be empty, or would need an event history that holds other
   drivers' data and is not the phone's to have.
4. **A different consistency model.** On the server, an aggregate decides at once and alone
   (ADR 0001). On the phone, everything is provisional until the server accepts it; a copy that
   pretends to decide produces "it said yes, and then it wasn't" bugs.
5. **Trimming and packaging.** Everything the app references ships in its package and must be
   trim- and AOT-safe. Domain assemblies pull in server dependencies over time; Akka.NET 1.5.73
   does not survive full trimming or NativeAOT (section 19.10).

## Decision

We will keep aggregates, invariants, event sourcing and actors on the server, where the single
writer decides.

We will treat the driver app as a bounded context of its own, the Driver App, downstream of
Charging in a Customer/Supplier relationship. Charging offers an Open Host Service (`/app/v1` and
the hub) and a Published Language, `Ohmline.Charging.Client.Contracts`, which references nothing:
not even `Ohmline.SharedKernel`, because the server's shared kernel is not the phone's.

We will keep a thin Anticorruption Layer in the app: its API client and its own types turn the
Published Language into the app's view state and words.

The device owns what is the device's: the driver's intent until the server has answered it (a
durable outbox of commands with ids made on the phone), a disposable cache of read models,
input validation for fast feedback, and the words of its own language (pending, syncing, failed
to sync, offline).

## Consequences

- The app's rules duplicate some of the server's on purpose (a connector code's shape, a spend
  cap's range). They exist for feedback; the server's are authoritative, and a disagreement is
  settled by the server's rejection.
- Commands must be safe to send twice, because the app retries (section 19.3,
  `docs/idempotency-contract.md`).
- The Published Language only grows: fields are added, never renamed or removed; the server
  tolerates missing fields and the app tolerates unknown rejection codes. A breaking change is
  `/app/v2` beside `/app/v1` (ADR 0009).
- Enforced by `tests/Ohmline.ArchitectureTests` from section 19.2 (the client contracts reference
  nothing) and section 19.4 (`Ohmline.Mobile.Core` references no server project but the client
  contracts, and no Akka.NET, MAUI or ASP.NET Core server assembly), and by the rule that no
  `Akka.*` package appears in a mobile project.
- A local-first device, one that must decide while disconnected and whose decisions must stand,
  would need a different decision: its own aggregate and event log. Ohmline's driver commands
  are not that case (section 19.10).
