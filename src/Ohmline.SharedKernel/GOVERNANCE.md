# Shared kernel governance

`Ohmline.SharedKernel` is the one piece of model that every bounded context shares. That makes it
the cheapest place to put a type and the most expensive place to change one: a change here
changes every context at once, and some of its types are written into stored events. This page
says what may live here, what never may, and how a change gets in. It follows the Shared Kernel
pattern on the context map (`docs/context-map.md`, section 5.5).

## What belongs here

Only concepts that mean exactly the same thing in every context that uses them:

| Kind | Types | Why shared |
|---|---|---|
| Units | `Energy` (kWh), `Power` (kW), `Current` (A), `Voltage` (V) | A kilowatt is a kilowatt in Charging, Site Energy and Tariffs. Sharing the type stops kW and kWh from being mixed at a boundary |
| Money | `Money`, `Currency`, `RoundingPolicy` | Tariffs prices, Charging caps spending and Billing captures, all in the same amounts |
| Cross-context identifiers | `SiteId`, `ChargePointId`, `ConnectorId`, `SessionId`, `JourneyId`, `TokenUid` | They are how one context refers to another context's things. Each context gives the thing behind the id its own model |

Next to these, the kernel holds the small building blocks that every pure core is written with:
`Result<T, E>`, `Unit`, `ValidationError`, `IValueObject<TSelf, TValue>`, `GuidV7` and, from
Chapter 7 on, the decider interface and its decision context. They carry no business meaning
of their own, and the same rules apply to them.

## What never belongs here

- **Actors, or anything that needs Akka.NET.** The kernel references the base class library and
  nothing else (`SharedKernelPurityTests`). Actor infrastructure lives in `Ohmline.ActorShell`.
- **Any context's aggregates, commands, events, replies or errors.** A charge point, a session's
  state or a tariff belongs to its context, even when two contexts both mention it. Messages
  that cross a boundary live in the sender's or receiver's `Contracts` project.
- **Serialization code.** No `System.Text.Json` attributes or converters; the converters for the
  kernel's value objects live in `Ohmline.ActorShell` (section 10.3).
- **Clocks, randomness and I/O.** A `TimeProvider` is passed in; it is never read here.
- **A type that only one context uses**, however general it looks. Put it in that context and
  move it here when a second context needs exactly the same thing.

## Serializer stability

Value objects from the kernel appear inside persisted events, inside messages between nodes and,
through the Published Language, inside other services' data. Their shape on the wire is a
durable contract (`docs/naming-rules.md`):

- Never change a type's unit or meaning. `Energy` is kWh forever; a value in Wh is converted
  before it becomes an `Energy`.
- Never rename or remove a member that a converter writes. Add a new member instead, and keep
  reading the old shape.
- Never tighten validation in a way that rejects values already stored. A `ChargePointId` that
  was valid when its event was written must still be readable when the event is replayed.
- Every converter for a kernel type has a golden-file test (section 10.3 on).

## Versioning

The kernel has a version of its own, recorded below, independent of the services that use it.
It follows semantic versioning:

- **Patch:** a fix that changes no public member and no wire shape.
- **Minor:** a new type or member. Existing code and stored data are unaffected.
- **Major:** a public member changes or disappears. A major change must still read every wire
  shape ever written; if it cannot, it is not allowed.

Today every context builds against the kernel in the same repository. If a context ever moves
to a repository or release cycle of its own (Billing is the likeliest), the kernel is packaged
and this version becomes the package version.

| Version | Change | Section |
|---|---|---|
| 1.0.0 | `Result`, `Unit`, `ValidationError`, `IValueObject`, `GuidV7` | 3.5–3.8 |
| 1.1.0 | Units: `Energy`, `Power`, `Current`, `Voltage` and the `Dimensions` operators | 6.1 |
| 2.0.0 | `ValidationError` becomes a union of `OutOfRange`, `Malformed` and `Missing` (major: its constructor is gone; it was never written to the wire); the units implement `IValueObject` | 6.2 |
| 2.1.0 | `Currency`, `Money`, `CurrencyMismatch` and `RoundingPolicy` in `Monetary` | 6.3 |
| 2.2.0 | Identifiers: `SiteId`, `ChargePointId`, `ConnectorId`, `SessionId`, `JourneyId`, `TokenUid`; `GuidV7.Check` | 6.4 |
| 2.3.0 | `DecisionContext` in `Deciders`: the time a decider is given | 7.4 |
| 2.4.0 | `IDecider<TState, TCommand, TEvent, TError>` and `Decider.Fold` | 7.6 |
| 2.5.0 | `CommandId` in `Identity`: the id a sender gives a command, recorded by its events | 8.1 |
| 2.6.0 | `ISpecification<T>`, `And`/`Or`/`Not` and their extension members in `Specifications` | 8.4 |
| 2.7.0 | `SiteLocalTime` in `Time`: an instant on a site's wall clock, by IANA time zone id | 8.7 |
| 2.8.0 | `GuidV7.Derive` and `CommandId.Derive`: an id that follows from another, the same every time | 15.1 |

## How a change gets in

1. Open a pull request that changes only the kernel and the tests that pin its behaviour.
2. Every team that uses the kernel reviews it. `.github/CODEOWNERS` makes the review group
   `kernel-reviewers@ohmline.example`, one reviewer from each team, the owner of this folder.
   GitHub enforces only one approval, so the author waits for one from each team.
3. The build, the architecture tests and the golden files pass.
4. The version table above gains a row in the same pull request.

A change that one team needs urgently and another team disagrees with does not go into the
kernel. The first team copies the type into its own context, and the kernel stays as it is.
