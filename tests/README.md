# Tests

The strategy, the layers and the naming rules are in
[`docs/testing-strategy.md`](../docs/testing-strategy.md). This page is the short version for
someone about to write a test: where the helpers are, and the smells to avoid.

## Where things are

| You want to test | Use | Example |
|---|---|---|
| A decider's rules | `DeciderSpec` in `Ohmline.Testing`, closed over each decider in the project's `Specs.cs` | `ChargePointSpec.Given(history).At(Morning).When(command).Then(events)` in `Ohmline.Charging.Domain.Tests/ChargePoints/ChargePointDecideTests.cs` |
| Rules an expert explains with examples | A `TheoryData` table in the glossary's words, or a data file the expert keeps | `ReservationScenarioTests`, `PriceCalculatorExampleTests` with `pricing-examples.csv` |
| An invariant that must survive any sequence of commands | `DeciderProperties` in `Ohmline.Testing`, with CsCheck generators | `SitePowerBudgetProperties`, `ChargingSessionProperties` |
| Anything about time | A `FakeTimeProvider` the test moves on, read once per command with `DecisionContext.From(clock)` | `ReservationExpiryTests`, `IdleFeeGraceTests`, `TouBoundaryTests` |
| A translator or a policy | A table of inputs and outputs, one row per case | `AssetTrackTranslationTableTests`, `PolicyTableTests` |
| What an actor replies, persists and recovers | `PersistenceShellSpec` in `Ohmline.Testing` (on Akka.Persistence.TestKit.Xunit) with `ActorTestConfig.With(...)`: given events in the journal, when a message, then a reply and the events persisted; every message serialized | `ChargePointActorTests` in `Ohmline.Charging.Actors.Tests` |
| What the shell does when the journal or snapshot store misbehaves | `PersistenceShellSpec`'s `WithJournalWrite`/`WithSnapshotSave`, or the test kit's `WithJournalRecovery` | `PersistenceFailureTests`, `SnapshotFailureTests`, `RecoveryFailureTests` |
| The shape of what is persisted or sent | `Golden.Verify` in `Ohmline.Testing`, one file per manifest in `tests/golden/`; accept a new file with `OHMLINE_ACCEPT_GOLDEN=1` | `SerializationTests` in `Ohmline.Charging.Actors.Tests` |

Fixtures are named for the domain: `Known` (Charging), `TheDepot` (Site Energy) and
`TheDepotTariff` (Tariffs) hold the people, places, tariffs and fixed instants the tests talk
about.

## Smells

Each smell is a test that passes today and costs later: it breaks when nothing is wrong, or it
stays green when something is.

### Asserting state instead of events

*Symptom:* a decider test calls `Decide`, folds the events and checks a field of the state, such
as the connector's state after a reservation.

*Why it hurts:* the events are the decision; the state is only Evolve's summary of them. A test
on the state passes when Decide records the wrong event and Evolve happens to land in the same
place, and it breaks when the state's shape is refactored although no rule changed.

*Instead:* `Then(new ConnectorReserved(...))`. Check state only in tests of Evolve itself.

### Mocking a repository that does not exist

*Symptom:* an `IChargePointRepository` mock set up to return an aggregate, so that a "service"
can be tested.

*Why it hurts:* there is no repository. An aggregate is the fold of its events, loaded by its
actor. The mock tests a design Ohmline does not have and hides the one it does.

*Instead:* give the history as events: `ChargePointSpec.Given(registered, reserved)`.

### Testing through actors what belongs in the core

*Symptom:* an actor test that sends a command, waits for the reply and checks a business rule,
once for every row of a decision table.

*Why it hurts:* each test needs an actor system, a mailbox and a timeout to check a pure
function. It is slower, can fail for reasons that have nothing to do with the rule, and when it
fails, it does not say whether the rule or the shell is wrong.

*Instead:* test the rule with `DeciderSpec` and keep actor tests for what only the shell does:
replies, persistence, recovery and timers.

### Smaller smells

- **The expected value computed with the production formula.** Write the number the expert
  wrote down (`16.59`), not `27.6m * 0.42m + 1.00m + ...`.
- **The wall clock.** `DateTime.UtcNow` in a test makes it pass at one time of day and fail at
  another. Use a fixed instant or a `FakeTimeProvider`.
- **Names in the code's language.** `Decide_returns_Err_for_case_3` says nothing to an expert.
  Name the test in glossary words, as the rule would be said aloud.
- **A property that cannot fail.** An invariant helper that is always true passes every
  property test. Test the assertion itself with a state that breaks it, as
  `Given_allocations_that_break_the_rules_then_the_assertion_is_false` does for `AllocationsFit`.
