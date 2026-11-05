# Testing strategy

Ohmline is a functional core inside an actor shell, and its tests follow the same split. Most of
them call pure functions with values and compare the values that come back: no actor system, no
clock, no database. The shell's tests are fewer and slower, and they prove only what the core
cannot: that messages arrive, that events are written and read back, and that the parts are
wired together.

This page says which kind of test goes where, what the tests are called, and what is not worth
testing. When a test and this page disagree, change one of them in the same commit.

## The layers

From the bottom of the pyramid to the top. The higher a layer, the fewer tests it has and the
more each one costs to run.

| Layer | What it proves | Tools | Where | From |
|---|---|---|---|---|
| Domain | The rules: what a decider decides, how it evolves, what a policy reacts with, what a translator makes of a row, what a price comes to. Most tests live here. | xUnit v3, Shouldly, CsCheck, `FakeTimeProvider`; from section 9.2, the shared helpers in `tests/Ohmline.Testing` | `tests/Ohmline.<Context>.Domain.Tests`, `tests/Ohmline.SharedKernel.Tests`, the ACL test projects | Chapter 6 |
| Actor | The shell's protocol: a command gets its reply, a rejection is not persisted, a timer fires, a supervisor restarts. | Akka.TestKit.Xunit (`TestProbe`, `ExpectMsgAsync`, `TestScheduler`) | `tests/Ohmline.<Context>.Actors.Tests` | Chapter 10 |
| Persistence | Recovery gives the state the events say; a failed write stops the actor before anyone is told yes; snapshots and events agree. | Akka.Persistence.TestKit.Xunit (journal and snapshot interceptors), with `akka.actor.serialize-messages = on` | `tests/Ohmline.<Context>.Actors.Tests` | Chapter 10 |
| Hosted | The context's actors are registered and reachable through the Generic Host; sharding works on one node. | Akka.Hosting.TestKit | `tests/Ohmline.Node.Tests` | Chapter 12 |
| Integration | HTTP, Akka.NET and a real PostgreSQL journal together: schema, serializers and SQL. | `WebApplicationFactory`, Testcontainers.PostgreSql on Podman or Docker | `tests/Ohmline.IntegrationTests`, `[Trait("Category", "Integration")]` | Chapter 12 |
| System | The whole distributed application starts and does one useful thing end to end. | Aspire.Hosting.Testing | `tests/Ohmline.SystemTests`, `[Trait("Category", "System")]` | Chapter 16 |

Three rules decide the layer:

1. **Test a rule where it lives.** A rule in `Decide` is tested by calling `Decide`. Testing it
   through an actor proves the same rule more slowly, and a failure no longer says whether the
   rule or the shell is wrong.
2. **Go one layer up only for what the lower layer cannot show.** An actor test that a rejected
   command writes nothing is worth having; an actor test for every row of a decision table is not.
3. **A bug found in a higher layer gets a test in the lowest layer that can show it**, and the
   higher test stays only if it proves something else as well.

Cluster tests across several processes (Akka.MultiNode.TestAdapter, network partitions) are an
advanced topic and are not part of the sample.

## Running the tests

The test projects use xUnit v3 on Microsoft.Testing.Platform (MTP). That has four consequences.

- **A test project is a program.** `tests/Directory.Build.props` sets `OutputType` to `Exe`, and
  xUnit generates the `Main` method. A test project must not contain top-level statements: they
  become the entry point instead (CS7022), and no test runs. `tests/Ohmline.Testing` is a library
  that tests share, so it sets `OutputType` back to `Library` and `IsTestProject` to `false`.
- **`global.json` selects the runner** (`"test": { "runner": "Microsoft.Testing.Platform" }`).
  Without it, `dotnet test` on the .NET 11 SDK refuses to run xUnit v3 4.x projects.
- **`dotnet test` needs a target:** `--solution Ohmline.slnx` or `--project <path>`. Options after
  `--` go to the test programs themselves.
