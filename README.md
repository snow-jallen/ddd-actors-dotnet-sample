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

## Licence and disclaimers

The code and documents are released under the [MIT licence](LICENSE).

Every company in the sample is fictional: Ohmline, TallyPay (payments), Northgate Grid (the grid
operator) and AssetTrack (a legacy asset registry). The charger protocol is a simplified subset in
the style of OCPP 1.6J; it makes no claim of conformance to any OCPP version.
