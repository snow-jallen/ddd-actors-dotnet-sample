# 0012. We trace through the mailbox by hand, and know when to buy Phobos instead

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch17-s09

## Context

Akka.NET 1.5.73 has no `ActivitySource` and no `Meter` of its own for messaging. A `Tell` or an
`Ask` made inside a span reaches the actor with no current activity, because
`Activity.Current` flows with the execution context and a mailbox does not carry one
(`MailboxLosesTraceTests`, section 17.3). Without help, every trace in Ohmline stops at the
first shard region.

Chapter 17 built the help by hand, in `Ohmline.ActorShell/Tracing`, at a cost of a few hundred
lines and these decisions:

- the sender's span travels as a W3C `traceparent` in a `TraceEnvelope`, written by
  `TraceEnvelopeSerializer` (9100, `ohmline.trace-envelope.v1`), which keeps the inner message's
  serializer id, manifest and bytes (section 17.4);
- `TracedPersistentActor` and `TracedReceiveActor` open the envelope and process the message in a
  CONSUMER span; stashed commands, persist callbacks and timers each needed a fix of their own,
  found by tests (sections 17.5 to 17.7);
- a traced command's events are stored in trace envelopes, tagged by `TracedEventTagger`, so
  projections can link to the span that decided them (section 17.7).

What it does **not** cover, by design or for now:

- **Replies.** An actor answers with a plain `Tell`: an envelope would break `Ask<T>`. A step the
  journey takes because a reply arrived (`TokenLocked`, `PowerAllocated`) runs in no span.
- **`PipeTo` results**, such as a port's answer, come back plain for the same reason.
- **Akka.Streams stages, DistributedPubSub publishes** (other than the charger commands, which
  the port wraps), cluster and remoting internals.
- **`ReceiveAsync`/`CommandAsync` handlers.** The span would end at the first `await`; Ohmline's
  aggregates are synchronous and use `PipeTo` for I/O, so none is affected today.
- **A user-level stash.** `ChargerConnectionActor` stashes frames itself while it forwards a
  call; such a frame is processed in two spans, one when it is stashed and one when it is
  handled.

Petabridge's **Phobos** is the commercial alternative. What follows is from its release notes and
product page, read on 2026-11-04; we have not compiled or run it, because it needs a licence:

- Phobos 2.12.1 (2026-08-27), built on Akka.NET 1.5.71, on OpenTelemetry since 2.0. The packages
  (`Phobos.Actor`, `Phobos.Hosting`) are not on nuget.org; they come from a private feed after a
  purchase through sdkbin.com. The 1.0 release notes say there is no per-node licensing; we have
  not confirmed current pricing or feed mechanics.
- It starts a span for every actor receive and propagates context through `Tell`, `Ask`,
  `PipeTo`, Akka.Streams (from 2.12.0), Akka.Remote and Cluster.Sharding (sharding continuity
  fixes in 2.11.x), with "trace compression" to filter noise, and it fixed spans of
  `ReceiveAsync` handlers closing early in 2.12.0.
- It adds metrics (messages processed by actor and message type, mailbox depth, processing
  latency, error and log rates, cluster membership) and prebuilt Grafana dashboards.
- The documented wiring is `akka.WithPhobos(AkkaRunMode.Cluster, ...)` on the Akka.Hosting
  builder and `AddPhobosInstrumentation()` on the OpenTelemetry tracer and meter builders.
- We found no statement about Akka.NET 1.6 support.

## Decision

We will keep the manual tracing in `Ohmline.ActorShell/Tracing` for now. It is vendor-neutral,
it uses only the BCL and OpenTelemetry's conventions, every line of it is covered by
`Ohmline.Telemetry.Tests`, and it teaches what any tool has to do at a mailbox.

We will re-evaluate Phobos when any of these becomes true, and judge it by the criteria below:

1. An incident's trace stopped at a gap listed above (a reply, a `PipeTo`, a stream) and cost
   real time to diagnose.
2. Ohmline adopts Akka.Streams or async handlers in aggregates.
3. The tracing code in the shell needs changes in more than one release in a row.

Evaluation criteria, each answered in writing before a purchase:

- **Coverage:** does it close the gaps listed above in Ohmline's own scenarios (OCPP frame to
  receipt, the journey's replies and timers), shown by `TraceShapeTests` run against it?
- **Version lag:** which Akka.NET versions does the current release support, how long after an
  Akka.NET release did the last three Phobos releases follow, and what is the plan for 1.6?
- **Distribution:** can CI restore from the private feed with a secret, and can a reader or a
  contractor build the sample without a licence (it must stay optional: behind a build flag, or
  in the hosts only)?
- **Cost:** the licence, and the time we stop spending on the shell's tracing.
- **Overhead and control:** latency and allocations per message with tracing on and off,
  sampling and filtering by actor type, and cardinality of its metric tags.
- **Data:** what it puts on spans (message contents or only types), and whether that passes our
  privacy rules (no tokens or driver ids in attributes).
- **The journal:** our events already contain trace envelopes. Whatever we adopt, the envelope
  serializer (9100) stays registered for as long as those rows exist, and the projections keep
  opening envelopes; the two must not both wrap the same message.
- **Exit:** if we stop paying, does the code still build and run with tracing simply off?

## Consequences

- Tracing is our code to maintain: a new kind of message flow (a stream, a new port) needs its
  own propagation, and a test in `Ohmline.Telemetry.Tests` that proves it.
- The gaps above are known and written down, rather than discovered during an incident.
- The trace envelope is durable: its serializer id and manifest are in ADR 0009, and stored
  events depend on them (`TraceEnvelopeSerializationTests` pins the golden file).
- Adopting Phobos later means turning off our envelopes for new messages while still reading
  old ones, and checking that its spans and ours are not both recorded for the same receive.
