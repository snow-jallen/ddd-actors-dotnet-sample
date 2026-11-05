# 0011. Journeys deliver at most once and retry under idempotency keys

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch15-s06

## Context

The charging journey (sections 15.2 to 15.5) sends requests to three contexts: Driver Accounts
(`LockForSession`, `UnlockFromSession`) and Site Energy (`RequestAllocation`,
`ReleaseAllocation`) through their shard regions, and the payment side and the charge points
through ports. Akka.NET delivers a `Tell` **at most once**. A message to a shard region can be
lost when the entity's node fails, while a shard is handed off, or when the receiving entity
stops with the message in its stash after a failed write (section 12.4). A port's call can time
out without saying whether it happened. The journey then hears nothing, and "no answer" does not
mean "no".

Two ways to get from at-most-once to "done exactly once, as far as anyone can tell" were
considered:

- **Akka.Delivery.** Akka.NET 1.5.73 ships reliable delivery: `Akka.Delivery.ProducerController`
  and `ConsumerController` in `Akka`, `EventSourcedProducerQueue` in `Akka.Persistence`, and
  `ShardingProducerController` and `ShardingConsumerController` in
  `Akka.Cluster.Sharding.Delivery` (the types are in the 1.5.73 assemblies; we did not run them).
  A producer controller numbers each message, resends it until the consumer confirms it, and,
  with the event-sourced queue, keeps unconfirmed messages in the journal. For the journey to
  use it, the site power budget's region would have to start a `ShardingConsumerController` in
  front of every `SitePowerBudgetActor`, which then confirms each delivery; every other sender
  of budget commands (the edge, the session-ended subscriber, the grid operator's ACL in Chapter
  16) would go through the same controller or around it; and each journey would own a second
  durable queue beside its own journal, holding requests that its own events already describe.
- **Retries under idempotency keys.** The journey is persistent already: its state says which
  request it is waiting for an answer to. It can ask again on a timer, and every request it sends
  carries a key derived from the journey and the step (`{journeyId}-hold`). The receivers already
  treat a request sent again as nothing new: the deciders by their state (a lock for the session
  that holds the token, a release of an allocation already released, a request for power the
  session has), the shells by the command id (section 12.6), and the payment side by the key.

## Decision

We will deliver the journey's requests at most once and make them safe to repeat:

- Every effect that leaves the journey carries an `IdempotencyKey` of the form
  `{journeyId}-{step}`. Commands to other contexts take their `CommandId` from it
  (`IdempotencyKey.CommandId`, derived with `GuidV7.Derive`); the ports receive the key itself,
  and an adapter must pass it on to the provider (TallyPay's `Idempotency-Key`, section 16.4).
- A step that waits for an answer starts a `Reply` timer (10 seconds). When it elapses, the
  journey asks again under the same key, and after a minute without an answer it gives up
  (`no-reply`).
- An abandoned journey gives back everything it **asked for**, not only what it was told it
  got: an unlock of a token it never locked, or a void of a hold that was never placed, changes
  nothing.
- A capture whose outcome is unknown (the port's call failed) counts as a failed attempt and is
  tried again under the same key: the payment side answers a capture it already made with the
  answer it gave.
- Receivers stay idempotent by their state, so a request sent again after the command-id window
  of the receiving shell has moved on is still harmless. Tests pin this for the id token
  (`Given_a_token_held_by_a_session_when_it_asks_again_then_it_still_holds_it`) and the site
  power budget (`Given_a_granted_request_when_it_is_asked_again_then_it_hears_its_allocation`,
  `Given_a_released_allocation_when_it_is_released_again_then_nothing_changes`).

We will not use Akka.Delivery between the journey and the site power budget now.

## Consequences

- Nothing in Site Energy or Driver Accounts changes shape: their regions, entities and other
  senders stay as they are, and the guarantee is visible in the journey's own decision table
  (`DeliveryTests`).
- The journey decides how long to wait and when to give up, in the domain's terms, instead of a
  delivery layer resending without end to an entity that refuses.
- Every receiver must stay idempotent, and every new request a journey makes needs a key and a
  receiver that honours it. A provider that ignores idempotency keys cannot sit behind the
  payment port.
- The key format and its derivation are durable (ADR 0009): a journey in flight during a release
  sends its next retry with the new code.
- If a context ever cannot be made idempotent, or messages must arrive in order under load,
  Akka.Delivery's sharded producer and consumer controllers are the place to start, and this ADR
  is superseded.
