# Ohmline

This is the companion sample repo for the book *Domain-Driven Design with the Actor Model on .NET:
functional core, actor shell with C# 15, .NET 11 and Akka.NET*.

Ohmline is a fictional operator of public EV charge points. The sample builds its software one
book section at a time: a pure functional domain core for charge points, charging sessions, site
power budgets and tariffs, wrapped in an Akka.NET actor shell that persists, shards and integrates
it. Start with [the domain primer](docs/ohmline-primer.md) if the domain is new to you.

## How the history maps to the book

- `main` has a linear history. Every book section that changes the sample adds one commit, usually
  exactly one.
- Each of those commits carries an annotated tag named after its section: `chNN-sMM` is the state
  of the sample at the end of section NN.MM. For example, `ch07-s04` is the end of section 7.4.
- Sections that only explain ideas change nothing, so they have no tag. Gaps in the numbering are
  expected: there is no `ch01-s01` because section 1.1 has no code.
- Commit subjects repeat the section title (`ch01-s02: Meet Ohmline: one charge, told end to end`),
  and every commit has a `Book-Section: chNN-sMM` trailer.

## Following along with tags

```bash
git tag --list 'ch*'                    # every section tag, in book order
git checkout ch01-s02                   # the sample as it is at the end of section 1.2
git diff ch01-s02 ch01-s03 --stat       # which files section 1.3 changed
git diff ch01-s02 ch01-s03 -- docs      # the same diff, limited to one folder
git log --format='%h %s' ch01-s02..ch01-s04
git checkout main                       # back to the latest state
```

A checked-out tag leaves you on a detached `HEAD`. To experiment from a tag, create a branch of
your own first: `git switch -c my-experiments ch07-s04`.

The usual pattern is `git diff <previous tag> <tag>`, where the previous tag is the last tag
before the one you are reading. Because tags exist only for sections with code, the previous tag
is not always the previous section.

## Building and testing

You need the .NET SDK named in `global.json` (11.0.100-rc.3 at the time of writing, or a later
11.0 SDK). Later chapters add more requirements:

- Podman or Docker for the integration tests, from Chapter 12. With rootless Podman, export
  `DOCKER_HOST=unix:///run/user/$(id -u)/podman/podman.sock` first.
- The Aspire CLI 13.6 to run the local cluster, from Chapter 12.
- The `maui-android` workload, an Android SDK and a JDK, only to build the driver app's .NET MAUI
  head in `mobile/` (Chapter 19).

From the repo root:

```bash
dotnet build Ohmline.slnx -warnaserror
dotnet test --solution Ohmline.slnx
tools/verify-tags.sh                    # build and test every tag in a temporary worktree
```

`global.json` pins the SDK and selects the Microsoft.Testing.Platform runner, which is why
`dotnet test` takes `--solution`. Until section 2.1 adds the first project, `Ohmline.slnx` is
empty and the two `dotnet` commands fail with "Unable to find a project to restore!";
`tools/verify-tags.sh` reports those tags as skipped.

The Chapter 1 tags (`ch01-s02` to `ch01-s07`) hold documents and setup only, so they are exempt
from the build-and-test check. From `ch02-s01` on, every tag must build with `-warnaserror`, pass
all of its tests and leave no files behind, and `tools/verify-tags.sh` fails any tag that does not.

## Side branches

Later chapters add branches next to `main`. None of them is ever merged.

| Branch | Meaning |
|---|---|
| `wrong/<tag>-<slug>` | A deliberate mistake made at `<tag>`. It builds; a test whose name starts with `Demonstrates_` shows the failure. |
| `alt/<tag>-<slug>` | A design alternative to the choice the book makes at `<tag>`. |
| `upgrade/akka-1.6` | The platform upgrade from Akka.NET 1.5 to 1.6. |

## Tag index

