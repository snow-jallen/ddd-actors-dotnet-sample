# 0008. Domain errors are replies; infrastructure failures stop the actor

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch11-s06

## Context

Two very different things go wrong in an aggregate's actor, and Akka.NET treats them
differently whether we like it or not.

A **domain error** is the decider saying no: the connector is reserved for another token, the
session has already ended. Nothing is wrong with the system. The decider returns the error as a
value, nothing is persisted, and the sender deserves an answer it can act on.

An **infrastructure failure** is the journal saying no. Akka.Persistence 1.5.73 distinguishes
two kinds:

| What the journal did | Typical cause | What Akka.NET does |
|---|---|---|
| **Failed** the write (`OnPersistFailure`) | The database is down or timed out, or another incarnation wrote the same sequence number (section 10.9) | Stops the actor. The write may or may not be stored, so the actor no longer knows its own state |
| **Rejected** the write (`OnPersistRejected`) | The journal refused the events before writing them, for example because one could not be serialized | Keeps the actor running. Nothing was stored and no event handler ran, but the rejected events still use up their sequence numbers, and `PersistAll`'s completion callback still runs |
| **Failed** the replay (`OnRecoveryFailure`) | The database is down, or an event cannot be read (a poison event, `docs/runbooks/poison-event.md`) | Stops the actor; the messages that waited for the replay become dead letters |

When the actor stops, the commands that were waiting in its internal stash behind the failed
write are lost: they become dead letters, and their senders hear nothing.

## Decision

We will keep domain errors and infrastructure failures apart, in `DeciderActor`, once, for every
aggregate:

- **Domain errors are replies.** The decider's error is mapped to the context's rejection reply
  (`CommandRejected`, `BudgetCommandRejected`) with a stable code. Nothing is logged, nothing
  is persisted, and the actor carries on.
- **A rejected write is a reply too.** `OnPersistRejected` logs at ERROR (a rejected event is a
  bug in our code or our serializer) and answers the waiting sender with the code
  `persistence-rejected` (`ShellRejectionCodes.PersistenceRejected`), once. The completion
  callback then finds nobody to tell that the command was accepted.
- **A failed write or replay stops the actor, and we let it.** `OnPersistFailure` and
  `OnRecoveryFailure` log at ERROR, without trying to reply or recover in place. Senders time out
  and retry with the same command id, which the shell will recognise (section 12.6).
- **Something outside the actor starts it again.** Until Chapter 12, each charge point runs under
  a `BackoffSupervisor` (`Backoff.OnStop`, 1 s to 30 s, jitter 0.2) inside `ChargePointDirectory`;
  from Chapter 12, the shard region starts an entity again on its next message.
- **We never stop a persistent actor with `PoisonPill` or `Context.Stop`** from the outside. Both
  can drop commands that are stashed or in flight; passivation through the shard's hand-off
  message is the only way an entity goes away (section 12.4).

## Consequences

- Callers see three outcomes: accepted with a version, rejected with a code (from the decider or
  `persistence-rejected`), or no answer at all. Every caller already needs a timeout and a retry
  for the third, because a node can also die; this decision only makes that case explicit.
- Versions are not always contiguous: a rejected write uses up sequence numbers, so the next
  accepted command can skip one. Callers must compare versions, never count them.
- Retries need idempotency, so section 12.6 has to deliver the command-id check before any
  endpoint retries automatically.
- A poison event makes the supervisor or the shard start the actor again and again. The backoff
  keeps that from hammering the database; the runbook says how to stop it for good.
- Enforced by `PersistenceFailureTests` (a failed write stops the actor and dead-letters what was
  stashed; a rejected write answers `persistence-rejected` and keeps the actor; the supervisor
  starts a stopped charge point again), `RecoveryFailureTests` and `PoisonEventTests`.
