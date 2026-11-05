# 0001. One writer per aggregate

- **Status:** Accepted
- **Date:** 2026-11-04
- **Book section:** ch01-s04

## Context

Ohmline's rules are about things that change concurrently. Two drivers can tap their cards at the
same connector within a second. Five sessions at `slc-depot` can ask for power while Northgate
Grid's curtailment request arrives. Meter readings from one charge point can arrive twice or out
of order. The sample runs on several nodes, so any of these messages can be handled anywhere.

The classic answers each have a cost:

- **A shared database with locks or optimistic concurrency** keeps one table consistent, but every
  rule that spans rows turns into retries, lock ordering and contention on the hottest rows.
- **Distributed transactions (two-phase commit)** across services couple their availability: a
  transaction cannot complete while any participant or the coordinator is down, and participants
  hold locks while they wait. The more services take part, the more often that happens.

Pat Helland's "Life Beyond Distributed Transactions" argues that systems which must keep growing
end up with the same shape anyway. Data is split into *entities*, each with a unique key, and each
entity is updated atomically on its own but never in a transaction with another entity. Work that
spans entities becomes an *activity*: each entity remembers what it knows about its partners and
reaches agreement with them through messages that may be retried. Martin Thompson's single-writer
principle makes the same point at the level of one machine: state that only one thread ever writes
needs no locks.

## Decision

We will give every aggregate exactly one writer at a time.

- An aggregate (a charge point, a charging session, a site's power budget, an id token) has a
  unique identity. At any moment exactly one live instance in the whole system may change its
  state, and every command for that identity is processed by that instance, one at a time.
- No transaction spans two aggregates: no two-phase commit, and no database transaction that
  writes two aggregates' state.
- A rule that spans aggregates is carried out by messages between their writers, tracked by an
  activity (a process manager in DDD terms), and is eventually consistent. Every such message
  may be delivered more than once, so its receiver must handle it idempotently.
- A uniqueness rule across many entities is enforced by making the unique value the identity of
  its own aggregate. For example, an id token is its own aggregate, keyed by its token id.

In the sample, the single writer is an Akka.NET persistent actor. Its mailbox serializes the
commands, cluster sharding keeps one instance per identity across the cluster, and the journal's
sequence numbers reject a second writer that slips through. The book builds these in Chapters 10
to 12.

## Consequences

- Rules inside one aggregate are checked without locks, retries or isolation levels, because no
  other code can change that state concurrently.
- The system grows by adding aggregates, not by making transactions larger.
- Drawing aggregate boundaries becomes the central design decision: a rule can only be checked
  immediately if all the data it needs is inside one aggregate.
- Rules that span aggregates are not checked immediately. For a short while a session can be
  charging while its site's power budget has not yet recorded the change, and the design must
  say what happens in that window.
- One writer is a throughput ceiling. A busy site's power budget handles every allocation request
  for that site in turn; this must be measured, and the aggregate split if it becomes too slow.
- Questions that span many aggregates ("which connectors are free near me?") cannot be answered
  by the writers. They need read models.
- Correctness depends on the runtime really keeping one instance per identity. A network
  partition, or two clusters writing the same journal, would create two writers; the cluster's
  split-brain resolver and the operations rules exist to prevent that.

## References

- Pat Helland, "Life Beyond Distributed Transactions: an Apostate's Opinion", CIDR 2007,
  https://www.cidrdb.org/cidr2007/papers/cidr07p15.pdf; revised in ACM Queue 14(5), 2016,
  https://queue.acm.org/detail.cfm?id=3025012
- Martin Thompson, "Single Writer Principle", 2011,
  https://mechanical-sympathy.blogspot.com/2011/09/single-writer-principle.html
