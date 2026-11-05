# 0002. Functional core, actor shell

- **Status:** Accepted
- **Date:** 2026-11-04
- **Book section:** ch01-s06

## Context

ADR 0001 makes every aggregate a single writer, and the sample implements that writer as an
Akka.NET persistent actor. An actor gives us one-at-a-time processing, location transparency and
supervision. It gives us no model. A `Receive` handler can read the clock, call another service,
mutate a field and persist an event in any order, and actor code that grows feature by feature
tends to end up doing all four, with the business rules spread across handlers.

Rules written that way are hard to test: every test needs an actor system, a journal, messages and
timeouts. They are also hard to replay. When state is rebuilt from events, the code that applies
an event must not fail and must not repeat side effects, and that is difficult to guarantee when
applying an event and deciding what to do are mixed in one handler.

Gary Bernhardt's "functional core, imperative shell" separates the two: decisions are pure
functions over values, and a thin shell performs the effects. Jérémie Chassaing's *Decider* gives
that core a fixed shape for an event-sourced aggregate:

```text
Decide : State × Command → Events | Error
Evolve : State × Event   → State
```

## Decision

We will write every aggregate as a pure decider and host it in an actor shell.

The **functional core** holds the model:

- an initial state, a `Decide` function and an `Evolve` function per aggregate, together with its
  states, commands, events and errors as immutable C# types;
- value objects, domain services and policies as pure functions over values.

The core lives in `Ohmline.SharedKernel` and the `*.Domain` projects. Those projects reference the
.NET base library only: no Akka.NET, no database, no HTTP, no logging. `Decide` and `Evolve` read
no clock, generate no random values and perform no I/O; the current time and new ids arrive as
data. `Evolve` is total: it never throws for an event that `Decide` can produce, and it never
re-validates, because an event records something that has already happened.

The **actor shell** does everything else, around each command:

1. receive the command from the mailbox, one at a time;
2. call `Decide` with the current state, the command and the current time;
3. persist the events it returns, or reply with the error;
4. apply each persisted event with `Evolve`, then reply and send any messages that follow.

On recovery the shell folds the stored events with the same `Evolve`. It also owns identity,
placement in the cluster, timers, snapshots, supervision and integration with other actors.

| Lives in the core | Lives in the shell |
|---|---|
| Invariants and business rules | The mailbox, which serializes commands |
| States, commands, events, errors | Persisting events and recovering state |
| Prices, allocations, deadlines as values | Reading the clock, running timers |
| What should happen next, as data | Sending messages, calling other contexts |

## Consequences

- Most tests become plain unit tests over values: given these events, when this command, then
  these events or this error. They need no actor system and no waiting, and they run in
  milliseconds. Actor tests shrink to protocol, persistence and recovery.
- Recovery and tests use the same fold over `Evolve`, so a rule that is tested is the rule that
  runs during replay.
- The model can change without touching Akka.NET, and Akka.NET can be upgraded without touching
  the model.
- There is more ceremony than writing the actor directly: separate types for commands and events,
  two functions instead of one handler, and a shell that translates between messages and the core.
- State is immutable, so every change allocates a new value. At the size of one aggregate this is
  negligible, but it is not free.
- Behaviour that is about time is split. The core decides that a reservation expires at 08:10; the
  shell schedules the timer that delivers the command at 08:10. Readers must look in two places.
- Effects the core asks for (place a hold, send a charging profile) are returned as data and
  carried out by the shell, which is one more level of indirection to follow when debugging.
- The rule needs enforcement, or it erodes. From section 5.3 the build fails if a `*.Domain`
  project references a forbidden package, and architecture tests check the project graph.

## References

- Gary Bernhardt, "Functional Core, Imperative Shell", Destroy All Software, 2012,
  https://www.destroyallsoftware.com/screencasts/catalog/functional-core-imperative-shell, and
  "Boundaries" (talk, 2012), https://www.destroyallsoftware.com/talks/boundaries
- Jérémie Chassaing, "Functional Event Sourcing Decider", 2021,
  https://thinkbeforecoding.com/post/2021/12/17/functional-event-sourcing-decider