- **The template that ships with the SDK is the old one.** `dotnet new xunit` still creates an
  xUnit v2 project for VSTest. Install `xunit.v3.templates` (`dotnet new install
  xunit.v3.templates`), or copy an existing test project, which needs no package references of
  its own beyond what `tests/Directory.Build.props` adds.

```bash
dotnet test --solution Ohmline.slnx                                     # everything
dotnet test --project tests/Ohmline.Charging.Domain.Tests               # one project
dotnet test --solution Ohmline.slnx -- --filter-class "*ChargePointDecideTests"
dotnet test --solution Ohmline.slnx -- --filter-not-trait "Category=Integration" \
                                       --filter-not-trait "Category=System"   # the fast loop
```

A test program in which no test runs ends with exit code 8 ("Zero tests ran"). For a whole
solution that happens only when the filter matches nothing at all; for a single project, pass
`--ignore-exit-code 8` if an empty run is expected.

A test that waits for anything passes `TestContext.Current.CancellationToken` wherever an API
accepts a token, so that a cancelled run stops at once; the xUnit analyzers point out calls that
do not. Nothing calls `Thread.Sleep`: domain tests take the time as a value, and actor tests use
`AwaitAssertAsync`, the `TestScheduler` or a `FakeTimeProvider`.

## Naming

- **One test project per production project with logic**, named after it with `.Tests`:
  `Ohmline.Charging.Domain` is tested by `Ohmline.Charging.Domain.Tests`. Folders and namespaces
  follow the production project's.
- **One test class per aggregate, service or policy family**, named after it:
  `ChargePointDecideTests`, `PriceCalculatorTests`, `PolicyTests`. Property tests end in
  `Properties`.
- **Test names are sentences in glossary words** (`docs/glossary.md`), in one of three forms:
  - `Given_<state>_when_<command>_then_<outcome>` for deciders and services:
    `Given_someone_elses_reservation_when_reserved_then_the_driver_learns_until_when`.
  - `Whenever_<event>_then_<intents>` for policies, as the lilac stickies read:
    `Whenever_the_spend_cap_is_reached_then_charging_stops`.
  - A plain sentence for properties: `Allocations_never_exceed_the_effective_capacity`.
- **Words a domain expert would not say stay out of names**: not `Decide_returns_Ok`, not
  `Test1`, not `Handles_null`. If a test cannot be named in the language, it is probably testing
  the implementation.
- **Fixtures are named for what they are in the domain**: `Known.DriversCard`, `TheDepot`,
  `TheDepotTariff`, `PrimersCharge`. Fixed instants (`Known.Morning`), never the wall clock.
- **Traits** mark only what needs infrastructure: `Category=Integration` (containers) and
  `Category=System` (Aspire). Domain tests carry no traits.
- **`Demonstrates_`** starts the name of a test on a `wrong/` branch that shows a deliberate
  mistake.

## What not to test

- **The framework.** That Akka.NET delivers a message, that `System.Text.Json` writes a string or
  that a record compares its members is already tested by their authors. Test what Ohmline asks
  of them (a serializer's manifest, a golden file), not that they work.
- **The implementation instead of the rule.** An expected value computed with the same formula
  as the production code proves nothing. Write the number the pricing analyst wrote down.
- **State where an event says it better.** A decider's test asserts the events it decides, not
  the state after them. State is what Evolve makes of the events, and Evolve has tests of its
  own.
- **Repositories that do not exist.** An aggregate here is loaded by folding its events. There
  is no `IChargePointRepository` to mock; a test gives the history as events.
- **Rules through actors.** Every rule of a decider, policy, specification or translator is
  tested in the core. Actor tests cover the protocol around it.
- **Private helpers.** They are tested through the public function that uses them. If a helper
  needs tests of its own, it is a concept and deserves a public name.
- **The clock.** No test reads the time. Instants are written down, or come from a
  `FakeTimeProvider` that the test advances.

`tests/README.md` lists where the test helpers are and the smells that break these rules.
