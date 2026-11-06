# 0017. Never two clusters on the same journal at the same time

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch21-s08

## Context

Most releases roll (ADR 0016, section 21.7). Some cannot: a change of Akka.NET's remoting
transport, a change of the actor system's name, an upgrade whose wire format an older node
cannot read (section 21.10). Those are deployed blue/green: a second cluster, green, beside the
running one, blue, and traffic moved from one to the other.

For a stateless web service, blue/green means running both for a while and switching the load
balancer. For Ohmline it cannot mean that. Both clusters would use the same journal, and each
would start shard regions for the same aggregates. A charge point would then have one live
actor in blue and another in green, each recovered to the same sequence number, each deciding
on its own copy of the state. That is ADR 0001's single writer broken across clusters, and its
shape in the journal is what `TwoIncarnationsTests` shows inside one node (section 10.9): both
write sequence number N + 1, the unique index on `(persistence_id, sequence_number)` refuses
the second write, and that incarnation stops. Which writes win depends on timing, so a
reservation one cluster accepted can be lost to the other's, and the driver was already told
yes. With a journal that has no such index, or two writes far enough apart, the history
interleaves instead, and later recoveries fold a sequence no single writer decided.

The unique index and Akka.Persistence.Sql's writer UUID are the last line of defence. They turn
a silent corruption into a failed write; they do not make two writers safe.

## Decision

We will never run two clusters that host the same aggregates at the same time.

1. **Blue/green is a cut-over, not an overlap.** Green's manifests are applied with its node
   and gateway deployments at zero replicas. Blue's shards are drained and blue is stopped;
   only when no blue node is left is green scaled up and given the traffic. The downtime is
   the drain, green's start and its first recoveries, and it is announced.
2. **What may overlap is what does not write aggregates:** read-model projections under a new
   version (section 20.9), the Device Gateway's connections (they reconnect), Billing (its own
   database, idempotent by hold id).
3. **Green has its own actor system identity:** a different remoting port range and discovery
   label (`akka-cluster=ohmline-green`), so the two clusters can never join each other by
   accident.
4. **The runbook `docs/runbooks/blue-green.md` is the only way to do it.** It names the steps,
   the checks between them and the rollback.

## Consequences

- A release that cannot roll costs a short, planned outage of the write side. ADR 0016 makes
  such releases rare and visible: they are MAJOR versions of the services.
- Feature flags, not parallel clusters, are how a new behaviour is tried on part of the
  traffic (section 21.8). A flag is evaluated in the shell, passed to Decide, and its outcome is
  recorded in the event, so replay never depends on the flag's value on another day.
- Read models can still be rebuilt side by side and switched without downtime.
- `TwoIncarnationsTests` (section 10.9) documents what a second writer looks like in the
  journal; the runbook tells an operator to look for that error if they suspect an overlap.