| Tag | Section | What it adds |
|---|---|---|
| `ch01-s02` | 1.2 Meet Ohmline: one charge, told end to end | This README, the licence, git settings, `docs/ohmline-primer.md` |
| `ch01-s03` | 1.3 Models, language and the people who know the business | `docs/glossary.md`, the ADR template, ADR 0000 |
| `ch01-s04` | 1.4 Consistency without distributed transactions | ADR 0001: one writer per aggregate |
| `ch01-s06` | 1.6 Functional core, actor shell | ADR 0002, a first-draft `docs/vision.md` |
| `ch01-s07` | 1.7 Setting up the workbench | `global.json`, `Directory.*.props`, `NuGet.config`, `.editorconfig`, an empty `Ohmline.slnx`, `tools/verify-tags.sh` |
| `ch02-s01` | 2.1 Shared mutable state: double-booking a connector | `learn/`, `RacyConnector`, `LockedConnector`, the first xUnit v3 tests |
| `ch02-s02` | 2.2 A mailbox in sixty lines | `Mailbox<TMessage>` on a single-reader `Channel`, `ConnectorActor` |
| `ch02-s03` | 2.3 Tell, don't ask: reply addresses and futures | `ReplyTo<TReply>`, `ICanTell<TMessage>`, `AskAsync` with a mandatory timeout |
| `ch02-s04` | 2.4 Let it crash: supervision and addresses by hand | `Supervisor`, `ActorDirectory<TActor>` |
| `ch02-s05` | 2.5 Hello, Akka.NET | Akka.NET 1.5.73, `Ch02.AkkaBasics.Tests`, `AkkaConnectorActor`, the TestKit |
| `ch02-s06` | 2.6 Behaviour as state: Become and Stash | `ChargerTwin` with `Become` and `IWithStash` |
| `ch02-s07` | 2.7 Time inside actors | The `ChargerTwin` heartbeat watchdog on `IWithTimers` |
| `ch02-s08` | 2.8 Supervision, backoff and the async question | `FirmwareLookupActor` (`PipeTo`), `ReceiveAsync` contrast, `BackoffSupervisor` |
| `ch02-s09` | 2.9 Actors in the Generic Host | `Ch02.Hosted`: Akka.Hosting, `IRequiredActor<T>`, coordinated shutdown |
| `ch03-s01` | 3.1 Learning tests: pinning down a language | `Ch03.LanguageLab.Tests`, record equality and with-expressions |
| `ch03-s02` | 3.2 Records, with, and validation with the field keyword | `Kilowatts` with a `field`-validated `init` |
| `ch03-s03` | 3.3 Union types: closed sets of existing types | `ConnectorStatus` union; CS8509 and CS8655 as build errors |
| `ch03-s04` | 3.4 Closed hierarchies and the union-versus-closed decision | `Ch03.ClosedLib`, `ChargerCommand` closed hierarchy, ADR 0003 |
| `ch03-s05` | 3.5 Errors as values: a Result union | `Ohmline.SharedKernel` with `Result<T, E>` and `Unit`; `tests/` |
| `ch03-s06` | 3.6 Static abstract members and generic math | `IValueObject<TSelf, TValue>`, `ValidationError`, operator interfaces |
| `ch03-s07` | 3.7 Extension members and extension indexers | Extension blocks on `decimal`, `Kilowatts` and a meter history |
| `ch03-s08` | 3.8 Patterns that read like rules; time and identity | List and property patterns, labeled `break`, `FakeTimeProvider`, `GuidV7` |
| `ch03-s09` | 3.9 Serialization reality check: unions and closed records on the wire and in the mailbox | System.Text.Json and Akka.NET tests, `docs/house-style.md` |
| `ch04-s01` | 4.1 Domain Storytelling with Ohmline's people | Three domain stories in `docs/domain-stories/` |
| `ch04-s02` | 4.2 Big Picture EventStorming | `docs/eventstorming/big-picture.md`: timeline, pivotal events, hotspots |
| `ch04-s03` | 4.3 Process-Level EventStorming | Process boards for starting charging and settling a session |
| `ch04-s04` | 4.4 Design-Level EventStorming and the sticky-to-code map | Aggregates and the sticky-to-code map in `docs/eventstorming/design-level.md` |
| `ch04-s05` | 4.5 Ubiquitous language: glossary, naming rules and durable vocabulary | The glossary per context, `docs/naming-rules.md` |
| `ch04-s06` | 4.6 From stickies to types: the vocabulary compiles | `Ohmline.Charging.Contracts` and `.Domain` with empty `ChargePointCommand` and `ChargePointEvent` hierarchies |
| `ch04-s07` | 4.7 Subdomains: core, supporting, generic | `docs/subdomains.md` |
| `ch04-s08` | 4.8 Choosing a business-logic pattern per subdomain | ADR 0004: an implementation style per subdomain |
| `ch05-s01` | 5.1 Bounded contexts and the Bounded Context Canvas | A Bounded Context Canvas per context in `docs/canvases/` |
| `ch05-s02` | 5.2 Modules made physical: architecture styles and the solution layout | Empty projects for every context, Billing, the Device Gateway and `Ohmline.ActorShell` |
| `ch05-s03` | 5.3 Keeping the core pure, enforced | `build/DomainPurity.targets`, `tests/Ohmline.ArchitectureTests` |
| `ch05-s04` | 5.4 Context mapping I: teams and flows | `docs/context-map.md`: teams, the map, every relationship |
| `ch05-s05` | 5.5 Context mapping II: Shared Kernel, Open Host Service and Published Language | Shared kernel governance, `.github/CODEOWNERS`, the Published Language rules |
| `ch05-s06` | 5.6 Context mapping III: Anticorruption Layer, Separate Ways and Big Ball of Mud | The AssetTrack export and its translation rules in `docs/acl/` |
| `ch05-s07` | 5.7 The executable context map and deployment topology | An artifact per relationship, ADR 0005, `ContextBoundaryTests` |
| `ch05-s08` | 5.8 Distillation | The final `docs/vision.md`, `docs/highlighted-core.md` |
| `ch05-s09` | 5.9 Large-scale structure | ADR 0006: responsibility layers and supervision |
| `ch06-s01` | 6.1 Primitive obsession in charging: kW versus kWh versus A | `Energy`, `Power`, `Current`, `Voltage` and the `Dimensions` operators |
| `ch06-s02` | 6.2 Smart constructors that return unions | `union ValidationError(OutOfRange, Malformed, Missing)`; `TryCreate` on every unit |
| `ch06-s03` | 6.3 Money, currency and rounding | `Currency`, `Money`, `CurrencyMismatch`, `RoundingPolicy` |
| `ch06-s04` | 6.4 Identity: strongly typed identifiers | `SiteId`, `ChargePointId`, `ConnectorId`, `SessionId`, `JourneyId`, `TokenUid` |
| `ch06-s05` | 6.5 Meter readings: monotonic registers and time | `MeterReading`, `MeterError`, `MeterHistory`; `Ohmline.Charging.Domain.Tests` |
| `ch06-s06` | 6.6 Entities inside an aggregate: a connector is a record, not an actor | `Connector` and `ConnectorComparer.ById` |
| `ch06-s07` | 6.7 Value objects at the boundary: parsing a Big Ball of Mud | `SiteConfiguration`, the AssetTrack anticorruption layer and its tests |
| `ch06-s08` | 6.8 Property-based tests for value objects | CsCheck 5.0.1, generators and property tests for units and money |
| `ch07-s01` | 7.1 Finding the true invariants: the Aggregate Design Canvas | An Aggregate Design Canvas for each of the four aggregates in `docs/canvases/` |
| `ch07-s02` | 7.2 Commands and events as closed hierarchies | The charge point's commands and events with their data; `PersistedShapeTests` |
| `ch07-s03` | 7.3 State as a union: making illegal states unrepresentable | `union ConnectorState(...)`, `union ChargePointState(Unregistered, Registered)` |
| `ch07-s04` | 7.4 Decide: business rules as an exhaustive decision table | `DecisionContext`, `ChargePoint.Decide`, `union ChargePointError(...)` |
| `ch07-s05` | 7.5 Evolve: total, deterministic, never failing | `ChargePoint.Evolve` and `ChargePoint.Fold` |
| `ch07-s06` | 7.6 The Decider abstraction | `IDecider<TState, TCommand, TEvent, TError>`, `Decider.Fold` |
| `ch07-s07` | 7.7 The ChargingSession aggregate | Session commands, events, `union SessionState(...)` and the `ChargingSession` decider |
| `ch07-s08` | 7.8 The SitePowerBudget aggregate and the fair-share allocator | `FairShareAllocator`, `union AllocationDecision(...)` and the `SitePowerBudget` decider |
| `ch08-s01` | 8.1 Domain events as facts experts care about | `CommandId` on every command; `CommandId` and `OccurredAt` stamped on every event |
| `ch08-s02` | 8.2 Tariffs as a Knowledge Level | `Tariff`, `TariffComponent`, `TimeOfUseBand`; `TariffSnapshot` locked into `SessionStarted` |
| `ch08-s03` | 8.3 Domain services: the price calculator | `PriceCalculator`, `SessionUsage`, `PriceBreakdown` |
| `ch08-s04` | 8.4 Specifications: as classes and as patterns | `ISpecification<T>` with `And`/`Or`/`Not`; `IdleFeeApplies`, `RequiresPreAuthorization` |
| `ch08-s05` | 8.5 Policies: 'whenever X then Y' as pure reactions | `ISessionPolicy`, `ReleaseOnSessionEnded`, `SpendCapPolicy`, `IdleFeePolicy`, `ChargingIntent` |
| `ch08-s06` | 8.6 Refactoring toward deeper insight: who suspended the charge? | `SuspendedByEv` and `SuspendedByCharger` in place of `Suspended` |
| `ch08-s07` | 8.7 Time in the domain: site-local time, time-of-use and DST | `SiteLocalTime`; pricing across Denver's daylight-saving changes |
| `ch09-s01` | 9.1 The test pyramid for a functional core and an actor shell | `docs/testing-strategy.md`: the layers, naming and what not to test |
| `ch09-s02` | 9.2 A Given-When-Then harness for deciders | `tests/Ohmline.Testing` with `DeciderSpec`; the decider tests rewritten on it |
| `ch09-s03` | 9.3 Specification by example: scenario tables in the ubiquitous language | Reservation and session-stop tables, `pricing-examples.csv`; a reservation fix |
| `ch09-s04` | 9.4 Model-based property tests for invariants | `DeciderProperties`, `AllocationsFit`, property tests for the three deciders |
| `ch09-s05` | 9.5 Testing time | `ReservationExpiryTests`, `IdleFeeGraceTests`, `TouBoundaryTests` on a fake clock |
| `ch09-s06` | 9.6 Testing translators and policies; DDD test smells | Table tests for the AssetTrack translator and the policies, `tests/README.md` |
| `ch10-s01` | 10.1 What the shell owns; a composition root with Akka.Hosting | `src/Ohmline.Node` (`/alive`, `TimeProvider.System`), `AddChargingContext()` |
| `ch10-s02` | 10.2 Protocol: commands in, replies out, and the union trap | `CommandAccepted`/`CommandRejected`, `GetChargePointStatus`, `ErrorMapping`, `SendExtensions.Send` |
| `ch10-s03` | 10.3 Serialization before the first persisted event | `ManifestJsonSerializer`, `ChargingSerializer` (9101), `Golden`, `tests/golden/`, ADR 0007 |
| `ch10-s04` | 10.4 ReceivePersistentActor: Decide, PersistAll, Evolve, reply | `ChargePointActor`, `ChargePointDirectory`, `ActorTestConfig`, the first shell tests |
| `ch10-s05` | 10.5 While the write is in flight: the internal stash | `InFlightWriteTests` with a held journal write |
| `ch10-s06` | 10.6 Persist, PersistAsync, PersistAll and DeferAsync | `learn/Ch10.PersistenceLab.Tests` |
| `ch10-s07` | 10.7 Real storage: Akka.Persistence.Sql on SQLite | `WithSqlPersistence` on SQLite in the node, `NodeRestartTests` |
| `ch10-s08` | 10.8 A guided tour of the journal | `JournalTourTests`, `JournalTourQueries`, `docs/journal-tour.md` |
| `ch10-s09` | 10.9 One writer per persistence id: sequence numbers instead of locks | `TwoIncarnationsTests`; "Two writers" in `docs/journal-tour.md` |
| `ch10-s10` | 10.10 A reusable DeciderActor and the other aggregates | `DeciderActor`, `ChargingSessionActor`, `SitePowerBudgetActor`, `SiteEnergySerializer` (9102) |
| `ch11-s01` | 11.1 Recovery in depth | The recovery INFO line in `DeciderActor`, `RecoveryTests`, `JournalProbe` |
| `ch11-s02` | 11.2 Snapshots: a cache with a cost model | Snapshot DTOs and manifests for the three aggregates, `SnapshotEvery`, the snapshot row in `docs/journal-tour.md` |
| `ch11-s03` | 11.3 Recovery at scale: throttling, storms and the poison event | `max-concurrent-recoveries` in the node, `OnRecoveryFailure`, `PoisonEventTests`, `docs/runbooks/poison-event.md` |
| `ch11-s04` | 11.4 Timers derived from state: reservation expiry | Reservation timers in `ChargePointActor`, `ReservationExpiryTests` on virtual time |
| `ch11-s06` | 11.6 Failure semantics: errors versus failures | `OnPersistFailure`/`OnPersistRejected`, `persistence-rejected`, a backoff supervisor, ADR 0008 |
| `ch11-s07` | 11.7 Durable State: the non-event-sourced alternative | `ChargePointListing`, `StateSourcedDeciderActor`, `docs/persistence-choices.md` |
| `ch11-s08` | 11.8 Testing the shell with Akka.Persistence.TestKit | `PersistenceShellSpec`; the actor tests rewritten on it; `SnapshotFailureTests` |
| `ch12-s01` | 12.1 From one node to a cluster | Remoting and clustering in the node (`WithOhmlineCluster`), the `charging` role, keep-majority set explicitly |
| `ch12-s02` | 12.2 The shard region is the repository | `EntityMessageExtractor` (100 shards), `EntityShardOptions`, a region per aggregate, ADR 0009 |
| `ch12-s03` | 12.3 Factories: domain versus actor | Entities made by the container (`resolver.Props<T>`), the clock injected |
| `ch12-s04` | 12.4 Passivation and remember-entities | The `Stop` hand-off message, idle passivation, an ended session passivates itself |
| `ch12-s05` | 12.5 Application services at the HTTP edge | `Ohmline.Charging.Api` and `Ohmline.SiteEnergy.Api` endpoints, validated options, development scaffolding |
| `ch12-s06` | 12.6 Idempotency keys and read-your-writes | `RecentCommands` in `DeciderActor` and every snapshot, the `Idempotency-Key` header |
| `ch12-s07` | 12.7 Uniqueness across aggregates: the IdToken aggregate | Driver Accounts: `IdToken`, `DriverAccountsSerializer` (9103), the `id-token` region, `/tokens` |
| `ch12-s08` | 12.8 The Knowledge Level in the cluster: the tariff book singleton | `TariffBook`, `TariffsSerializer` (9104), the `tariff-book` singleton and its proxy, `/tariffs` |
| `ch12-s09` | 12.9 Testing sharding locally with Akka.Hosting.TestKit | `Ohmline.Node.Tests`: the node's wiring on a cluster of one |
| `ch12-s10` | 12.10 Postgres and a multi-node cluster on your laptop with Aspire | `Ohmline.AppHost` (PostgreSQL 17, three nodes), `Ohmline.ServiceDefaults`, ADR 0010 |
| `ch12-s11` | 12.11 Integration tests against real Postgres | `Ohmline.IntegrationTests`: `WebApplicationFactory`, Testcontainers, state that survives a restart |
| `ch13-s02` | 13.2 Tagging events | `ChargingTagger`, `SiteEnergyTagger`: one tag per event; the tags rows in the journal tour |
| `ch13-s03` | 13.3 Persistence Query: the read side of the journal | `ReadJournalAccess`; `Ohmline.Charging.ReadModels.Tests` with `ReadJournalTourTests` |
| `ch13-s04` | 13.4 A resumable projection runner: offsets and at-least-once | `ProjectionRunner`, `NpgsqlOffsetStore`, `ReadModelSchema`; branch `wrong/ch13-s04-offset-before-handler` |
| `ch13-s05` | 13.5 Idempotent read models: upserts and the offset in one transaction | `SessionHistoryProjection`, the pure `SessionHistoryRow.Apply`, the offset in the row's transaction |
| `ch13-s06` | 13.6 Running exactly one projector | The sharded daemon process `projections` (`ProjectionDaemon`, `AddChargingReadModels()`) |
| `ch13-s07` | 13.7 When a projection fails: restarts, poison events and lag | `ProjectionFailurePolicy`, `read_models.projection_failures`, `GET /projections/status`, a runbook |
| `ch13-s08` | 13.8 Read models people use: a site live board and a query API | `SiteLiveBoardProjection`, `ISiteAllocationEvent`, `GET /sites/{id}/live-board`, `GET /tokens/{uid}/sessions` |
| `ch14-s01` | 14.1 Read-your-writes with the version a command returns | `GET /sessions/{id}?minVersion=`, `ReadYourWrites`, `Ohmline:ReadModels:ReadYourWritesTimeout`; session commands' `Location` asks for their version |
| `ch14-s02` | 14.2 Consistency choices per screen | `docs/consistency-per-screen.md` |
| `ch14-s03` | 14.3 Tuning and measuring projection lag | `Ohmline:Projections:RefreshInterval` and `QueryDelay` (100 ms), `last_event_at` and lag in `GET /projections/status` |
| `ch14-s04` | 14.4 Live push with Server-Sent Events | `LiveUpdate`/`SessionSummaryChanged` published after the commit on DistributedPubSub, `GET /sessions/{id}/events` |
| `ch14-s05` | 14.5 Live push to app clients with SignalR | `ChargingHub` at `/hubs/charging`, `LiveUpdatesBridge` |
| `ch14-s06` | 14.6 Many read models from one stream, including one for the driver app | `DriverAppProjection` (`driver-app-v1`), the driver app cards and feed, `GET /app/v1/tokens/{uid}/cards` and `/feed`, `driverCardChanged` |
| `ch14-s07` | 14.7 Testing projections | `ProjectionRulesTests` and `InMemoryRunnerTests` on Akka.Persistence.Query.InMemory, `ReplayTests` |
| `ch15-s01` | 15.1 Saga or process manager? Choreography first | `SessionEndedSubscriber` (`session-ended-subscriber-v1`): a session's end releases its allocation and unlocks its token; `CommandId.Derive` |
| `ch15-s02` | 15.2 The charging journey as a pure state machine | `ChargingJourney.Next` over the `JourneyInput` union, `JourneyState`, `JourneyEffect`, the journey's events; Site Energy's `PowerAllocated` and `QueuedForPower` |
| `ch15-s03` | 15.3 The journey actor: persistent, sharded, with timers | `ChargingJourneyActor` in the `charging-journey` region (remember-entities on), journey manifests and tag, `POST /journeys`, `GET /journeys/{id}` |
| `ch15-s04` | 15.4 Ports to other contexts | `IPaymentPort`, `IChargerCommandPort`, `InMemoryPaymentPort` and `LoggingChargerCommandPort` in Development, `JourneySessionRelay` (`journey-session-relay-v1`), fake contexts with autopilots |
| `ch15-s05` | 15.5 Timeouts and compensation | The energy-flow timeout gives everything back; capture retries with backoff, then `DebtRecorded` |
| `ch15-s06` | 15.6 Delivery guarantees and idempotency keys | `IdempotencyKey` on every request, reply timers, idempotent ports and receivers, ADR 0011 |
| `ch15-s07` | 15.7 Testing time-driven processes | `JourneyTransitionTableTests`, `ChargingJourneyActorTests` on the `TestScheduler` with autopilots |

## Licence and disclaimers

The code and documents are released under the [MIT licence](LICENSE).

Every company in the sample is fictional: Ohmline, TallyPay (payments), Northgate Grid (the grid
operator) and AssetTrack (a legacy asset registry). The charger protocol is a simplified subset in
the style of OCPP 1.6J; it makes no claim of conformance to any OCPP version.
